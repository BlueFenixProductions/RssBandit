using System.Collections.Generic;
using System.IO;
using System.Xml;
using NewsComponents.Feed;

namespace NewsComponents
{
    /// <summary>
    /// The feed-list <em>save/serialize</em> accessor extracted from the <c>FeedSource</c>
    /// god-object (Slice 3a of the FeedSource decomposition).
    /// </summary>
    /// <remarks>
    /// This is the WRITE half only: serializing the in-memory feed set to a stream in either the
    /// native RSS Bandit format (<see cref="FeedListFormat.NewsHandler"/> /
    /// <see cref="FeedListFormat.NewsHandlerLite"/>) or OPML (<see cref="FeedListFormat.OPML"/>).
    /// The parse/import/merge half (<c>ImportFeedlist</c>, <c>ConvertFeedList</c>, the merge engine)
    /// stays on <c>FeedSource</c> and is the subject of Slice 3b.
    ///
    /// Because the on-disk format is compared byte-for-byte by the sync feature, this is a
    /// behavior-preserving move: the OPML UTF-8-BOM indented writer, the native no-BOM
    /// <see cref="System.IO.StreamWriter"/> left open, the seven forced-false <c>*Specified</c>
    /// flags, the <c>categories</c>/<c>identities</c> null-omissions and the <c>AddViewedStory</c>
    /// save side effect are all reproduced exactly.
    ///
    /// The surface deals in the engine types <see cref="INewsFeed"/> and <see cref="FeedListFormat"/>,
    /// so the contract lives in NewsComponents rather than the contracts layer.
    /// </remarks>
    internal interface IFeedListSerializer
    {
        /// <summary>
        /// Saves the provided feed list to the specified stream.
        /// </summary>
        /// <param name="feedStream">The stream to save the feed list to.</param>
        /// <param name="format">The format to save the stream as.</param>
        /// <param name="feeds">FeedsCollection containing the feeds to save. Can contain a subset of
        /// the owned feeds collection.</param>
        /// <param name="includeEmptyCategories">Set to true, if categories without a contained feed
        /// should be included.</param>
        /// <exception cref="System.InvalidOperationException">If anything wrong goes on with XmlSerializer.</exception>
        /// <exception cref="System.ArgumentNullException">If feedStream is null.</exception>
        void WriteFeedList(Stream feedStream, FeedListFormat format,
                           IDictionary<string, INewsFeed> feeds, bool includeEmptyCategories);

        /// <summary>
        /// Parses a feed-list stream into a <see cref="feeds"/> object: loads the stream into an
        /// <see cref="XmlDocument"/>, normalizes the format via <see cref="ConvertFeedList"/> and
        /// deserializes the result. This is the read counterpart of <see cref="WriteFeedList"/>;
        /// the state-mutating merge engine stays on <c>FeedSource</c>.
        /// </summary>
        /// <param name="feedlist">The stream containing the feed list.</param>
        /// <returns>The deserialized <see cref="feeds"/> object.</returns>
        feeds ParseFeedList(Stream feedlist);

        /// <summary>
        /// Converts the input XML document from OCS, OPML or SIAM to the RSS Bandit feed list format.
        /// </summary>
        /// <param name="doc">The input feed list.</param>
        /// <returns>The converted feed list.</returns>
        /// <exception cref="System.ApplicationException">If the feed list format is unknown.</exception>
        XmlDocument ConvertFeedList(XmlDocument doc);
    }
}
