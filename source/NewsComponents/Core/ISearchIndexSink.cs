using System.Collections.Generic;
using NewsComponents.Feed;

namespace NewsComponents
{
    /// <summary>
    /// The narrow instance-facing surface of the process-wide Lucene search index used by the
    /// <see cref="FeedSource"/> call sites (Slice 4 of the FeedSource decomposition).
    /// </summary>
    /// <remarks>
    /// This seam is introduced specifically because the search-index calls reach a process-wide,
    /// lazily-bound, <c>sealed</c> <see cref="NewsComponents.Search.LuceneSearch"/> singleton via the
    /// static <see cref="FeedSource.SearchHandler"/>. The unit-test suite (and the shipped app default)
    /// neuter that singleton to <see cref="SearchIndexBehavior.NoIndexing"/>, so every index call is a
    /// silent no-op and there was no way to observe routing. The interface lets a
    /// <c>RecordingSearchIndexSink</c> be injected so each routed call site can be pinned, while the
    /// production <see cref="SearchIndexSink"/> remains a pure 1:1 pass-through onto the static handler
    /// (so the existing no-op behavior — and the green characterization suite — is preserved exactly).
    ///
    /// The six members mirror the real <see cref="NewsComponents.Search.LuceneSearch"/> signatures used
    /// by the instance call sites.
    /// </remarks>
    internal interface ISearchIndexSink
    {
        /// <summary>Adds a single news item to the search index.</summary>
        void IndexAdd(INewsItem item);

        /// <summary>Adds a list of news items to the search index.</summary>
        void IndexAdd(IList<INewsItem> items);

        /// <summary>Removes a single news item from the search index.</summary>
        void IndexRemove(INewsItem item);

        /// <summary>Removes a feed (and all its items) from the search index.</summary>
        void IndexRemove(string feedId);

        /// <summary>Flushes pending index operations/writes to disk.</summary>
        void Flush();

        /// <summary>Checks/(re)builds the index if required.</summary>
        void CheckIndex();
    }
}
