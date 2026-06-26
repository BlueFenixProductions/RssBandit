using System;
using System.Collections.Generic;
using System.IO;
using NewsComponents.Feed;
using NUnit.Framework;
using RssBandit.UnitTests;

namespace NewsComponents.UnitTests
{
    /// <summary>
    /// Interaction (routing) tests for Slice 4 of the FeedSource decomposition: the extraction of the
    /// search-index calls behind <see cref="ISearchIndexSink"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This slice deliberately deviates from the "characterization-tests-first" ordering of the prior
    /// slices. The search-index calls reach a process-wide, lazily-bound, <c>sealed</c>
    /// <see cref="NewsComponents.Search.LuceneSearch"/> singleton via the static
    /// <see cref="FeedSource.SearchHandler"/>, which both the test suite and the shipped app neuter to
    /// <see cref="SearchIndexBehavior.NoIndexing"/> (every index call a silent no-op). There was no seam
    /// to <em>observe</em> a search-index call, and a recording fake could not be injected because
    /// <c>LuceneSearch</c> is sealed. So the seam, the routing, and these pinning tests all land together:
    /// the existing 106 characterization tests staying green proves the no-op paths are preserved; these
    /// tests prove each routed site forwards to the correct sink method (which the green suite alone
    /// cannot, since <c>NoIndexing</c> swallows everything).
    /// </para>
    /// <para>
    /// Each test injects a <see cref="RecordingSearchIndexSink"/> via the internal
    /// <see cref="FeedSource.SearchIndexSink"/> setter (reachable through
    /// <c>InternalsVisibleTo("NewsComponents.UnitTests")</c>), drives a PUBLIC trigger, and asserts the
    /// recorded calls match the ORIGINAL behavior at the call site (not "whatever the new routing does").
    /// </para>
    /// <para>
    /// Coverage map (routed instance sites a-i from the slice brief):
    /// <list type="bullet">
    ///   <item>a — <see cref="FeedSource.DeleteAllItemsInFeed(INewsFeed)"/> -&gt; IndexRemove(feedId).</item>
    ///   <item>b — <see cref="FeedSource.RestoreDeletedItem(INewsItem)"/> -&gt; IndexAdd(item).</item>
    ///   <item>c — <see cref="FeedSource.RestoreDeletedItem(IList{INewsItem})"/> -&gt; per-item IndexAdd(item) AND IndexAdd(list) (the double-index).</item>
    ///   <item>f — <c>SaveFeed</c> purge loop (reached via <see cref="FeedSource.ApplyFeedModifications(string)"/>) -&gt; IndexRemove(item).</item>
    ///   <item>g — <see cref="FeedSource.DeleteFeed(string)"/> -&gt; IndexRemove(feedId).</item>
    ///   <item>h — <see cref="FeedSource.DeleteAllFeedsAndCategories(bool)"/> (false branch) -&gt; IndexRemove(feedId) per feed; the true branch routes through DeleteFeed.</item>
    ///   <item>i — <c>BanditFeedSource.LoadFeedlist</c> -&gt; CheckIndex().</item>
    /// </list>
    /// Plus the negative pin: <see cref="FeedSource.DeleteItem(INewsItem)"/> makes ZERO sink calls.
    /// </para>
    /// <para>
    /// Sites d (<c>OnRequestComplete</c> IndexAdd(items)) and e (<c>OnAllRequestsComplete</c> Flush()) are
    /// on the async refresh hot path. There is no existing unit test in this suite that drives a FeedSource
    /// refresh to completion, and constructing one (async callbacks, threads, timing) would be a novel,
    /// flaky test. Per the brief these two sites are covered by routing-inspection only: each is a verbatim
    /// <c>SearchHandler.X(arg)</c> -&gt; <c>SearchIndexSink.X(arg)</c> replacement with identical arguments
    /// and the same per-call static read. They are NOT pinned by a fake assertion here.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class SearchIndexSinkTests : BaseTestFixture
    {
        private string _origUserLocalApplicationDataPath;

        [SetUp]
        public void SetUp()
        {
            DeleteDirectory(UNPACK_DESTINATION);
            // Needed only by the CheckIndex-on-load test, which loads a local feed-list fixture.
            UnpackResourceDirectory("WebRoot.NewsHandlerTestFiles");

            var cfg = NewsComponentsConfiguration.Default as NewsComponentsConfiguration;
            _origUserLocalApplicationDataPath = cfg.UserLocalApplicationDataPath;
        }

        [TearDown]
        public void TearDown()
        {
            var cfg = NewsComponentsConfiguration.Default as NewsComponentsConfiguration;
            cfg.UserLocalApplicationDataPath = _origUserLocalApplicationDataPath;

            DeleteDirectory(UNPACK_DESTINATION);
        }

        // -----------------------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------------------

        private NewsComponentsConfiguration BuildCfg()
        {
            var cfg = NewsComponentsConfiguration.Default as NewsComponentsConfiguration;
            cfg.SearchIndexBehavior = SearchIndexBehavior.NoIndexing; // keep the NoIndexing construction dodge
            cfg.UserLocalApplicationDataPath = UNPACK_DESTINATION;
            return cfg;
        }

        /// <summary>A handler with no feed list loaded; feeds are added programmatically.</summary>
        private BanditFeedSource CreateEmptyHandler()
        {
            return new BanditFeedSource(BuildCfg(),
                new SubscriptionLocation(Path.Combine(UNPACK_DESTINATION, "slice4-feeds.xml")));
        }

        /// <summary>
        /// Adds a feed to BOTH feedsTable and itemsTable (via a non-null <see cref="FeedInfo"/>), so the
        /// delete/restore/save sites that index into <c>itemsTable[feed.link]</c> resolve without throwing.
        /// </summary>
        private static INewsFeed AddSubscribedFeed(FeedSource handler, string url, params INewsItem[] items)
        {
            var feed = new NewsFeed { link = url, title = "Title for " + url, category = null };
            var feedInfo = new FeedInfo(url, url, items);
            handler.AddFeed(feed, feedInfo);
            return feed;
        }

        private static NewsItem MakeItem(INewsFeed feed, string suffix)
        {
            // Mirrors the 8-arg ctor used by FeedListSaveTests: (feed, title, link, content, date, subject, id, parentId).
            return new NewsItem(feed, "Item " + suffix, feed.link + "#" + suffix, "body",
                DateTime.Now, null, "slice4-" + suffix, null);
        }

        // ===================================================================================
        // Site a: DeleteAllItemsInFeed(INewsFeed) -> one IndexRemove(feedId)
        // ===================================================================================

        [Test]
        public void Site_a_DeleteAllItemsInFeed_RoutesOneIndexRemoveForTheFeedId()
        {
            BanditFeedSource handler = CreateEmptyHandler();
            // Subscribed feed present in BOTH tables; an empty (but non-null) items list is enough to
            // reach site a — DeleteAllItemsInFeed clears the (empty) list, then routes IndexRemove(feed.id).
            INewsFeed feed = AddSubscribedFeed(handler, "http://example.com/a-delete-all.xml");

            string feedId = feed.id;
            var recorder = new RecordingSearchIndexSink();
            handler.SearchIndexSink = recorder;

            handler.DeleteAllItemsInFeed(feed);

            Assert.AreEqual(1, recorder.Calls.Count, "DeleteAllItemsInFeed must route exactly one sink call.");
            Assert.AreEqual("IndexRemove(feedId)", recorder.Calls[0].Method);
            Assert.AreEqual(feedId, recorder.Calls[0].Arg, "The removed feed-id must be feed.id.");
        }

        // ===================================================================================
        // Site b: RestoreDeletedItem(INewsItem) -> one IndexAdd(item)
        // ===================================================================================

        [Test]
        public void Site_b_RestoreDeletedItem_Single_RoutesOneIndexAddForThatItem()
        {
            BanditFeedSource handler = CreateEmptyHandler();
            INewsFeed feed = AddSubscribedFeed(handler, "http://example.com/b-restore-one.xml");
            NewsItem item = MakeItem(feed, "b1");

            var recorder = new RecordingSearchIndexSink();
            handler.SearchIndexSink = recorder;

            handler.RestoreDeletedItem(item);

            Assert.AreEqual(1, recorder.Calls.Count, "RestoreDeletedItem(item) must route exactly one sink call.");
            Assert.AreEqual("IndexAdd(item)", recorder.Calls[0].Method);
            Assert.AreSame(item, recorder.Calls[0].Arg, "The indexed item must be the restored item itself.");
        }

        // ===================================================================================
        // Site c: RestoreDeletedItem(IList<INewsItem>) -> per-item IndexAdd(item) AND IndexAdd(list)
        // (the double-index nuance, pinned exactly as the original code does it)
        // ===================================================================================

        [Test]
        public void Site_c_RestoreDeletedItem_List_DoubleIndexes_PerItemThenWholeList()
        {
            BanditFeedSource handler = CreateEmptyHandler();
            INewsFeed feed = AddSubscribedFeed(handler, "http://example.com/c-restore-list.xml");
            NewsItem item1 = MakeItem(feed, "c1");
            NewsItem item2 = MakeItem(feed, "c2");
            var list = new List<INewsItem> { item1, item2 };

            var recorder = new RecordingSearchIndexSink();
            handler.SearchIndexSink = recorder;

            handler.RestoreDeletedItem(list);

            // The list overload calls RestoreDeletedItem(item) per item (each -> IndexAdd(item)) and then,
            // after the loop, IndexAdd(deletedItems). For a 2-item list that is 3 calls in this exact order.
            Assert.AreEqual(3, recorder.Calls.Count,
                "The list overload double-indexes: one IndexAdd(item) per item PLUS one IndexAdd(list).");

            Assert.AreEqual("IndexAdd(item)", recorder.Calls[0].Method);
            Assert.AreSame(item1, recorder.Calls[0].Arg);

            Assert.AreEqual("IndexAdd(item)", recorder.Calls[1].Method);
            Assert.AreSame(item2, recorder.Calls[1].Arg);

            Assert.AreEqual("IndexAdd(list)", recorder.Calls[2].Method);
            Assert.AreSame(list, recorder.Calls[2].Arg, "The trailing whole-list IndexAdd must receive the original list.");
        }

        // ===================================================================================
        // Negative pin: DeleteItem(INewsItem) -> ZERO sink calls
        // (de-indexing of a single deleted item is lazy, done later by SaveFeed, not by DeleteItem)
        // ===================================================================================

        [Test]
        public void DeleteItem_MakesNoSinkCalls()
        {
            BanditFeedSource handler = CreateEmptyHandler();
            INewsFeed feed = AddSubscribedFeed(handler, "http://example.com/delete-item.xml");
            NewsItem item = MakeItem(feed, "del1");
            // Put the item into the live items list so DeleteItem reaches its body (AddDeletedStory + remove).
            handler.RestoreDeletedItem(item);

            var recorder = new RecordingSearchIndexSink();
            handler.SearchIndexSink = recorder; // inject AFTER the restore so only DeleteItem is observed

            handler.DeleteItem(item);

            Assert.AreEqual(0, recorder.Calls.Count,
                "DeleteItem only updates itemsTable + AddDeletedStory; it must NOT touch the search index.");
        }

        // ===================================================================================
        // Site f: SaveFeed purge loop -> IndexRemove(item) for expired/deleted items
        // (reached via ApplyFeedModifications, as BanditFeedSourceTests.MarkAllCachedItemsAsRead... does)
        // ===================================================================================

        [Test]
        public void Site_f_SaveFeedPurge_RoutesIndexRemoveForDeletedItem()
        {
            BanditFeedSource handler = CreateEmptyHandler();
            handler.MaxItemAge = TimeSpan.FromDays(30); // a real (non-MinValue) age so SaveFeed enters the purge loop

            const string url = "http://example.com/f-savefeed-purge.xml";
            NewsFeed feed = new NewsFeed { link = url, title = "Purge Feed", category = null };
            NewsItem item = MakeItem(feed, "f1");
            var feedInfo = new FeedInfo(url, url, new[] { (INewsItem)item });
            handler.AddFeed(feed, feedInfo); // both tables

            // Mark the item deleted WITHOUT removing it from the list, so SaveFeed's purge condition
            // (deletedstories.Contains(item.Id)) matches and the item is removed + de-indexed there.
            feed.AddDeletedStory(item.Id);

            var recorder = new RecordingSearchIndexSink();
            handler.SearchIndexSink = recorder;

            handler.ApplyFeedModifications(url); // -> SaveFeed(feed) -> purge loop -> IndexRemove(item)

            Assert.AreEqual(1, recorder.Calls.Count, "SaveFeed must route exactly one IndexRemove for the purged item.");
            Assert.AreEqual("IndexRemove(item)", recorder.Calls[0].Method);
            Assert.AreSame(item, recorder.Calls[0].Arg, "The de-indexed item must be the purged item.");
        }

        // ===================================================================================
        // Site g: DeleteFeed(string) -> one IndexRemove(feedId)
        // ===================================================================================

        [Test]
        public void Site_g_DeleteFeed_RoutesOneIndexRemoveForTheFeedId()
        {
            BanditFeedSource handler = CreateEmptyHandler();
            const string url = "http://example.com/g-delete-feed.xml";
            INewsFeed feed = AddSubscribedFeed(handler, url);
            string feedId = feed.id;

            var recorder = new RecordingSearchIndexSink();
            handler.SearchIndexSink = recorder;

            handler.DeleteFeed(url);

            Assert.AreEqual(1, recorder.Calls.Count, "DeleteFeed must route exactly one sink call.");
            Assert.AreEqual("IndexRemove(feedId)", recorder.Calls[0].Method);
            Assert.AreEqual(feedId, recorder.Calls[0].Arg);
        }

        // ===================================================================================
        // Site h: DeleteAllFeedsAndCategories(false) -> IndexRemove(feedId) per feed (direct site);
        //         DeleteAllFeedsAndCategories(true)  -> routes through DeleteFeed (still IndexRemove).
        // ===================================================================================

        [Test]
        public void Site_h_DeleteAllFeedsAndCategories_False_RoutesIndexRemovePerFeed()
        {
            BanditFeedSource handler = CreateEmptyHandler();
            INewsFeed f1 = AddSubscribedFeed(handler, "http://example.com/h-1.xml");
            INewsFeed f2 = AddSubscribedFeed(handler, "http://example.com/h-2.xml");
            var expectedIds = new HashSet<string> { f1.id, f2.id };

            var recorder = new RecordingSearchIndexSink();
            handler.SearchIndexSink = recorder;

            handler.DeleteAllFeedsAndCategories(false);

            Assert.AreEqual(2, recorder.Calls.Count, "The non-source branch removes each feed from the index.");
            var seen = new HashSet<string>();
            foreach (var call in recorder.Calls)
            {
                Assert.AreEqual("IndexRemove(feedId)", call.Method);
                seen.Add((string)call.Arg);
            }
            Assert.IsTrue(seen.SetEquals(expectedIds), "Each subscribed feed's id must be de-indexed exactly once.");
        }

        [Test]
        public void Site_h_DeleteAllFeedsAndCategories_True_RoutesThroughDeleteFeed_StillIndexRemovePerFeed()
        {
            BanditFeedSource handler = CreateEmptyHandler();
            INewsFeed f1 = AddSubscribedFeed(handler, "http://example.com/h-true-1.xml");
            INewsFeed f2 = AddSubscribedFeed(handler, "http://example.com/h-true-2.xml");
            var expectedIds = new HashSet<string> { f1.id, f2.id };

            var recorder = new RecordingSearchIndexSink();
            handler.SearchIndexSink = recorder;

            handler.DeleteAllFeedsAndCategories(true); // delegates to DeleteFeed(url) per feed (site g)

            Assert.AreEqual(2, recorder.Calls.Count);
            var seen = new HashSet<string>();
            foreach (var call in recorder.Calls)
            {
                Assert.AreEqual("IndexRemove(feedId)", call.Method);
                seen.Add((string)call.Arg);
            }
            Assert.IsTrue(seen.SetEquals(expectedIds),
                "Even via the delete-from-source branch (DeleteFeed), each feed's id is de-indexed once.");
        }

        // ===================================================================================
        // Site i: BanditFeedSource.LoadFeedlist (subclass) -> one CheckIndex()
        // ===================================================================================

        [Test]
        public void Site_i_LoadFeedlist_RoutesOneCheckIndex()
        {
            var handler = new BanditFeedSource(BuildCfg(),
                new SubscriptionLocation(WEBROOT_PATH + @"\NewsHandlerTestFiles\FeedList03Feeds.xml"));

            var recorder = new RecordingSearchIndexSink();
            handler.SearchIndexSink = recorder; // inject BEFORE the load so the trailing CheckIndex is observed

            handler.LoadFeedlist();
            Assert.IsTrue(handler.FeedsListOK, "FeedList03Feeds.xml should load cleanly.");

            Assert.AreEqual(1, recorder.Calls.Count, "Loading the feed list must route exactly one sink call.");
            Assert.AreEqual("CheckIndex", recorder.Calls[0].Method,
                "The subclass load path ends by routing CheckIndex() through the sink.");
        }
    }

    /// <summary>
    /// A recording <see cref="ISearchIndexSink"/> test double. Records each routed call as a
    /// (method-label, argument) pair so the interaction tests can assert the exact call sequence and
    /// arguments produced by the routed FeedSource sites — without ever touching the real (sealed,
    /// NoIndexing) Lucene singleton.
    /// </summary>
    internal sealed class RecordingSearchIndexSink : ISearchIndexSink
    {
        /// <summary>The ordered list of recorded calls: (method label, argument).</summary>
        public readonly List<(string Method, object Arg)> Calls = new List<(string, object)>();

        public void IndexAdd(INewsItem item)
        {
            Calls.Add(("IndexAdd(item)", item));
        }

        public void IndexAdd(IList<INewsItem> items)
        {
            Calls.Add(("IndexAdd(list)", items));
        }

        public void IndexRemove(INewsItem item)
        {
            Calls.Add(("IndexRemove(item)", item));
        }

        public void IndexRemove(string feedId)
        {
            Calls.Add(("IndexRemove(feedId)", feedId));
        }

        public void Flush()
        {
            Calls.Add(("Flush", null));
        }

        public void CheckIndex()
        {
            Calls.Add(("CheckIndex", null));
        }
    }
}
