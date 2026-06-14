using System;
using System.Diagnostics;
using System.IO;
using NewsComponents;
using NewsComponents.Net;

namespace RssBandit
{
    internal partial class RssBanditApplication
    {
        #region Podcast related routines

        /// <summary>
        /// Gets the current Enclosure folder
        /// </summary>		
        public string EnclosureFolder
        {
            get
            {
				return Preferences.EnclosureFolder;
            }
        }


        /// <summary>
        /// Gets the current Podcast folder
        /// </summary>		
        public string PodcastFolder
        {
            get
            {
				return Preferences.PodcastFolder;
            }
        }

        /// <summary>
        /// Indicates the number of enclosures which should be downloaded automatically from a newly subscribed feed.
        /// </summary>
        public int NumEnclosuresToDownloadOnNewFeed
        {
            get
            {
                return Preferences.NumEnclosuresToDownloadOnNewFeed;
            }
        }


        /// <summary>
        /// Indicates the maximum amount of space that enclosures and podcasts can use on disk.
        /// </summary>
        public int EnclosureCacheSize
        {
            get
            {
                return Preferences.EnclosureCacheSize;
            }
        }

        /// <summary>
        /// Gets a semi-colon delimited list of file extensions of enclosures that 
        /// should be treated as podcasts
        /// </summary>
        public string PodcastFileExtensions
        {
            get
            {
                return Preferences.PodcastFileExtensions;
            }
        }

        /// <summary>
        /// Gets whether enclosures should be created in a subfolder named after the feed. 
        /// </summary>
        public bool DownloadCreateFolderPerFeed
        {
            get
            {
                return Preferences.CreateSubfoldersForEnclosures;
            }
        }

        /// <summary>
        /// Gets whether alert Windows should be displayed for enclosures or not. 
        /// </summary>
        public bool EnableEnclosureAlerts
        {
            get
            {
                return Preferences.EnclosureAlert;
            }
        }

        /// <summary>
        /// Gets whether enclosures should be downloaded automatically or not.
        /// </summary>
        public bool DownloadEnclosures
        {
            get
            {
                return Preferences.DownloadEnclosures;
            }
        }

        /// <summary>
        /// Tests whether a downloaded file is one of the user's configured podcast
        /// types (the semicolon-delimited <see cref="PodcastFileExtensions"/> list).
        /// </summary>
        /// <param name="fileExtension">The file extension to test (with or without a leading dot).</param>
        /// <returns>True if the extension matches a configured podcast type.</returns>
        private bool IsPodcastFile(string fileExtension)
        {
            if (string.IsNullOrEmpty(fileExtension))
                return false;

            string ext = fileExtension.TrimStart('.');
            foreach (string podcastExt in PodcastFileExtensions.Split(new[] { ';' },
                                                                      StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(podcastExt.Trim().TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Opens a freshly downloaded podcast in the user's default media application via
        /// the Windows shell file association.
        /// </summary>
        /// <remarks>
        /// Replaced the iTunes/WMP COM "add to playlist" integration on 2026-06-14: both
        /// players are legacy (iTunes-for-Windows has been split into Apple's Music/Podcasts
        /// apps; Windows Media Player by the Media Player app), and a shell-open hands the
        /// file to whatever the user has set as their default podcast/media player - with no
        /// COM interop.
        /// </remarks>
        /// <param name="podcast">The downloaded item to open.</param>
        private void OpenPodcastInDefaultPlayer(DownloadItem podcast)
        {
            try
            {
                if (!IsPodcastFile(Path.GetExtension(podcast.File.LocalName)))
                {
                    return;
                }

                string fullPath = Path.Combine(podcast.TargetFolder, podcast.File.LocalName);
                if (!File.Exists(fullPath))
                {
                    return;
                }

                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo(fullPath) { UseShellExecute = true };
                    process.Start();
                }
            }
            catch (Exception e)
            {
                _log.Error("The following error occurred in OpenPodcastInDefaultPlayer(): ", e);
            }
        }

        #endregion
    }
}