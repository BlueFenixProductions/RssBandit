#region CVS Version Header

/*
 * $Id$
 * Last modified by $Author$
 * Last modified at $Date$
 * $Revision$
 */

#endregion

using System;
using System.Net;
using NewsComponents.Feed;

namespace NewsComponents.Net
{
    /// <summary>
    /// This class mantains all the information needed to describe
    /// a downloadable item.
    /// </summary>
    public sealed class DownloadItem
    {
        #region Private fields

        /// <summary>
        /// Information needed to download the file such as credentials, proxy information, etc
        /// </summary>
        private IDownloadInfoProvider downloadInfo;


        /// <summary>
        /// This represents the local file and also contains information about where it was downloaded from. 
        /// </summary>
        private readonly DownloadFile file;

        /// <summary>
        /// The enclosure that is being downloaded. 
        /// </summary>
        private readonly Enclosure enclosure;

        /// <summary>
        /// The ID for a DownloadItem owner, such as a feed.
        /// </summary>
        private readonly string ownerFeedId;

        /// <summary>
        /// Additional ID of a DownloadItem owner, such as a feed item.
        /// </summary>
        private readonly string ownerItemId;

        /// <summary>
        /// The download item id.
        /// </summary>
        private Guid downloadItemId = Guid.Empty;

        #endregion

        #region Public Constructors

        /// <summary>
        /// Creates a DownloadItem using the owner ID of the creating instance.
        /// </summary>
        /// <param name="ownerFeedId">The download owner ID.</param>
        /// <param name="ownerItemId">The download item ID</param>
        /// <param name="enclosure">Information about the item to download</param>
        /// <param name="downloadInfo">Information needed to download the files that is independent of the file</param>
        public DownloadItem(string ownerFeedId, string ownerItemId, Enclosure enclosure,
                            IDownloadInfoProvider downloadInfo)
        {
            this.ownerFeedId = ownerFeedId;
            this.ownerItemId = ownerItemId;
            this.enclosure = enclosure;
            file = new DownloadFile(enclosure);
            this.downloadInfo = downloadInfo;
        }

        #endregion

        #region DownloadItem Members

        /// <summary>
        /// The DownloadItem ID.
        /// </summary>
        public Guid ItemId
        {
            get
            {
                if (downloadItemId == Guid.Empty)
                {
                    downloadItemId = Guid.NewGuid();
                }
                return downloadItemId;
            }
        }

        /// <summary>
        /// The owner of the download item.
        /// </summary>
        public string OwnerFeedId
        {
            get { return ownerFeedId; }
        }


        /// <summary>
        /// The feed that this item belongs to. 
        /// </summary>
        public INewsFeed OwnerFeed { get; set; }

        /// <summary>
        /// The owner item of the download item.
        /// </summary>
        public string OwnerItemId
        {
            get { return ownerItemId; }
        }

        /// <summary>
        /// The target folder to place the downloaded file
        /// </summary>
        public string TargetFolder
        {
            get 
            {
                if (downloadInfo != null)
                    return downloadInfo.GetTargetFolder(this);
                else
                    return FeedSource.EnclosureFolder;
            }
        }


        /// <summary>
        /// The enclosure being downloaded. 
        /// </summary>
        public Enclosure Enclosure
        {
            get { return enclosure; }
        }


        /// <summary>
        /// This represents the local file and also contains information about where it was downloaded from. 
        /// </summary>
        public DownloadFile File
        {
            get { return file; }
        }

        /// <summary>
        /// The credentials needed to download the file.
        /// </summary>
        public ICredentials Credentials
        {
            get { return downloadInfo.GetCredentials(this); }
        }

        /// <summary>
        /// The proxy information
        /// </summary>
        public IWebProxy Proxy
        {
            get { return downloadInfo.Proxy; }
        }

        #endregion

        #region JSON persistence

        /// <summary>
        /// Constructor used when loading an item from its JSON persistence shape.
        /// </summary>
        internal DownloadItem(DownloadItemDto dto)
        {
            downloadItemId = dto.Id;
            ownerItemId = dto.OwnerItemId;
            ownerFeedId = dto.OwnerFeedId;
            enclosure = new Enclosure(dto.EnclosureMimeType, dto.EnclosureLength, dto.EnclosureUrl,
                                      dto.EnclosureDescription);
            file = new DownloadFile(enclosure);
        }

        /// <summary>
        /// Maps the item to its JSON persistence shape.
        /// </summary>
        internal DownloadItemDto ToDto()
        {
            return new DownloadItemDto
            {
                Id = downloadItemId,
                OwnerItemId = OwnerItemId,
                OwnerFeedId = OwnerFeedId,
                EnclosureUrl = enclosure.Url,
                EnclosureMimeType = enclosure.MimeType,
                EnclosureLength = enclosure.Length,
                EnclosureDescription = enclosure.Description,
            };
        }

        #endregion

        #region Public methods 

        /// <summary>
        /// Initializes the IDownloadInfoProvider for this object. This is needed if this DownloadItem is deserialized from disk. 
        /// </summary>
        /// <param name="downloadInfo"></param>
        public void Init(IDownloadInfoProvider downloadInfo)
        {
            this.downloadInfo = downloadInfo;
        }

        #endregion
    }

    /// <summary>
    /// JSON persistence shape of a <see cref="DownloadItem"/>, nested in
    /// <see cref="DownloadTaskDto"/>.
    /// </summary>
    internal sealed class DownloadItemDto
    {
        public Guid Id { get; set; }

        public string OwnerItemId { get; set; }

        public string OwnerFeedId { get; set; }

        public string EnclosureUrl { get; set; }

        public string EnclosureMimeType { get; set; }

        public long EnclosureLength { get; set; }

        public string EnclosureDescription { get; set; }
    }
}
