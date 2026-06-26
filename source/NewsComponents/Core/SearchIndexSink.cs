using System;
using System.Collections.Generic;
using NewsComponents.Feed;
using NewsComponents.Search;

namespace NewsComponents
{
    /// <summary>
    /// The production <see cref="ISearchIndexSink"/>: a thin, stateless 1:1 pass-through onto the
    /// process-wide static <see cref="FeedSource.SearchHandler"/> (Slice 4 of the FeedSource
    /// decomposition).
    /// </summary>
    /// <remarks>
    /// <para>
    /// CRITICAL: the <see cref="LuceneSearch"/> handler is read <em>per call</em> through the injected
    /// <see cref="Func{TResult}"/> accessor (mirroring <see cref="FaviconStore"/>'s
    /// <c>() =&gt; UserCacheDataService</c> accessor), never captured at construction. This preserves the
    /// original "read the static <see cref="FeedSource.SearchHandler"/> on every call" semantics —
    /// including its global lazy first-use binding (<see cref="FeedSourceManager.SearchHandler"/>) and the
    /// <see cref="SearchIndexBehavior.NoIndexing"/> dodge — so behavior is unchanged and the existing
    /// characterization suite stays green.
    /// </para>
    /// <para>
    /// The sink holds NO feed/item state (no <c>feedsTable</c>/<c>categories</c>/<c>itemsTable</c>), so —
    /// unlike <see cref="FaviconStore"/> / <see cref="FeedListSerializer"/> — it needs no swap-rebuild
    /// guard: a single instance can live for the lifetime of its owning <see cref="FeedSource"/>.
    /// </para>
    /// </remarks>
    internal sealed class SearchIndexSink : ISearchIndexSink
    {
        /// <summary>
        /// Accessor for the process-wide static search handler. Invoked on every call (never cached) so the
        /// original lazy-binding and <c>NoIndexing</c> no-op semantics are preserved exactly.
        /// </summary>
        private readonly Func<LuceneSearch> searchHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchIndexSink"/> class.
        /// </summary>
        /// <param name="searchHandler">
        /// Accessor returning the static <see cref="FeedSource.SearchHandler"/>; called through on each use
        /// so the static handler is read per call (lazy binding preserved), not captured at construction.
        /// </param>
        public SearchIndexSink(Func<LuceneSearch> searchHandler)
        {
            this.searchHandler = searchHandler;
        }

        /// <inheritdoc/>
        public void IndexAdd(INewsItem item)
        {
            searchHandler().IndexAdd(item);
        }

        /// <inheritdoc/>
        public void IndexAdd(IList<INewsItem> items)
        {
            searchHandler().IndexAdd(items);
        }

        /// <inheritdoc/>
        public void IndexRemove(INewsItem item)
        {
            searchHandler().IndexRemove(item);
        }

        /// <inheritdoc/>
        public void IndexRemove(string feedId)
        {
            searchHandler().IndexRemove(feedId);
        }

        /// <inheritdoc/>
        public void Flush()
        {
            searchHandler().Flush();
        }

        /// <inheritdoc/>
        public void CheckIndex()
        {
            searchHandler().CheckIndex();
        }
    }
}
