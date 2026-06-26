using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NewsComponents;
using NewsComponents.Feed;
using NUnit.Framework;
using RssBandit.UnitTests;

namespace NewsComponents.UnitTests
{
    /// <summary>
    /// Characterization tests for <see cref="FeedSource"/>'s feed-list SAVE/serialize path
    /// (the <c>SaveFeedList</c> overloads + the core 4-arg writer, OPML + native NewsHandler,
    /// FeedSource.cs ~2827-3002, plus the private static <c>CreateCategoryHive</c> ~3160).
    /// This is Slice 3a (the SAVE side) of decomposing the FeedSource god-object.
    /// </summary>
    /// <remarks>
    /// These tests pin the CURRENT, observable behavior of the writer against the unchanged
    /// monolith. They are the safety net the upcoming <c>IFeedListSerializer</c> extraction must
    /// keep green; the next task extracts <c>WriteFeedList</c> behind that interface, relying on
    /// these tests to prove the on-disk shape is byte-for-byte unchanged (the sync feature
    /// compares the saved file byte-wise, so this is the riskiest concern).
    ///
    /// They intentionally assert what the code does TODAY, including the quirks, which are flagged
    /// with QUIRK comments. Do NOT "fix" any behavior here - that belongs to the extraction task.
    ///
    /// Scope: SAVE/write ONLY. ImportFeedlist / ConvertFeedList / parse are Slice 3b and are not
    /// characterized here.
    ///
    /// The six behavior groups (per the brief):
    ///   1. Native round-trip fidelity (feeds + categories + per-feed maxitemage/refreshrate).
    ///   2. The AddViewedStory save side effect (read items get pushed into storiesrecentlyviewed).
    ///   3. Empty-omission quirks (no &lt;categories&gt; when empty; &lt;user-identities&gt; always omitted).
    ///   4. The inert *Specified=false quirk (top-level enclosure/refresh attributes absent).
    ///   5. OPML structure (nested outlines mirroring the category hive; includeEmptyCategories).
    ///   6. Root namespace (native &lt;feeds&gt; is in Feeds_vCurrent = the 2004 namespace).
    ///
    /// Fixture: pure in-memory save over feeds loaded from the unpacked WebRoot resources (no web
    /// server / port binding required), so it extends <see cref="BaseTestFixture"/> directly and
    /// reloads round-tripped streams from a local file path (the same load path the settings-
    /// accessor characterization tests use).
    /// </remarks>
    [TestFixture]
    public class FeedListSaveTests : BaseTestFixture
    {
        private const string FeedList04 = "FeedList04Feeds.xml"; // 4 feeds, 3 categories
        // The live, current on-disk feed-list namespace the writer always emits.
        private const string CurrentNs = "http://www.25hoursaday.com/2004/RSSBandit/feeds/";
        // The (historical) namespace the test fixtures are physically stored in.
        private const string LegacyNs = "http://www.25hoursaday.com/2003/RSSBandit/feeds/";

        private string _origUserLocalApplicationDataPath;

        [SetUp]
        public void SetUp()
        {
            DeleteDirectory(UNPACK_DESTINATION);
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
            cfg.SearchIndexBehavior = SearchIndexBehavior.NoIndexing;
            cfg.UserLocalApplicationDataPath = UNPACK_DESTINATION;
            return cfg;
        }

        private string FixturePath(string fileName)
        {
            return Path.Combine(WEBROOT_PATH, "NewsHandlerTestFiles", fileName);
        }

        /// <summary>Loads one of the WebRoot feed-list fixtures from a local path (no web server).</summary>
        private FeedSource CreateLoadedHandler(string fixtureFile)
        {
            var handler = new BanditFeedSource(BuildCfg(), new SubscriptionLocation(FixturePath(fixtureFile)));
            handler.LoadFeedlist();
            Assert.IsTrue(handler.FeedsListOK, fixtureFile + " should load cleanly.");
            return handler;
        }

        /// <summary>A handler with no feed list loaded; feeds/categories are added programmatically.</summary>
        private FeedSource CreateEmptyHandler()
        {
            return new BanditFeedSource(BuildCfg(),
                new SubscriptionLocation(FixturePath("save-characterization-feeds.xml")));
        }

        private static INewsFeed AddBareFeed(FeedSource handler, string url, string title, string category = null)
        {
            return handler.AddFeed(new NewsFeed { link = url, title = title, category = category });
        }

        private static byte[] Save(FeedSource handler, FeedListFormat format)
        {
            using (var ms = new MemoryStream())
            {
                handler.SaveFeedList(ms, format);
                return ms.ToArray();
            }
        }

        private static byte[] Save(FeedSource handler, FeedListFormat format,
            System.Collections.Generic.IDictionary<string, INewsFeed> feeds, bool includeEmptyCategories)
        {
            using (var ms = new MemoryStream())
            {
                handler.SaveFeedList(ms, format, feeds, includeEmptyCategories);
                return ms.ToArray();
            }
        }

        private static string AsText(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        private static XDocument AsXml(byte[] bytes)
        {
            // Load from the bytes (handles any BOM the writer emits) rather than from a decoded string.
            using (var ms = new MemoryStream(bytes))
            {
                return XDocument.Load(ms);
            }
        }

        // -----------------------------------------------------------------------------------
        // Group 1: Native round-trip fidelity (the primary safety net).
        // -----------------------------------------------------------------------------------

        [Test]
        public void Group1_NativeRoundTrip_PreservesFeeds_Categories_AndPerFeedSettings()
        {
            FeedSource handler = CreateLoadedHandler(FeedList04);

            const string blogUrl = "http://haacked.com/Rss.aspx"; // category "Blogs" in FeedList04

            // Per-feed overrides set via the settings accessors so we prove they serialize.
            handler.SetMaxItemAge(blogUrl, TimeSpan.FromDays(3)); // stored as XmlConvert "P3D"
            handler.SetRefreshRate(blogUrl, 999);                 // flips refreshrateSpecified on

            // Save native, then reload into a *fresh* source from a local file path.
            byte[] saved = Save(handler, FeedListFormat.NewsHandler);
            string reloadPath = FixturePath("RoundTrip_NEW.xml");
            File.WriteAllBytes(reloadPath, saved);

            var reloaded = new BanditFeedSource(BuildCfg(), new SubscriptionLocation(reloadPath));
            reloaded.LoadFeedlist();
            Assert.IsTrue(reloaded.FeedsListOK, "Round-tripped native feed list should reload cleanly.");

            // (a) All four feeds survive.
            Assert.AreEqual(4, reloaded.GetFeeds().Count, "All 4 feeds should survive the round-trip.");
            Assert.IsTrue(reloaded.IsSubscribed(blogUrl));
            Assert.IsTrue(reloaded.IsSubscribed("http://www.rendelmann.info/blog/SyndicationService.asmx/GetRss"));
            Assert.IsTrue(reloaded.IsSubscribed("http://www.kuro5hin.org/backend.rdf"));
            Assert.IsTrue(reloaded.IsSubscribed("http://www.25hoursaday.com/weblog/SyndicationService.asmx/GetRss"));

            // (b) Per-feed category survives.
            Assert.AreEqual("Blogs", reloaded.GetFeeds()[blogUrl].category);
            Assert.AreEqual("News Technology", reloaded.GetFeeds()["http://www.kuro5hin.org/backend.rdf"].category);

            // (c) The explicit <categories> list survives (FeedList04 has 3 categories).
            Assert.IsTrue(reloaded.HasCategory("News Technology"));
            Assert.IsTrue(reloaded.HasCategory("Blogs"));
            Assert.IsTrue(reloaded.HasCategory("Blogs Microsoft"));

            // (d) Per-feed maxitemage + refreshrate survive (the values we set above).
            Assert.AreEqual("P3D", reloaded.GetFeeds()[blogUrl].maxitemage,
                "max-item-age is serialized as the XmlConvert duration string and round-trips.");
            Assert.AreEqual(TimeSpan.FromDays(3), reloaded.GetMaxItemAge(blogUrl));
            Assert.AreEqual(999, reloaded.GetFeeds()[blogUrl].refreshrate);
            Assert.AreEqual(999, reloaded.GetRefreshRate(blogUrl));
        }

        // -----------------------------------------------------------------------------------
        // Group 2: The AddViewedStory save side effect (QUIRK).
        // During native save the writer walks each feed's cached items and, for every BeenRead
        // item not already known, calls f.AddViewedStory(item.Id) - mutating the feed's
        // storiesrecentlyviewed collection as a side effect of *saving*.
        // -----------------------------------------------------------------------------------

        [Test]
        public void Group2_NativeSave_PushesReadItems_IntoStoriesRecentlyViewed()
        {
            FeedSource handler = CreateEmptyHandler();

            const string feedUrl = "http://example.com/g2-readstate.xml";
            var feed = new NewsFeed { link = feedUrl, title = "Read-state Feed", category = null };

            // Two items: one read, one unread. Ids distinct from links so they are not collapsed.
            var readItem = new NewsItem(feed, "Read item", feedUrl + "#1", "body",
                DateTime.Now, null, "g2-story-read", null) { BeenRead = true };
            var unreadItem = new NewsItem(feed, "Unread item", feedUrl + "#2", "body",
                DateTime.Now, null, "g2-story-unread", null) { BeenRead = false };

            // Capture the *actual* ids (the ctor may canonicalize via the relation cosmos).
            string readId = readItem.Id;
            string unreadId = unreadItem.Id;

            var feedInfo = new FeedInfo(feedUrl, feedUrl, new[] { (INewsItem)readItem, unreadItem });
            handler.AddFeed(feed, feedInfo); // populates feedsTable + itemsTable

            // Pre-condition: nothing has been viewed yet.
            Assert.AreEqual(0, feed.storiesrecentlyviewed.Count,
                "Pre-condition: the feed starts with an empty storiesrecentlyviewed list.");

            // Saving is what triggers the side effect.
            Save(handler, FeedListFormat.NewsHandler);

            // QUIRK: the read item's id was pushed into storiesrecentlyviewed by *save*...
            Assert.IsTrue(feed.storiesrecentlyviewed.Contains(readId),
                "QUIRK: native save syncs each BeenRead item into the feed's storiesrecentlyviewed.");
            // ...and the unread item's id was NOT.
            Assert.IsFalse(feed.storiesrecentlyviewed.Contains(unreadId),
                "Unread items are not added to storiesrecentlyviewed.");
            Assert.AreEqual(1, feed.storiesrecentlyviewed.Count,
                "Exactly the one read item should have been recorded.");
        }

        [Test]
        public void Group2_NativeLite_DoesNotPushReadItems_IntoStoriesRecentlyViewed()
        {
            // Counterpart to the quirk: the side effect is skipped for the NewsHandlerLite format
            // (the BeenRead -> AddViewedStory loop is gated on !NewsHandlerLite).
            FeedSource handler = CreateEmptyHandler();

            const string feedUrl = "http://example.com/g2-lite.xml";
            var feed = new NewsFeed { link = feedUrl, title = "Lite Feed", category = null };
            var readItem = new NewsItem(feed, "Read item", feedUrl + "#1", "body",
                DateTime.Now, null, "g2-lite-read", null) { BeenRead = true };

            var feedInfo = new FeedInfo(feedUrl, feedUrl, new[] { (INewsItem)readItem });
            handler.AddFeed(feed, feedInfo);

            Save(handler, FeedListFormat.NewsHandlerLite);

            Assert.AreEqual(0, feed.storiesrecentlyviewed.Count,
                "NewsHandlerLite save must NOT push read items into storiesrecentlyviewed.");
        }

        // -----------------------------------------------------------------------------------
        // Group 3: Empty-omission quirks.
        //   - With no categories, the writer sets feedlist.categories = null, so no <categories>.
        //   - identities is ALWAYS null on save, so <user-identities> is always omitted.
        // -----------------------------------------------------------------------------------

        [Test]
        public void Group3_NativeSave_NoCategories_OmitsTopLevelCategoriesElement()
        {
            FeedSource handler = CreateEmptyHandler();
            AddBareFeed(handler, "http://example.com/g3-nocat.xml", "No-category Feed"); // category null

            XDocument doc = AsXml(Save(handler, FeedListFormat.NewsHandler));
            XNamespace ns = CurrentNs;

            // QUIRK: empty category set => feedlist.categories = null => the whole top-level
            // <categories> element is dropped (not even an empty <categories />). Checked as a
            // DIRECT child of <feeds>, because each <feed> emits its own (unrelated, empty)
            // <categories /> element.
            CollectionAssert.IsEmpty(doc.Root.Elements(ns + "categories"),
                "QUIRK: with no categories, the top-level <categories> element is omitted entirely.");
        }

        [Test]
        public void Group3_NativeSave_AlwaysOmitsUserIdentitiesElement()
        {
            // QUIRK: feedlist.identities is hard-set to null on save (identities are saved
            // separately), so <user-identities> never appears - even with categories present.
            FeedSource handler = CreateLoadedHandler(FeedList04);

            string xml = AsText(Save(handler, FeedListFormat.NewsHandler));

            StringAssert.DoesNotContain("<user-identities", xml,
                "QUIRK: <user-identities> is always omitted (identities are set to null on save).");
        }

        [Test]
        public void Group3_NativeSave_WithCategories_EmitsTopLevelCategoriesElement()
        {
            // The positive control for the omission quirk: when categories DO exist they are written
            // as a single top-level <categories> element holding the category names as <category> text.
            FeedSource handler = CreateLoadedHandler(FeedList04);

            XDocument doc = AsXml(Save(handler, FeedListFormat.NewsHandler));
            XNamespace ns = CurrentNs;

            var topLevel = doc.Root.Elements(ns + "categories").ToList();
            Assert.AreEqual(1, topLevel.Count,
                "Non-empty category set => exactly one top-level <categories> element is written.");

            var names = topLevel[0].Elements(ns + "category").Select(c => (string)c).ToList();
            CollectionAssert.Contains(names, "News Technology");
            CollectionAssert.Contains(names, "Blogs");
            CollectionAssert.Contains(names, "Blogs Microsoft");
        }

        // -----------------------------------------------------------------------------------
        // Group 4: The inert *Specified=false quirk.
        // Native save flips the legacy top-level enclosure/refresh "Specified" flags to false, so
        // the serialized root <feeds> carries NONE of these attributes.
        // -----------------------------------------------------------------------------------

        [Test]
        public void Group4_NativeSave_RootHasNoLegacyEnclosureOrRefreshAttributes()
        {
            FeedSource handler = CreateLoadedHandler(FeedList04);

            XDocument doc = AsXml(Save(handler, FeedListFormat.NewsHandler));
            XElement root = doc.Root;

            // These six attributes are gated by *Specified flags the writer forces to false.
            foreach (var attrName in new[]
                     {
                         "download-enclosures",
                         "enclosure-folder",
                         "enclosure-cache-size-in-MB",
                         "num-enclosures-to-download-on-new-feed",
                         "enclosure-alert",
                         "create-subfolders-for-enclosures",
                     })
            {
                Assert.IsNull(root.Attribute(attrName),
                    "QUIRK: top-level <feeds> must NOT carry the '" + attrName + "' attribute.");
            }

            // mark-items-read-on-exit and refresh-rate are forced unspecified too, so they are absent.
            Assert.IsNull(root.Attribute("mark-items-read-on-exit"),
                "mark-items-read-on-exit is also forced unspecified and thus absent at the top level.");
            Assert.IsNull(root.Attribute("refresh-rate"),
                "refresh-rate is forced unspecified and thus absent at the top level.");
        }

        // -----------------------------------------------------------------------------------
        // Group 5: OPML structure.
        // -----------------------------------------------------------------------------------

        [Test]
        public void Group5_Opml_NestedCategories_ProduceNestedOutlines()
        {
            FeedSource handler = CreateEmptyHandler();

            const string url = "http://example.com/g5-nested.xml";
            // A nested category path: separator is '\' (FeedSource.CategorySeparator).
            AddBareFeed(handler, url, "Nested Feed", @"News Technology\Blogs");

            XDocument doc = AsXml(Save(handler, FeedListFormat.OPML, handler.GetFeeds(), false));

            // Well-formed OPML 1.0 with head + body.
            Assert.AreEqual("opml", doc.Root.Name.LocalName);
            Assert.AreEqual("1.0", (string)doc.Root.Attribute("version"));
            XElement body = doc.Root.Element("body");
            Assert.IsNotNull(body, "OPML must have a <body>.");

            // Nested outlines mirror the category hive: News Technology > Blogs > <feed outline>.
            XElement newsTech = body.Elements("outline")
                .Single(o => (string)o.Attribute("title") == "News Technology");
            XElement blogs = newsTech.Elements("outline")
                .Single(o => (string)o.Attribute("title") == "Blogs");
            XElement feedOutline = blogs.Elements("outline")
                .Single(o => (string)o.Attribute("xmlUrl") == url);

            // The feed outline carries title/text/type=rss (no htmlUrl/description: no itemsTable entry).
            Assert.AreEqual("rss", (string)feedOutline.Attribute("type"));
            Assert.AreEqual("Nested Feed", (string)feedOutline.Attribute("title"));
            Assert.AreEqual("Nested Feed", (string)feedOutline.Attribute("text"));
        }

        [Test]
        public void Group5_Opml_IncludeEmptyCategories_TogglesEmptyCategoryOutline()
        {
            FeedSource handler = CreateEmptyHandler();

            // A feed sitting in "Tech" (a category that HAS a feed)...
            AddBareFeed(handler, "http://example.com/g5-tech.xml", "Tech Feed", "Tech");
            // ...and an extra category with NO feed in it.
            handler.AddCategory("Empty Category");

            // includeEmptyCategories = false: the feedless category is NOT emitted.
            XDocument without = AsXml(Save(handler, FeedListFormat.OPML, handler.GetFeeds(), false));
            Assert.IsTrue(OutlineTitles(without).Contains("Tech"),
                "A category that has a feed is always emitted.");
            Assert.IsFalse(OutlineTitles(without).Contains("Empty Category"),
                "includeEmptyCategories=false: a feedless category is NOT emitted.");

            // includeEmptyCategories = true: the feedless category IS emitted.
            XDocument with = AsXml(Save(handler, FeedListFormat.OPML, handler.GetFeeds(), true));
            Assert.IsTrue(OutlineTitles(with).Contains("Empty Category"),
                "includeEmptyCategories=true: the feedless category IS emitted as an empty outline.");
        }

        private static System.Collections.Generic.List<string> OutlineTitles(XDocument doc)
        {
            return doc.Descendants("outline")
                .Select(o => (string)o.Attribute("title"))
                .Where(t => t != null)
                .ToList();
        }

        // -----------------------------------------------------------------------------------
        // Group 6: Root namespace.
        // Native output's root <feeds> is in the CURRENT (2004) namespace - regardless of the
        // (2003) namespace the fixture was physically loaded from.
        // -----------------------------------------------------------------------------------

        [Test]
        public void Group6_NativeSave_RootFeedsElement_IsInCurrentNamespace()
        {
            FeedSource handler = CreateLoadedHandler(FeedList04); // fixture is stored in the 2003 ns

            XDocument doc = AsXml(Save(handler, FeedListFormat.NewsHandler));

            Assert.AreEqual("feeds", doc.Root.Name.LocalName);
            Assert.AreEqual(CurrentNs, doc.Root.Name.NamespaceName,
                "Saved <feeds> root is in the current (2004) feeds namespace.");
            Assert.AreEqual(NamespaceCore.Feeds_vCurrent, doc.Root.Name.NamespaceName);
            // QUIRK: the fixture is stored in the legacy 2003 namespace, but save always emits 2004.
            Assert.AreNotEqual(LegacyNs, doc.Root.Name.NamespaceName,
                "Save upgrades the namespace: a 2003-loaded list is written back in the 2004 namespace.");
        }
    }
}
