#region CVS Version Header

/*
 * $Id$
 * Last modified by $Author$
 * Last modified at $Date$
 * $Revision$
 */

#endregion

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using log4net;
using NewsComponents.Utils;
using RssBandit.Common.Logging;

namespace NewsComponents.Net
{
    /// <summary>
    /// Manages the list of download tasks under progress, from its initial registration when they
    /// are submitted to download, to their final state when they are successfully downloaded
    /// or cancelled.
    /// </summary>
    public sealed class DownloadRegistryManager
    {
        #region Private fields

        /// <summary>
        /// The singleton instance stored.
        /// </summary>
        private static readonly DownloadRegistryManager instance = new DownloadRegistryManager();

        private static readonly ILog Logger = Log.GetLogger(typeof (DownloadRegistryManager));

        private SynchronizationContext _context;

        /// <summary>
        /// Root folder name for the registry.
        /// </summary>
        private const string root = "download.registry";

        /// <summary>
        /// Helper class for directory information.
        /// </summary>
        private DirectoryInfo rootDirInfo;

        /// <summary>
        /// The in memory registry storage.
        /// </summary>
        private readonly Dictionary<string, DownloadTask> registry = new Dictionary<string, DownloadTask>();

        private readonly ObservableCollection<DownloadTask> _tasks = new ObservableCollection<DownloadTask>();
        private readonly ReadOnlyObservableCollection<DownloadTask> _tasksRo;

        /// <summary>
        /// Indicates if the list of tasks is loaded
        /// </summary>
        private volatile bool loaded;

        private static readonly JsonSerializerOptions TaskSerializationOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        #endregion

        #region Public fields 

        // Intentional app-lifetime singletons: never disposed. HttpDownloader.Dispose
        // only cancels an in-flight task and BITS handles are reclaimed at process
        // exit, so disposing these at shutdown adds ordering risk for no benefit.

        /// <summary>
        /// The HTTP downloader used by tasks loaded on startup
        /// </summary>
        public static readonly HttpDownloader httpDownloader = new HttpDownloader();

        #endregion


        #region Singleton implementation

        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static DownloadRegistryManager Current
        {
            get { return instance; }
        }

        /// <summary>
        /// Default constructor disable because there is a singleton implementation.
        /// </summary>
        private DownloadRegistryManager()
        {
            _tasksRo = new ReadOnlyObservableCollection<DownloadTask>(_tasks);
        }

        #endregion

        #region Public Methods

        public void Initialize()
        {
            _context = SynchronizationContext.Current;
        }

        /// <summary>
        /// Directs the instance to use a different base path
        /// than Temp.
        /// </summary>
        /// <param name="baseFolder">string</param>
        public void SetBaseFolder(string baseFolder)
        {
            string path = Path.Combine(baseFolder, root);
            if (!Directory.Exists(path))
            {
                rootDirInfo = Directory.CreateDirectory(path);
            }
            else
            {
                rootDirInfo = new DirectoryInfo(path);
            }
        }

        /// <summary>
        /// Loads all the pending tasks.
        /// </summary>
        public void Load()
        {
            if (loaded)
                return;

            lock (registry)
            {
                if (!loaded)
                {
                    foreach (var fi in RootDir.GetFiles())
                    {
                        LoadTask(fi.FullName);
                    }

                    loaded = true;
                }
            }
        }

        /// <summary>
        /// Updates the information of an existing stored task.
        /// </summary>
        /// <param name="task">The DownloadTask instance.</param>
        public void UpdateTask(DownloadTask task)
        {
            SaveTask(task);
        }


        /// <summary>
        /// Indicates whether a download task is already being downloaded that corresponds to the input task
        /// </summary>
        /// <param name="task">The DownloadTask instance</param>
        /// <returns>True if there is already a download task for the enclosure</returns>
        public bool IsTaskActive(DownloadTask task)
        {
            lock(registry)
            {
                if (TasksDictionary.ContainsKey(task.DownloadItem.Enclosure.Url))
                {
                    DownloadTask existingTask = TasksDictionary[task.DownloadItem.Enclosure.Url];
                    return existingTask.State == DownloadTaskState.Downloading || existingTask.State == DownloadTaskState.Pending; 
                }
                return false; 
            }
        }

        /// <summary>
        /// Registers the task in the storage.
        /// </summary>
        /// <param name="task">The DownloadTask instance.</param>
        public void RegisterTask(DownloadTask task)
        {
            lock (registry)
            {
                if (!TasksDictionary.ContainsKey(task.DownloadItem.Enclosure.Url))
                {
                    TasksDictionary.Add(task.DownloadItem.Enclosure.Url, task);
                    AddTask(task);
                }
                else //change state to downloading because we are reattempting
                {
                    TasksDictionary[task.DownloadItem.Enclosure.Url].State = DownloadTaskState.Pending; 
                }
            }
            SaveTask(task);
        }

        /// <summary>
        /// Removes the task from the storage.
        /// </summary>
        /// <param name="task">The DownloadTask.</param>
        public void UnRegisterTask(DownloadTask task)
        {
            lock (registry)
            {
                if(TasksDictionary.ContainsKey(task.DownloadItem.Enclosure.Url))
                {
                    TasksDictionary.Remove(task.DownloadItem.Enclosure.Url);
                    RemoveTask(task);
                    var disposable = task.Downloader as IDisposable;
                    if (disposable != null)
                    {
                        disposable.Dispose();
                    }
                }
            }
            string fileName = Path.Combine(RootDir.FullName, task.TaskId + ".task");
            FileHelper.DestroyFile(fileName);
        }

        /// <summary>
        /// Return all the tasks stored in memory.
        /// </summary>
        /// <returns>An array of DownloadTask instances.</returns>
        public DownloadTask[] GetTasks()
        {
            lock (registry)
            {
                return TasksDictionary.Values.ToArray();
            }
        }

        /// <summary>
        /// Returns all the stored tasks by a given owner id.
        /// </summary>
        /// <param name="ownerId">The owner id.</param>
        /// <returns>An array of DownloadTask instances.</returns>
        public DownloadTask[] GetByOwnerId(string ownerId)
        {
            lock (registry)
            {

                var tasks = from task in TasksDictionary.Values
                            where task.DownloadItem.OwnerFeedId == ownerId
                            select task;

                return tasks.ToArray();
            }
        }

        /// <summary>
        /// Returns all the stored tasks by a given owner item id.
        /// </summary>
        /// <param name="ownerItemId">The owner item id.</param>
        /// <returns>An array of DownloadTask instances.</returns>
        public DownloadTask[] GetByOwnerItemId(string ownerItemId)
        {

            lock (registry)
            {

                var tasks = from task in TasksDictionary.Values
                            where task.DownloadItem.OwnerItemId == ownerItemId
                            select task;

                return tasks.ToArray();
            }
        }

        /// <summary>
        /// Return the DownloadTask for a given item id.
        /// </summary>
        /// <param name="itemId">The item id (Guid).</param>
        /// <returns>An DownloadTask instance.</returns>
        public DownloadTask GetByItemID(Guid itemId)
        {
            lock (registry)
            {

                return TasksDictionary.Values.FirstOrDefault(task => task.DownloadItem.ItemId == itemId);
            }
        }

        #endregion

        #region Private Methods

        private DirectoryInfo RootDir
        {
            get
            {
                if (rootDirInfo != null)
                    return rootDirInfo;
                SetBaseFolder(Path.GetTempPath());
                return rootDirInfo;
            }
        }

        /// <summary>
        /// Load the tasks stored in a specified path.
        /// </summary>
        /// <param name="taskFilePath">The base path for the registry storage.</param>
        /// <returns>An DownloadTask instance.</returns>
        private DownloadTask LoadTask(string taskFilePath)
        {
            DownloadTask task = null;
            try
            {
                DownloadTaskDto dto;
                using (var stream = new FileStream(taskFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    dto = JsonSerializer.Deserialize<DownloadTaskDto>(stream);
                }

                task = DownloadTask.FromDto(dto);
                task.Downloader = httpDownloader;

                lock (registry)
                {
                    //TODO: Once we have a UI for managing enclosures we'll need to
                    //always load all tasks.
                    if (!registry.ContainsKey(task.DownloadItem.Enclosure.Url))
                    {
                        // Revert to pending if it was in a downloading state
                        if (task.State == DownloadTaskState.Downloading)
                            task.State = DownloadTaskState.Pending;

                        registry.Add(task.DownloadItem.Enclosure.Url, task);
                        AddTask(task);
                    }
                    else
                    {
                        FileHelper.DestroyFile(Path.Combine(RootDir.FullName, task.TaskId + ".task"));
                    }
                }
            }
            catch (Exception e)
            {
                // not a readable JSON task file: either corrupt, or written by the
                // legacy BinaryFormatter format (pre Phase B) - discard it; the
                // enclosure can simply be downloaded again.
                Logger.Error("DownloadRegistryManager.LoadTask(): unreadable task file gets removed: " + taskFilePath, e);
                task = null;
                FileHelper.DestroyFile(taskFilePath);
            }
            return task;
        }

        /// <summary>
        /// Stores a task in the registry storage.
        /// </summary>
        /// <param name="task">The DownloadTask instance.</param>
        private void SaveTask(DownloadTask task)
        {
            string filename = Path.Combine(RootDir.FullName,
                                           String.Format(CultureInfo.InvariantCulture, "{0}.task", task.TaskId));
	        try
	        {
		        using (Stream stream = FileHelper.OpenForWrite(filename))
		        {
			        JsonSerializer.Serialize(stream, task.ToDto(), TaskSerializationOptions);
		        }
	        }
	        catch (IOException ex)
	        {
				Logger.Error(ex);
	        }
            catch (Exception ex)
            {
                File.Delete(filename);
                Logger.Error(ex);
                throw;
            }
        }

        private void AddTask(DownloadTask task)
        {
            // _context is only set when Initialize() ran on the dispatcher thread;
            // fall back to a direct add in headless/startup-race scenarios
            var ctx = _context;
            if (ctx != null)
                ctx.Post(o => _tasks.Add(task), null);
            else
                _tasks.Add(task);
        }

        private void RemoveTask(DownloadTask task)
        {
            var ctx = _context;
            if (ctx != null)
                ctx.Post(o => _tasks.Remove(task), null);
            else
                _tasks.Remove(task);
        }


        /// <summary>
        /// Gets the list of registered tasks, ensuring the list is loaded
        /// </summary>
        private Dictionary<string, DownloadTask> TasksDictionary
        {
            get
            {
                Load();
                return registry;
            }
        }

        public ReadOnlyObservableCollection<DownloadTask> Tasks
        {
            get
            {
                Load();
                return _tasksRo;
            }
        }

        #endregion
    }
}
