#region CVS Version Header

/*
 * $Id$
 * Last modified by $Author$
 * Last modified at $Date$
 * $Revision$
 */

#endregion

using System;
using System.IO;
using System.Text.Json.Serialization;
using NewsComponents.Core;

namespace NewsComponents.Net
{
    /// <summary>
    /// Holds the state information about an download in progress.
    /// </summary>
    public class DownloadTask : BindableObject
    {
        #region Private fields

        /// <summary>
        /// The task Id.
        /// </summary>
        private readonly Guid _id;

        private readonly DateTime _createDate;

        private string _fileName;

        /// <summary>
        /// The status of the download task.
        /// </summary>
        private DownloadTaskState _state = DownloadTaskState.Pending;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of the updater task using the specified item.
        /// </summary>
        /// <param name="item">The item corresponding to an updater task.</param>
        /// <param name="info">IDownloadInfo</param>
        public DownloadTask(DownloadItem item, IDownloadInfoProvider info)
        {
            _id = Guid.NewGuid();
            _createDate = DateTime.Now;

            Init(item, info);
        }

        #endregion

        #region DownloadTask Members


        /// <summary>
        /// The base folder where files will be downloaded.
        /// </summary>
        public string DownloadFilesBase { get; private set; }

        /// <summary>
        /// Initializes an existing DownloadTask with the specified item.
        /// </summary>
        /// <param name="item">The item corresponding to an updater task.</param>
        /// <param name="info">IDownloadInfoProvider</param>
        public void Init(DownloadItem item, IDownloadInfoProvider info)
        {
            DownloadItem = item;
            if (DownloadItem != null && info != null)
            {
                DownloadFilesBase = info.InitialDownloadLocation;
                DownloadItem.Init(info);

                // Set a default name
                if(FileName == null)
                    FileName = Path.Combine(DownloadItem.TargetFolder, DownloadItem.File.LocalName);
            }
        }

        /// <summary>
        /// An unique identifier for the DownloadTask used internally
        /// </summary>
        public Guid TaskId
        {
            get { return _id; }
        }

        private Guid? _jobId;

        /// <summary>
        /// An external Id that can be used for tracking
        /// </summary>
        public Guid? JobId
        {
            get { return _jobId; }
            set
            {
                _jobId = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// The current state of the DownloadTask.
        /// <see cref="DownloadTaskState"/>
        /// </summary>
        public DownloadTaskState State
        {
            get { return _state; }
            set
            {
                _state = value;
                RaisePropertyChanged();
            }
        }

        private string _errorText;
		/// <summary>
		/// Gets or sets the error text.
		/// </summary>
		/// <value>The error text.</value>
        public string ErrorText
        {
            get { return _errorText; }

            set
            {
                _errorText = value;
                RaisePropertyChanged();
            }
        }

		/// <summary>
		/// Gets the created date.
		/// </summary>
		/// <value>The created date.</value>
        public DateTime CreatedDate
        {
            get
            {
                return _createDate;
            }
        }

		/// <summary>
		/// Gets or sets the name of the file.
		/// </summary>
		/// <value>The name of the file.</value>
        public string FileName
        {
            get
            {
                return _fileName;
            }
            set
            {
                _fileName = value;
                RaisePropertyChanged();
            }
        }

        private long _fileSize;

		/// <summary>
		/// Gets the size of the file.
		/// </summary>
		/// <value>The size of the file.</value>
        public long FileSize
        {
            get { return _fileSize; }
            internal set
            {
                _fileSize = value;
                RaisePropertyChanged();

                CalculatePercentComplete();
            }
        }

        private long _transferredSize;

		/// <summary>
		/// Gets the size of the transferred.
		/// </summary>
		/// <value>The size of the transferred.</value>
        public long TransferredSize
        {
            get { return _transferredSize; }
            internal set
            {
                _transferredSize = value;
                RaisePropertyChanged();

                CalculatePercentComplete();
            }
        }

		/// <summary>
		/// Gets the percent complete.
		/// </summary>
		/// <value>The percent complete.</value>
        public double PercentComplete
        {
            get;
            private set;
        }

        private void CalculatePercentComplete()
        {
            if(FileSize > 0)
            {
                PercentComplete = (TransferredSize / (double)FileSize) * 100;
            }
            else
            {
                PercentComplete = 0;
            }

            RaisePropertyChanged();
        }

	    private int _downloadErrorResumeCount;
		/// <summary>
		/// Gets or sets the download error resume count.
		/// </summary>
		/// <value>The download error resume count.</value>
	    public int DownloadErrorResumeCount
	    {
		    get { return _downloadErrorResumeCount; }
		    set
		    {
			    _downloadErrorResumeCount = value;
			    RaisePropertyChanged();
		    }
	    }

		/// <summary>
		/// Gets a value indicating whether this instance can cancel resume.
		/// </summary>
		/// <value><c>true</c> if this instance can cancel resume; otherwise, <c>false</c>.</value>
        public bool CanCancelResume
        {
            get
            {
                return _downloader != null;
            }
            
        }

		/// <summary>
		/// Cancels this downloading instance.
		/// </summary>
        public void Cancel()
        {
            if(CanCancelResume)
                Downloader.CancelDownload(this);
        }

        /// <summary>
        /// The item corresponding to the current DownloadTask.
        /// </summary>
        public DownloadItem DownloadItem { get; private set; }

        private IDownloader _downloader;

        /// <summary>
        /// The IDownloader instance responsible for downloading this task
        /// </summary>
        internal IDownloader Downloader
        {
            get { return _downloader; }
            set
            {
                _downloader = value;
                RaisePropertyChanged(nameof(CanCancelResume));
                RaisePropertyChanged();
            }
        }


        private bool _supportsBITS; 

        /// <summary>
        /// Indicates whether the downloader for this task supports BITS or not. 
        /// </summary>
        public bool SupportsBITS {
            get { return _supportsBITS;  } 
            set { _supportsBITS = value; }
        }

        #endregion

        #region JSON persistence

        /// <summary>
        /// Constructor used when loading a task from its JSON persistence shape.
        /// </summary>
        private DownloadTask(DownloadTaskDto dto)
        {
            DownloadItem = dto.Item != null ? new DownloadItem(dto.Item) : null;
            _state = dto.State;
            _id = dto.Id;
            JobId = dto.JobId;
            TransferredSize = dto.TransferredSize;
            FileSize = dto.FileSize;
            _createDate = TimeZoneInfo.ConvertTime(
                DateTime.SpecifyKind(dto.CreatedDateUtc, DateTimeKind.Utc), TimeZoneInfo.Local);
            _fileName = dto.FileName;
            _errorText = dto.ErrorText;
            _supportsBITS = dto.SupportsBits;
            DownloadFilesBase = dto.DownloadFilesBase;
            _downloadErrorResumeCount = dto.DownloadErrorResumeCount;
        }

        /// <summary>
        /// Recreates a task from its JSON persistence shape.
        /// </summary>
        internal static DownloadTask FromDto(DownloadTaskDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));
            return new DownloadTask(dto);
        }

        /// <summary>
        /// Maps the task to its JSON persistence shape.
        /// </summary>
        internal DownloadTaskDto ToDto()
        {
            return new DownloadTaskDto
            {
                Item = DownloadItem?.ToDto(),
                State = _state,
                Id = _id,
                JobId = JobId,
                TransferredSize = TransferredSize,
                FileSize = FileSize,
                CreatedDateUtc = TimeZoneInfo.ConvertTimeToUtc(_createDate),
                FileName = _fileName,
                ErrorText = _errorText,
                SupportsBits = _supportsBITS,
                DownloadFilesBase = DownloadFilesBase,
                DownloadErrorResumeCount = DownloadErrorResumeCount,
            };
        }

        #endregion
    }

    /// <summary>
    /// JSON persistence shape of a <see cref="DownloadTask"/> (the *.task files in
    /// the download.registry folder). Replaced the BinaryFormatter format in Phase B;
    /// task files in the legacy binary format are discarded on load.
    /// </summary>
    internal sealed class DownloadTaskDto
    {
        public Guid Id { get; set; }

        public Guid? JobId { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public DownloadTaskState State { get; set; }

        public long TransferredSize { get; set; }

        public long FileSize { get; set; }

        public DateTime CreatedDateUtc { get; set; }

        public string FileName { get; set; }

        public string ErrorText { get; set; }

        public bool SupportsBits { get; set; }

        public string DownloadFilesBase { get; set; }

        public int DownloadErrorResumeCount { get; set; }

        public DownloadItemDto Item { get; set; }
    }
}
