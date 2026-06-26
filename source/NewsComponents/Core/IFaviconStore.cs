using System;
using NewsComponents.Feed;

namespace NewsComponents
{
    /// <summary>
    /// The per-feed favicon <em>storage</em> accessor cluster extracted from the
    /// <c>FeedSource</c> god-object (Slice 2 of the FeedSource decomposition).
    /// </summary>
    /// <remarks>
    /// This is the storage half only: looking a feed up by URL, reading/writing the
    /// <see cref="INewsFeed.favicon"/> content-id, and round-tripping the cached image blob through
    /// the user cache data service. The favicon <em>download</em> half (the async request completion,
    /// image-format sniffing and refresh scheduling) stays on <c>FeedSource</c>.
    ///
    /// Unlike <c>IFeedAndCategorySettings</c>, this surface deals in the engine types
    /// <see cref="INewsFeed"/> and <see cref="byte"/>[], so the contract lives in NewsComponents
    /// rather than the contracts layer.
    ///
    /// The contract intentionally pins the surprising-but-current behavior characterized by
    /// <c>FaviconStoreTests</c>: the string overloads throw <see cref="ArgumentException"/> (not
    /// <see cref="ArgumentNullException"/>) on a null/empty/whitespace url; <c>SetFaviconForFeed</c>
    /// is a silent no-op when the content-id or image data is null/empty; and
    /// <c>RemoveFaviconFromFeed</c> deletes the cached blob but does NOT clear
    /// <see cref="INewsFeed.favicon"/> (so <c>FeedHasFavicon</c> still returns true afterwards).
    /// </remarks>
    public interface IFaviconStore
    {
        /// <summary>
        /// Gets true, if the feed has a favicon.
        /// </summary>
        /// <param name="feedUrl">The feed URL.</param>
        bool FeedHasFavicon(string feedUrl);

        /// <summary>
        /// Gets true, if the feed has a favicon.
        /// </summary>
        /// <param name="feed">The feed.</param>
        bool FeedHasFavicon(INewsFeed feed);

        /// <summary>
        /// Gets the favicon for feed, or null in case there is none.
        /// </summary>
        /// <param name="feedUrl">The feed URL.</param>
        byte[] GetFaviconForFeed(string feedUrl);

        /// <summary>
        /// Gets the favicon for feed, or null in case there is none.
        /// </summary>
        /// <param name="feed">The feed.</param>
        byte[] GetFaviconForFeed(INewsFeed feed);

        /// <summary>
        /// Sets the favicon for a feed. Assigns the favicon property and stores the byte array.
        /// </summary>
        /// <param name="feedUrl">The feed URL.</param>
        /// <param name="contentId">The content id.</param>
        /// <param name="imageData">The image data.</param>
        void SetFaviconForFeed(string feedUrl, string contentId, byte[] imageData);

        /// <summary>
        /// Sets the favicon for a feed. Assigns the favicon property and stores the byte array.
        /// </summary>
        /// <param name="feed">The feed.</param>
        /// <param name="contentId">The content id.</param>
        /// <param name="imageData">The image data.</param>
        void SetFaviconForFeed(INewsFeed feed, string contentId, byte[] imageData);

        /// <summary>
        /// Removes the favicon from feed.
        /// </summary>
        /// <param name="feed">The feed.</param>
        void RemoveFaviconFromFeed(INewsFeed feed);
    }
}
