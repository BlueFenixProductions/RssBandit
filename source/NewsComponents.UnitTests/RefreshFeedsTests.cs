using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewsComponents.Feed;
using NewsComponents.Net;
using NUnit.Framework;
using RssBandit.Common;
using RssBandit.UnitTests;

namespace NewsComponents.UnitTests
{
    /// <summary>
    /// End-to-end characterization harness for <see cref="FeedSource.RefreshFeeds(bool)"/>: it drives a
    /// REAL force-refresh of a single WireMock-served feed across HTTP, awaits
    /// <see cref="FeedSource.OnAllAsyncRequestsCompleted"/>, and asserts the observable end-state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This fills the gap documented in <see cref="SearchIndexSinkTests"/>: Slice 4 routed every
    /// search-index call behind <see cref="ISearchIndexSink"/>, but two of those sites live on the async
    /// refresh hot path and could only be verified by inspection because no test in this suite drove a
    /// FeedSource refresh to completion:
    /// <list type="bullet">
    ///   <item>site d — <c>OnRequestComplete</c> -&gt; <c>SearchIndexSink.IndexAdd(newReceivedItems)</c>.</item>
    ///   <item>site e — <c>OnAllRequestsComplete</c> -&gt; <c>SearchIndexSink.Flush()</c>.</item>
    /// </list>
    /// With a real refresh harness in place we can inject a <see cref="RecordingSearchIndexSink"/> (the
    /// same double used by <see cref="SearchIndexSinkTests"/>) BEFORE the refresh and assert those two
    /// sites actually fire on the live path — which Slice 4 could not do.
    /// </para>
    /// <para>
    /// Determinism: a force download of a WireMock-served feed genuinely queues exactly one request, so
    /// completion arrives via the <c>AsyncWebRequest</c> callback path (which runs site e's
    /// <c>Flush()</c>). The tests still wait on the completion event with a generous (30s) timeout and
    /// assert on observable END-STATE — never on wall-clock timing or strict call ordering beyond
    /// "these happened, in d-before-e order". If the wait times out the test FAILS loudly, surfacing any
    /// recorded <c>OnUpdateFeedException</c> rather than passing silently. The injected recorder replaces
    /// the sink so the real (sealed, <c>NoIndexing</c>) Lucene singleton is never touched.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class RefreshFeedsTests : WebServerTestFixture
    {
        internal const string BASE_URL = "http://127.0.0.1:8081/NewsHandlerTestFiles/";

        /// <summary>The single feed the fixture refreshes (served by WireMock from the unpacked web root).</summary>
        private const string FEED_URL = BASE_URL + "LocalTestFeed.xml";

        /// <summary>Generous wait for the async refresh to cross the wire and complete (end-state, not timing).</summary>
        private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(30);

        // The shared NewsComponentsConfiguration.Default is process-wide; save/restore what we touch so we
        // do not perturb the NoIndexing default config for other fixtures (mirrors SearchIndexSinkTests).
        private string _origUserLocalApplicationDataPath;
        private SearchIndexBehavior _origSearchIndexBehavior;

        // =====================================================================================
        // Behavior 1: happy-path refresh fetches + lands items.
        // =====================================================================================

        [Test]
        public void Refresh_HappyPath_FetchesFeedAndLandsItems()
        {
            BanditFeedSource handler = CreateHandlerWithSingleFeed();

            var completed = new ManualResetEventSlim(false);
            var updatedFeeds = new List<FeedSource.UpdatedFeedEventArgs>();
            Exception updateException = null;

            handler.OnAllAsyncRequestsCompleted += (s, e) => completed.Set();
            handler.OnUpdatedFeed += (s, e) => { lock (updatedFeeds) updatedFeeds.Add(e); };
            handler.OnUpdateFeedException += (s, e) => updateException = e.ExceptionThrown;

            // force => crosses the wire (a cached/false call would just return cached and never download)
            handler.RefreshFeeds(true);

            AwaitCompletionOrFail(completed, updateException);

            // ---- observable end-state ----
            Assert.IsNull(updateException,
                "The refresh must not record an OnUpdateFeedException for a well-formed WireMock feed.");

            IList<INewsItem> items = handler.GetCachedItemsForFeed(FEED_URL);
            Assert.IsNotNull(items, "After a successful refresh the feed's items must be present in itemsTable.");
            Assert.AreEqual(2, items.Count, "LocalTestFeed.xml contains exactly two items.");

            INewsFeed feed = handler.GetFeeds()[FEED_URL];
            Assert.IsFalse(feed.causedException, "A successful download must clear causedException.");
            Assert.IsTrue(feed.lastretrievedSpecified, "A successful download must stamp lastretrievedSpecified.");

            FeedSource.UpdatedFeedEventArgs updated;
            lock (updatedFeeds)
            {
                Assert.AreEqual(1, updatedFeeds.Count, "Exactly one feed was refreshed, so OnUpdatedFeed must fire once.");
                updated = updatedFeeds[0];
            }
            Assert.AreEqual(RequestResult.OK, updated.UpdateState,
                "A force download that crosses the wire and parses must report RequestResult.OK.");
            Assert.IsTrue(updated.FirstSuccessfulDownload,
                "The feed was not in itemsTable beforehand, so this is its first successful download.");
            Assert.AreEqual(new Uri(FEED_URL).CanonicalizedUri(), updated.UpdatedFeedUri.CanonicalizedUri(),
                "OnUpdatedFeed must report the refreshed feed's uri.");
        }

        // =====================================================================================
        // Behavior 2 (the high-value pin): sites d + e on the live refresh path.
        //   site d: OnRequestComplete    -> SearchIndexSink.IndexAdd(newReceivedItems)
        //   site e: OnAllRequestsComplete -> SearchIndexSink.Flush()
        // =====================================================================================

        [Test]
        public void Refresh_RoutesIndexAddThenFlush_PinsSlice4HotPathSites()
        {
            BanditFeedSource handler = CreateHandlerWithSingleFeed();

            // Inject the recorder AFTER LoadFeedlist (CreateHandlerWithSingleFeed already loaded), so the
            // load-path CheckIndex (site i) is NOT recorded and only the refresh-path calls are observed.
            var recorder = new RecordingSearchIndexSink();
            handler.SearchIndexSink = recorder;

            var completed = new ManualResetEventSlim(false);
            Exception updateException = null;
            handler.OnAllAsyncRequestsCompleted += (s, e) => completed.Set();
            handler.OnUpdateFeedException += (s, e) => updateException = e.ExceptionThrown;

            handler.RefreshFeeds(true);

            AwaitCompletionOrFail(completed, updateException);

            Assert.IsNull(updateException,
                "The refresh must not fault before reaching the index-add / flush sites.");

            // The completion event is raised AFTER Flush() (RaiseOnAllAsyncRequestsCompleted runs at the end
            // of OnAllRequestsComplete), so by the time Wait() returns both sites have executed. The Set/Wait
            // pair establishes the happens-before that makes reading recorder.Calls here safe.
            List<(string Method, object Arg)> calls = recorder.Calls;

            int indexAddListIdx = calls.FindIndex(c => c.Method == "IndexAdd(list)");
            int flushIdx = calls.FindIndex(c => c.Method == "Flush");

            Assert.GreaterOrEqual(indexAddListIdx, 0,
                "site d: OnRequestComplete must route IndexAdd(newReceivedItems) for the newly-received items.");
            Assert.GreaterOrEqual(flushIdx, 0,
                "site e: OnAllRequestsComplete must route Flush().");
            Assert.Less(indexAddListIdx, flushIdx,
                "site d (IndexAdd) must run before site e (Flush): items are indexed per-request, then flushed once at the end.");

            // site d carried the newly-received items (the same two items now cached for the feed).
            var indexedItems = (IList<INewsItem>)calls[indexAddListIdx].Arg;
            Assert.IsNotNull(indexedItems, "IndexAdd(list) must receive the newReceivedItems list, not null.");
            Assert.AreEqual(2, indexedItems.Count, "Both received items of LocalTestFeed.xml must be handed to IndexAdd.");

            // No de-indexing should happen on a clean first download (large MaxItemAge, no deleted stories),
            // so the recorder must NOT have observed any IndexRemove — keeps the d/e pin unambiguous.
            Assert.IsFalse(calls.Any(c => c.Method.StartsWith("IndexRemove")),
                "A clean first download must not route any IndexRemove; only IndexAdd (d) then Flush (e).");
        }

        // -------------------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------------------

        /// <summary>
        /// Builds a <see cref="BanditFeedSource"/> over the one-feed <c>LocalTestFeedList.xml</c> fixture
        /// (whose single feed url points at the WireMock server) and loads it. NoIndexing is preserved; the
        /// cache root is redirected to a writable temp dir so SaveFeed works and the feed is NOT found in
        /// cache (forcing the first-successful-download path that yields newReceivedItems).
        /// </summary>
        private BanditFeedSource CreateHandlerWithSingleFeed()
        {
            var cfg = NewsComponentsConfiguration.Default as NewsComponentsConfiguration;
            cfg.SearchIndexBehavior = SearchIndexBehavior.NoIndexing; // never touch real Lucene
            cfg.UserLocalApplicationDataPath = UNPACK_DESTINATION;    // writable cache root, no pre-seeded cache file

            var handler = new BanditFeedSource(cfg,
                new SubscriptionLocation(WEBROOT_PATH + @"\NewsHandlerTestFiles\LocalTestFeedList.xml"));
            // Keep the 2004-dated fixture items from being purged by age, so all of them count as received.
            handler.MaxItemAge = TimeSpan.MaxValue.Subtract(TimeSpan.FromDays(1));

            handler.LoadFeedlist();
            Assert.IsTrue(handler.FeedsListOK, "LocalTestFeedList.xml should load cleanly.");
            Assert.IsTrue(handler.IsSubscribed(FEED_URL), "The fixture must contain exactly the WireMock-served feed.");
            return handler;
        }

        /// <summary>
        /// Waits for the refresh to complete; on timeout fails with a clear, debuggable message that
        /// surfaces any recorded refresh exception (never silently passes).
        /// </summary>
        private static void AwaitCompletionOrFail(ManualResetEventSlim completed, Exception recordedException)
        {
            if (!completed.Wait(CompletionTimeout))
            {
                Assert.Fail($"RefreshFeeds did not raise OnAllAsyncRequestsCompleted within " +
                            $"{CompletionTimeout.TotalSeconds:0}s. Recorded OnUpdateFeedException: " +
                            $"{(recordedException != null ? recordedException.ToString() : "(none)")}");
            }
        }

        // -------------------------------------------------------------------------------------
        // setup / teardown (mirrors the WebServerTestFixture pattern used by BanditFeedSourceTests)
        // -------------------------------------------------------------------------------------

        [SetUp]
        protected override void SetUp()
        {
            DeleteDirectory(UNPACK_DESTINATION);
            UnpackResourceDirectory("WebRoot.NewsHandlerTestFiles");

            var cfg = NewsComponentsConfiguration.Default as NewsComponentsConfiguration;
            _origUserLocalApplicationDataPath = cfg.UserLocalApplicationDataPath;
            _origSearchIndexBehavior = cfg.SearchIndexBehavior;

            base.SetUp(); // starts the WireMock server
        }

        [TearDown]
        protected override void TearDown()
        {
            base.TearDown(); // stops the WireMock server

            // restore the shared default config so sibling fixtures are unaffected
            var cfg = NewsComponentsConfiguration.Default as NewsComponentsConfiguration;
            cfg.UserLocalApplicationDataPath = _origUserLocalApplicationDataPath;
            cfg.SearchIndexBehavior = _origSearchIndexBehavior;

            DeleteDirectory(UNPACK_DESTINATION);
        }
    }
}
