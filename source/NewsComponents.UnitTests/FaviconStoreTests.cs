using System;
using System.IO;
using NewsComponents.Feed;
using NUnit.Framework;
using RssBandit.UnitTests;

namespace NewsComponents.UnitTests
{
    /// <summary>
    /// Characterization tests for <see cref="FeedSource"/>'s favicon storage methods
    /// (#region favicon handling, FeedSource.cs ~2712-2854, Slice 2 Task 1).
    /// </summary>
    /// <remarks>
    /// These tests pin the CURRENT, observable behavior of the favicon storage cluster against the
    /// unchanged monolith. They are the safety net for the upcoming IFaviconStore extraction (Task 2)
    /// and must stay green against the current production code without modification.
    ///
    /// Real round-trips through a live <see cref="NewsComponents.Storage.FileStorageDataService"/>
    /// (UserCacheDataService) are exercised by pointing cfg.UserLocalApplicationDataPath to an
    /// isolated per-fixture temp directory. The service is created lazily by BanditFeedSource on
    /// first use and initialises its Cache subdirectory under that path automatically.
    ///
    /// Quirks are flagged with QUIRK comments and must NOT be "fixed" here; they are pinned as-is.
    ///   Q1 - String overloads throw ArgumentException (NOT ArgumentNullException) with the literal
    ///        message "message" on null/empty/whitespace feedUrl.
    ///   Q2 - SetFaviconForFeed is a no-op (stores nothing, does not set feed.favicon) when
    ///        contentId is null/empty OR imageData is null/empty.
    ///   Q3 - RemoveFaviconFromFeed deletes the cached blob but does NOT clear feed.favicon, so
    ///        FeedHasFavicon still returns true after a Remove.
    /// </remarks>
    [TestFixture]
    public class FaviconStoreTests : BaseTestFixture
    {
        // A URL we add to feedsTable in CreateHandlerWithFeed().
        private const string KnownFeedUrl = "http://example.com/favicon-characterization.xml";

        // Snapshot of the singleton so we can restore it after each test.
        private string _origUserLocalApplicationDataPath;

        [SetUp]
        public void SetUp()
        {
            // Clear any leftover files from a previous test run.
            DeleteDirectory(UNPACK_DESTINATION);

            // Snapshot the shared singleton's path so TearDown can restore it.
            var cfg = NewsComponentsConfiguration.Default as NewsComponentsConfiguration;
            _origUserLocalApplicationDataPath = cfg.UserLocalApplicationDataPath;
        }

        [TearDown]
        public void TearDown()
        {
            // Remove test files written by the real FileStorageDataService.
            DeleteDirectory(UNPACK_DESTINATION);

            // Restore the shared singleton to the original state so other tests are not affected.
            var cfg = NewsComponentsConfiguration.Default as NewsComponentsConfiguration;
            cfg.UserLocalApplicationDataPath = _origUserLocalApplicationDataPath;
        }

        // -----------------------------------------------------------------------
        // Internal helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns a <see cref="NewsComponentsConfiguration"/> whose UserLocalApplicationDataPath
        /// points to the per-fixture temp directory. The DataServiceFactory will create
        /// {UNPACK_DESTINATION}\Cache on first access (i.e. the first favicon operation),
        /// giving real file-backed binary-content round-trips without touching production paths.
        /// </summary>
        private NewsComponentsConfiguration BuildCfg()
        {
            var cfg = NewsComponentsConfiguration.Default as NewsComponentsConfiguration;
            cfg.SearchIndexBehavior = SearchIndexBehavior.NoIndexing;
            cfg.UserLocalApplicationDataPath = UNPACK_DESTINATION;
            return cfg;
        }

        /// <summary>
        /// Creates a <see cref="BanditFeedSource"/> (no feed list loaded) and injects a single
        /// feed directly into feedsTable via AddFeed so the favicon methods have a known URL to
        /// look up without a live network or web server.
        /// </summary>
        private FeedSource CreateHandlerWithFeed(out INewsFeed feed)
        {
            var handler = new BanditFeedSource(BuildCfg(),
                // SubscriptionLocation is required by the constructor but LoadFeedlist() is never
                // called — feeds are added programmatically, so any local path is fine here.
                new SubscriptionLocation(Path.Combine(UNPACK_DESTINATION, "feeds.xml")));

            feed = handler.AddFeed(new NewsFeed
            {
                link = KnownFeedUrl,
                title = "Favicon Characterisation Test Feed",
                favicon = null   // explicitly bare — no favicon set yet
            });
            return handler;
        }

        // -----------------------------------------------------------------------
        // Group 1: FeedHasFavicon
        // -----------------------------------------------------------------------

        [Test]
        public void Group1_FeedHasFavicon_INewsFeedOverload_ThrowsArgumentNullException_WhenFeedIsNull()
        {
            FeedSource handler = CreateHandlerWithFeed(out _);

            Assert.Throws<ArgumentNullException>(() => handler.FeedHasFavicon((INewsFeed)null));
        }

        [Test]
        public void Group1_FeedHasFavicon_StringOverload_ThrowsArgumentException_WhenUrlIsNull()
        {
            // QUIRK Q1: ArgumentException (not ArgumentNullException) on null url.
            FeedSource handler = CreateHandlerWithFeed(out _);

            Assert.Throws<ArgumentException>(() => handler.FeedHasFavicon((string)null));
        }

        [Test]
        public void Group1_FeedHasFavicon_StringOverload_ThrowsArgumentException_WhenUrlIsEmpty()
        {
            // QUIRK Q1: ArgumentException on empty string.
            FeedSource handler = CreateHandlerWithFeed(out _);

            Assert.Throws<ArgumentException>(() => handler.FeedHasFavicon(""));
        }

        [Test]
        public void Group1_FeedHasFavicon_StringOverload_ThrowsArgumentException_WhenUrlIsWhitespace()
        {
            // QUIRK Q1: ArgumentException on whitespace.
            FeedSource handler = CreateHandlerWithFeed(out _);

            Assert.Throws<ArgumentException>(() => handler.FeedHasFavicon("   "));
        }

        [Test]
        public void Group1_FeedHasFavicon_StringOverload_ReturnsFalse_WhenUrlNotInFeedsTable()
        {
            FeedSource handler = CreateHandlerWithFeed(out _);

            bool result = handler.FeedHasFavicon("http://not-subscribed.example.com/nope.xml");

            Assert.IsFalse(result,
                "A url not in feedsTable must return false (not throw).");
        }

        [Test]
        public void Group1_FeedHasFavicon_INewsFeedOverload_ReturnsFalse_WhenFaviconIsNull()
        {
            FeedSource handler = CreateHandlerWithFeed(out INewsFeed feed);
            feed.favicon = null;

            Assert.IsFalse(handler.FeedHasFavicon(feed));
        }

        [Test]
        public void Group1_FeedHasFavicon_INewsFeedOverload_ReturnsFalse_WhenFaviconIsEmpty()
        {
            FeedSource handler = CreateHandlerWithFeed(out INewsFeed feed);
            feed.favicon = "";

            Assert.IsFalse(handler.FeedHasFavicon(feed));
        }

        [Test]
        public void Group1_FeedHasFavicon_INewsFeedOverload_ReturnsTrue_WhenFaviconIsNonEmpty()
        {
            FeedSource handler = CreateHandlerWithFeed(out INewsFeed feed);
            // Assign a content-id without going through Set (tests the direct property path).
            feed.favicon = "arbitrary-content-id";

            Assert.IsTrue(handler.FeedHasFavicon(feed));
        }

        [Test]
        public void Group1_FeedHasFavicon_StringOverload_ReturnsFalse_WhenFaviconIsNull()
        {
            FeedSource handler = CreateHandlerWithFeed(out INewsFeed feed);
            feed.favicon = null;

            Assert.IsFalse(handler.FeedHasFavicon(KnownFeedUrl));
        }

        [Test]
        public void Group1_FeedHasFavicon_StringOverload_ReturnsTrue_WhenFaviconIsNonEmpty()
        {
            FeedSource handler = CreateHandlerWithFeed(out INewsFeed feed);
            feed.favicon = "arbitrary-content-id";

            Assert.IsTrue(handler.FeedHasFavicon(KnownFeedUrl));
        }

        // -----------------------------------------------------------------------
        // Group 2: The ArgumentException("message") quirk — string overloads
        // Covers GetFaviconForFeed and SetFaviconForFeed in addition to FeedHasFavicon
        // (the FeedHasFavicon string-overload cases are already in Group 1 above).
        // -----------------------------------------------------------------------

        [Test]
        public void Group2_GetFaviconForFeed_StringOverload_ThrowsArgumentException_WhenUrlIsNull()
        {
            // QUIRK Q1: ArgumentException (not ArgumentNullException) on null.
            FeedSource handler = CreateHandlerWithFeed(out _);

            Assert.Throws<ArgumentException>(() => handler.GetFaviconForFeed((string)null));
        }

        [Test]
        public void Group2_GetFaviconForFeed_StringOverload_ThrowsArgumentException_WhenUrlIsEmpty()
        {
            FeedSource handler = CreateHandlerWithFeed(out _);

            Assert.Throws<ArgumentException>(() => handler.GetFaviconForFeed(""));
        }

        [Test]
        public void Group2_GetFaviconForFeed_StringOverload_ThrowsArgumentException_WhenUrlIsWhitespace()
        {
            FeedSource handler = CreateHandlerWithFeed(out _);

            Assert.Throws<ArgumentException>(() => handler.GetFaviconForFeed(" "));
        }

        [Test]
        public void Group2_SetFaviconForFeed_StringOverload_ThrowsArgumentException_WhenUrlIsNull()
        {
            // QUIRK Q1: ArgumentException (not ArgumentNullException) on null.
            FeedSource handler = CreateHandlerWithFeed(out _);
            var data = new byte[] { 1, 2, 3, 4 };

            Assert.Throws<ArgumentException>(() =>
                handler.SetFaviconForFeed((string)null, "favicon.ico", data));
        }

        [Test]
        public void Group2_SetFaviconForFeed_StringOverload_ThrowsArgumentException_WhenUrlIsEmpty()
        {
            FeedSource handler = CreateHandlerWithFeed(out _);
            var data = new byte[] { 1, 2, 3, 4 };

            Assert.Throws<ArgumentException>(() =>
                handler.SetFaviconForFeed("", "favicon.ico", data));
        }

        [Test]
        public void Group2_SetFaviconForFeed_StringOverload_ThrowsArgumentException_WhenUrlIsWhitespace()
        {
            FeedSource handler = CreateHandlerWithFeed(out _);
            var data = new byte[] { 1, 2, 3, 4 };

            Assert.Throws<ArgumentException>(() =>
                handler.SetFaviconForFeed("   ", "favicon.ico", data));
        }

        // -----------------------------------------------------------------------
        // Group 3: GetFaviconForFeed
        // -----------------------------------------------------------------------

        [Test]
        public void Group3_GetFaviconForFeed_INewsFeedOverload_ThrowsArgumentNullException_WhenFeedIsNull()
        {
            FeedSource handler = CreateHandlerWithFeed(out _);

            Assert.Throws<ArgumentNullException>(() => handler.GetFaviconForFeed((INewsFeed)null));
        }

        [Test]
        public void Group3_GetFaviconForFeed_ReturnsNull_WhenFeedHasNoFavicon()
        {
            FeedSource handler = CreateHandlerWithFeed(out INewsFeed feed);
            feed.favicon = null;

            Assert.IsNull(handler.GetFaviconForFeed(feed),
                "GetFaviconForFeed must return null when feed.favicon is null.");
        }

        [Test]
        public void Group3_GetFaviconForFeed_StringOverload_ReturnsNull_WhenUrlNotInFeedsTable()
        {
            FeedSource handler = CreateHandlerWithFeed(out _);

            byte[] result = handler.GetFaviconForFeed("http://not-subscribed.example.com/nope.xml");

            Assert.IsNull(result,
                "Url not in feedsTable must return null (not throw).");
        }

        [Test]
        public void Group3_GetFaviconForFeed_RoundTrip_ViaINewsFeedOverload()
        {
            // Exercises the real FileStorageDataService: Set stores to disk, Get reads from disk.
            FeedSource handler = CreateHandlerWithFeed(out INewsFeed feed);
            var imageData = new byte[] { 1, 2, 3, 4 };
            const string contentId = "chars-favicon-roundtrip-001.ico";

            handler.SetFaviconForFeed(feed, contentId, imageData);
            byte[] retrieved = handler.GetFaviconForFeed(feed);

            Assert.IsNotNull(retrieved, "GetFaviconForFeed should return the stored bytes.");
            CollectionAssert.AreEqual(imageData, retrieved,
                "Round-tripped bytes must match exactly.");
        }

        [Test]
        public void Group3_GetFaviconForFeed_RoundTrip_ViaStringOverload()
        {
            FeedSource handler = CreateHandlerWithFeed(out _);
            var imageData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // plausible JPEG header
            const string contentId = "chars-favicon-roundtrip-002.ico";

            handler.SetFaviconForFeed(KnownFeedUrl, contentId, imageData);
            byte[] retrieved = handler.GetFaviconForFeed(KnownFeedUrl);

            Assert.IsNotNull(retrieved);
            CollectionAssert.AreEqual(imageData, retrieved);
        }

        // -----------------------------------------------------------------------
        // Group 4: SetFaviconForFeed round-trip + guards
        // -----------------------------------------------------------------------

        [Test]
        public void Group4_SetFaviconForFeed_INewsFeedOverload_ThrowsArgumentNullException_WhenFeedIsNull()
        {
            FeedSource handler = CreateHandlerWithFeed(out _);

            Assert.Throws<ArgumentNullException>(() =>
                handler.SetFaviconForFeed((INewsFeed)null, "id.ico", new byte[] { 1 }));
        }

        [Test]
        public void Group4_SetFaviconForFeed_SetsFaviconProperty_AndStoresBlob()
        {
            // The happy path: contentId + non-empty imageData -> feed.favicon set, blob stored.
            FeedSource handler = CreateHandlerWithFeed(out INewsFeed feed);
            var imageData = new byte[] { 1, 2, 3, 4 };
            const string contentId = "chars-favicon-set-001.ico";

            handler.SetFaviconForFeed(feed, contentId, imageData);

            Assert.AreEqual(contentId, feed.favicon,
                "SetFaviconForFeed must set feed.favicon to contentId.");
            Assert.IsTrue(handler.FeedHasFavicon(feed));
            CollectionAssert.AreEqual(imageData, handler.GetFaviconForFeed(feed));
        }

        [Test]
        public void Group4_SetFaviconForFeed_IsNoOp_WhenContentIdIsNull()
        {
            // QUIRK Q2: null contentId means nothing is stored and feed.favicon stays null.
            FeedSource handler = CreateHandlerWithFeed(out INewsFeed feed);
            var imageData = new byte[] { 1, 2, 3, 4 };

            handler.SetFaviconForFeed(feed, null, imageData);

            Assert.IsNull(feed.favicon,
                "feed.favicon must remain null when contentId is null.");
            Assert.IsFalse(handler.FeedHasFavicon(feed));
        }

        [Test]
        public void Group4_SetFaviconForFeed_IsNoOp_WhenContentIdIsEmpty()
        {
            // QUIRK Q2: empty contentId is also a no-op.
            FeedSource handler = CreateHandlerWithFeed(out INewsFeed feed);
            var imageData = new byte[] { 1, 2, 3, 4 };

            handler.SetFaviconForFeed(feed, "", imageData);

            Assert.IsNull(feed.favicon,
                "feed.favicon must remain null when contentId is empty.");
        }

        [Test]
        public void Group4_SetFaviconForFeed_IsNoOp_WhenImageDataIsNull()
        {
            // QUIRK Q2: null imageData -> no-op, feed.favicon not set.
            FeedSource handler = CreateHandlerWithFeed(out INewsFeed feed);

            handler.SetFaviconForFeed(feed, "some-id.ico", null);

            Assert.IsNull(feed.favicon,
                "feed.favicon must remain null when imageData is null.");
        }

        [Test]
        public void Group4_SetFaviconForFeed_IsNoOp_WhenImageDataIsEmpty()
        {
            // QUIRK Q2: zero-length imageData -> no-op, feed.favicon not set.
            FeedSource handler = CreateHandlerWithFeed(out INewsFeed feed);

            handler.SetFaviconForFeed(feed, "some-id.ico", Array.Empty<byte>());

            Assert.IsNull(feed.favicon,
                "feed.favicon must remain null when imageData is an empty array.");
        }

        [Test]
        public void Group4_SetFaviconForFeed_StringOverload_IsSilentNoOp_WhenUrlNotInFeedsTable()
        {
            // QUIRK: The string overload silently returns when the url is not in feedsTable.
            FeedSource handler = CreateHandlerWithFeed(out _);
            var imageData = new byte[] { 1, 2, 3, 4 };
            const string notSubscribedUrl = "http://not-subscribed.example.com/feed.xml";

            Assert.DoesNotThrow(() =>
                handler.SetFaviconForFeed(notSubscribedUrl, "noop.ico", imageData),
                "Set on an unknown url must not throw.");

            // Nothing stored — GetFaviconForFeed also returns null (url not in feedsTable).
            Assert.IsNull(handler.GetFaviconForFeed(notSubscribedUrl),
                "Nothing must be stored for an unsubscribed url.");
        }

        // -----------------------------------------------------------------------
        // Group 5: RemoveFaviconFromFeed (INewsFeed overload only — no string version exists)
        // -----------------------------------------------------------------------

        [Test]
        public void Group5_RemoveFaviconFromFeed_ThrowsArgumentNullException_WhenFeedIsNull()
        {
            FeedSource handler = CreateHandlerWithFeed(out _);

            Assert.Throws<ArgumentNullException>(() => handler.RemoveFaviconFromFeed(null));
        }

        [Test]
        public void Group5_RemoveFaviconFromFeed_IsNoOp_WhenFeedHasNoFavicon()
        {
            FeedSource handler = CreateHandlerWithFeed(out INewsFeed feed);
            feed.favicon = null;   // no favicon present

            // FeedHasFavicon(feed) == false -> the if-block is skipped; must not throw.
            Assert.DoesNotThrow(() => handler.RemoveFaviconFromFeed(feed));
        }

        [Test]
        public void Group5_RemoveFaviconFromFeed_DeletesBlob_ButDoesNotClearFeedFaviconProperty()
        {
            // QUIRK Q3: RemoveFaviconFromFeed calls DeleteBinaryContent but leaves feed.favicon set.
            // Consequence: FeedHasFavicon still returns true; GetFaviconForFeed returns null.
            FeedSource handler = CreateHandlerWithFeed(out INewsFeed feed);
            var imageData = new byte[] { 1, 2, 3, 4 };
            const string contentId = "chars-favicon-remove-001.ico";

            // Set up favicon so there is something to remove.
            handler.SetFaviconForFeed(feed, contentId, imageData);
            Assert.IsTrue(handler.FeedHasFavicon(feed), "Pre-condition: favicon should be set.");
            Assert.IsNotNull(handler.GetFaviconForFeed(feed), "Pre-condition: blob should be stored.");

            handler.RemoveFaviconFromFeed(feed);

            // QUIRK Q3a: feed.favicon is NOT cleared — the content-id string stays.
            Assert.AreEqual(contentId, feed.favicon,
                "QUIRK Q3: RemoveFaviconFromFeed must NOT clear feed.favicon.");

            // QUIRK Q3b: FeedHasFavicon still returns true because feed.favicon is still set.
            Assert.IsTrue(handler.FeedHasFavicon(feed),
                "QUIRK Q3: FeedHasFavicon must still return true after Remove (feed.favicon not cleared).");

            // The blob is gone — GetFaviconForFeed returns null now.
            Assert.IsNull(handler.GetFaviconForFeed(feed),
                "GetFaviconForFeed must return null after the blob is deleted.");
        }
    }
}
