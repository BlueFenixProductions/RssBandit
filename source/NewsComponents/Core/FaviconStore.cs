using System;
using System.Collections.Generic;
using NewsComponents.Feed;
using NewsComponents.Storage;

namespace NewsComponents
{
    /// <summary>
    /// Owns the per-feed favicon <em>storage</em> engine extracted verbatim from the
    /// <see cref="FeedSource"/> god-object (Slice 2 of the FeedSource decomposition).
    /// <see cref="FeedSource"/> keeps its public favicon storage methods as thin delegations
    /// onto an instance of this class.
    /// </summary>
    /// <remarks>
    /// This is a behavior-preserving move. The engine reads the <em>live</em> <c>feedsTable</c>
    /// owned by the originating <see cref="FeedSource"/> (passed by reference, never copied) so a
    /// table swap by <c>ImportFeedlist(replace:true)</c> is handled by the owner re-creating this
    /// component on a <c>ReferenceEquals</c> mismatch. The user cache data service is read
    /// <em>lazily</em> through an injected accessor (rather than captured at construction) so that
    /// the original "read the lazy property on each call" semantics — including lazy first-use
    /// creation — are preserved exactly.
    ///
    /// None of the quirks pinned by <c>FaviconStoreTests</c> are altered: the string overloads
    /// throw <see cref="ArgumentException"/> (not <see cref="ArgumentNullException"/>) on a
    /// null/empty/whitespace url; <see cref="SetFaviconForFeed(INewsFeed, string, byte[])"/> is a
    /// silent no-op when the content-id or image data is null/empty; and
    /// <see cref="RemoveFaviconFromFeed"/> deletes the cached blob but does NOT clear
    /// <see cref="INewsFeed.favicon"/>.
    /// </remarks>
    internal sealed class FaviconStore : IFaviconStore
    {
        /// <summary>The live feeds table owned by the FeedSource (same reference, not a copy).</summary>
        private readonly IDictionary<string, INewsFeed> feedsTable;

        /// <summary>
        /// Accessor for the FeedSource's lazily-initialized user cache data service. Invoked on every
        /// call (never cached) so the original lazy-on-first-use creation semantics are preserved.
        /// </summary>
        private readonly Func<IUserCacheDataService> userCacheDataServiceAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="FaviconStore"/> class.
        /// </summary>
        /// <param name="feeds">The FeedSource's live <c>feedsTable</c> (same reference, not a copy).</param>
        /// <param name="userCacheDataServiceAccessor">
        /// Accessor that returns the owning <see cref="FeedSource"/>'s lazily-initialized
        /// <c>UserCacheDataService</c>; called through on each use so lazy creation is preserved.
        /// </param>
        public FaviconStore(
            IDictionary<string, INewsFeed> feeds,
            Func<IUserCacheDataService> userCacheDataServiceAccessor)
        {
            this.feedsTable = feeds;
            this.userCacheDataServiceAccessor = userCacheDataServiceAccessor;
        }

        /// <summary>The live feeds table this component reads; used by the owner to detect a table swap.</summary>
        internal IDictionary<string, INewsFeed> FeedsTable
        {
            get { return feedsTable; }
        }

        /// <summary>
        /// The owning FeedSource's lazily-initialized user cache data service, read live on each access.
        /// </summary>
        private IUserCacheDataService UserCacheDataService
        {
            get { return userCacheDataServiceAccessor(); }
        }

        /// <summary>
        /// Gets true, if the feed has a favicon.
        /// </summary>
        /// <param name="feedUrl">The feed URL.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">If feedUrl is null or empty</exception>
        public bool FeedHasFavicon(string feedUrl)
        {
            if (string.IsNullOrWhiteSpace(feedUrl))
            {
                throw new ArgumentException("message", nameof(feedUrl));
            }

            INewsFeed f;
            if (!feedsTable.TryGetValue(feedUrl, out f))
            {
                return false;
            }
            return FeedHasFavicon(f);
        }

        /// <summary>
        /// Gets true, if the feed has a favicon.
        /// </summary>
        /// <param name="feed">The feed.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">If feed is null</exception>
        public bool FeedHasFavicon(INewsFeed feed)
        {
            if (feed == null)
            {
                throw new ArgumentNullException(nameof(feed));
            }

            return !String.IsNullOrEmpty(feed.favicon);
        }

        /// <summary>
        /// Gets the favicon for feed, or null in case there is none.
        /// </summary>
        /// <param name="feedUrl">The feed URL.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">If feedUrl is null or empty</exception>
        public byte[] GetFaviconForFeed(string feedUrl)
        {
            if (string.IsNullOrWhiteSpace(feedUrl))
            {
                throw new ArgumentException("message", nameof(feedUrl));
            }

            INewsFeed f;
            if (!feedsTable.TryGetValue(feedUrl, out f))
            {
                return null;
            }
            return GetFaviconForFeed(f);
        }

        /// <summary>
        /// Gets the favicon for feed, or null in case there is none.
        /// </summary>
        /// <param name="feed">The feed.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">If feed is null</exception>
        public byte[] GetFaviconForFeed(INewsFeed feed)
        {
            if (feed == null)
            {
                throw new ArgumentNullException(nameof(feed));
            }

            if (FeedHasFavicon(feed))
            {
                return UserCacheDataService.GetBinaryContent(feed.favicon);
            }
            return null;
        }

        /// <summary>
        /// Sets the favicon for a feed. Assigns the favicon property and
        /// store the byte array.
        /// </summary>
        /// <param name="feedUrl">The feed URL.</param>
        /// <param name="contentId">The content id.</param>
        /// <param name="imageData">The image data.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="feedUrl"/> is null or empty</exception>
        public void SetFaviconForFeed(string feedUrl, string contentId, byte[] imageData)
        {
            if (string.IsNullOrWhiteSpace(feedUrl))
            {
                throw new ArgumentException("message", nameof(feedUrl));
            }

            INewsFeed f;
            if (!feedsTable.TryGetValue(feedUrl, out f))
            {
                return;
            }
            SetFaviconForFeed(f, contentId, imageData);
        }

        /// <summary>
        /// Sets the favicon for a feed. Assigns the favicon property and
        /// store the byte array.
        /// </summary>
        /// <param name="feed">The feed.</param>
        /// <param name="contentId">The content id.</param>
        /// <param name="imageData">The image data.</param>
        /// <exception cref="ArgumentNullException">If feed is null</exception>
        public void SetFaviconForFeed(INewsFeed feed, string contentId, byte[] imageData)
        {
            if (feed == null)
            {
                throw new ArgumentNullException(nameof(feed));
            }

            if (!String.IsNullOrEmpty(contentId) && imageData != null && imageData.Length > 0)
            {
                UserCacheDataService.SaveBinaryContent(contentId, imageData);
                feed.favicon = contentId;
            }
        }

        /// <summary>
        /// Removes the favicon from feed.
        /// </summary>
        /// <param name="feed">The feed.</param>
        /// <exception cref="ArgumentNullException">If feed is null</exception>
        public void RemoveFaviconFromFeed(INewsFeed feed)
        {
            if (feed == null)
            {
                throw new ArgumentNullException(nameof(feed));
            }

            if (FeedHasFavicon(feed))
            {
                UserCacheDataService.DeleteBinaryContent(feed.favicon);
            }
        }
    }
}
