using System;
using System.Xml;
using NewsComponents;
using NewsComponents.Feed;
using NUnit.Framework;
using RssBandit.UnitTests;

namespace NewsComponents.UnitTests
{
    /// <summary>
    /// Characterization tests for <see cref="FeedSource"/>'s per-feed and per-category
    /// settings accessors (Concern Q of the FeedSource decomposition; Slice 1, Task 1).
    /// </summary>
    /// <remarks>
    /// These tests pin the CURRENT, observable behavior of the settings accessors against the
    /// unchanged monolith. They are the safety net the upcoming
    /// <c>IFeedAndCategorySettings</c> extraction must keep green. They intentionally assert
    /// what the code does TODAY, even where that is surprising (those cases are flagged with
    /// SURPRISE comments). Do not "fix" behavior here - that belongs in the extraction task.
    ///
    /// They are pure in-memory get/set tests over a loaded feed list; no live feed fetching is
    /// required, so this fixture extends <see cref="BaseTestFixture"/> directly (no web server,
    /// no port binding) and loads <c>FeedList03Feeds.xml</c> from the unpacked resources.
    /// </remarks>
    [TestFixture]
    public class FeedAndCategorySettingsTests : BaseTestFixture
    {
        // Feeds present in FeedList03Feeds.xml (category in parentheses):
        private const string MsdnFeed = "http://msdn.microsoft.com/vcsharp/rss.xml"; // category "Development"

        // FeedSource exposes a couple of process-wide static settings (Stylesheet,
        // MarkItemsReadOnExit) that the accessors fall back to. We snapshot and restore them so
        // these tests neither leak into, nor depend on ordering with, the rest of the suite.
        private string _origStylesheet;
        private bool _origMarkItemsReadOnExit;

        [SetUp]
        public void SetUp()
        {
            DeleteDirectory(UNPACK_DESTINATION);
            UnpackResourceDirectory("WebRoot.NewsHandlerTestFiles");

            _origStylesheet = FeedSource.Stylesheet;
            _origMarkItemsReadOnExit = FeedSource.MarkItemsReadOnExit;
        }

        [TearDown]
        public void TearDown()
        {
            FeedSource.Stylesheet = _origStylesheet;
            FeedSource.MarkItemsReadOnExit = _origMarkItemsReadOnExit;
        }

        private FeedSource CreateLoadedHandler()
        {
            var cfg = NewsComponentsConfiguration.Default as NewsComponentsConfiguration;
            cfg.SearchIndexBehavior = SearchIndexBehavior.NoIndexing;

            var handler = new BanditFeedSource(cfg,
                new SubscriptionLocation(WEBROOT_PATH + @"\NewsHandlerTestFiles\FeedList03Feeds.xml"));
            handler.LoadFeedlist();
            Assert.IsTrue(handler.FeedsListOK, "FeedList03Feeds.xml should load cleanly.");
            return handler;
        }

        /// <summary>Adds a bare in-memory feed (optionally in a category) with nothing specified.</summary>
        private static INewsFeed AddBareFeed(FeedSource handler, string url, string category = null)
        {
            var feed = new NewsFeed { link = url, title = "Title for " + url, category = category };
            return handler.AddFeed(feed);
        }

        // -----------------------------------------------------------------------------------
        // Group 1: Per-feed override, the maxitemage TimeSpan <-> XmlConvert round-trip, and
        //          the "...Specified" flag convention (set only for non-string int/bool props).
        // -----------------------------------------------------------------------------------

        [Test]
        public void Group1_SetMaxItemAge_StoresXmlConvertString_AndSurfacesAsTimeSpan()
        {
            var handler = CreateLoadedHandler();
            const string url = "http://example.com/g1-maxage.xml";
            INewsFeed feed = AddBareFeed(handler, url);

            handler.SetMaxItemAge(url, TimeSpan.FromDays(3));

            // Stored on the feed as an XmlConvert duration string ("P3D")...
            Assert.AreEqual(XmlConvert.ToString(TimeSpan.FromDays(3)), feed.maxitemage,
                "maxitemage is persisted as an XmlConvert duration string.");
            // ...and surfaced back through the getter as a TimeSpan (the round-trip).
            Assert.AreEqual(TimeSpan.FromDays(3), handler.GetMaxItemAge(url),
                "GetMaxItemAge converts the stored XmlConvert string back to a TimeSpan.");

            // SURPRISE: maxitemage is a string property, so SetFeedProperty does NOT set any
            // "Specified" flag for it (there is no maxitemageSpecified). "Is it set?" is decided
            // purely by string emptiness / the MaxValue sentinel (see Group 2).
        }

        [Test]
        public void Group1_SetRefreshRate_SetsSpecifiedFlag_AndOverridesGetter()
        {
            var handler = CreateLoadedHandler();
            const string url = "http://example.com/g1-refresh.xml";
            INewsFeed feed = AddBareFeed(handler, url);

            Assert.IsFalse(feed.refreshrateSpecified, "baseline: a bare feed has refreshrate unspecified.");

            handler.SetRefreshRate(url, 999);

            Assert.IsTrue(feed.refreshrateSpecified,
                "SetRefreshRate (an int property) flips the refreshrateSpecified flag on.");
            Assert.AreEqual(999, handler.GetRefreshRate(url),
                "The per-feed override is returned by the getter.");
        }

        [Test]
        public void Group1_SetMarkItemsReadOnExit_SetsSpecifiedFlag_AndOverridesGetter()
        {
            var handler = CreateLoadedHandler();
            const string url = "http://example.com/g1-mark.xml";
            INewsFeed feed = AddBareFeed(handler, url);

            Assert.IsFalse(feed.markitemsreadonexitSpecified, "baseline: bare feed unspecified.");

            handler.SetMarkItemsReadOnExit(url, true);

            Assert.IsTrue(feed.markitemsreadonexitSpecified,
                "SetMarkItemsReadOnExit (a bool property) flips the Specified flag on.");
            Assert.IsTrue(handler.GetMarkItemsReadOnExit(url));
        }

        [Test]
        public void Group1_SetStringProperties_DoNotSetSpecifiedFlag_ButStillOverride()
        {
            var handler = CreateLoadedHandler();
            const string url = "http://example.com/g1-strings.xml";
            INewsFeed feed = AddBareFeed(handler, url);

            handler.SetStyleSheet(url, "feed.xslt");
            handler.SetFeedColumnLayoutID(url, "layout-A");

            // String properties are "set" by virtue of being a non-empty string; there is no
            // separate Specified flag for them.
            Assert.AreEqual("feed.xslt", feed.stylesheet);
            Assert.AreEqual("layout-A", feed.listviewlayout);
            Assert.AreEqual("feed.xslt", handler.GetStyleSheet(url));
            Assert.AreEqual("layout-A", handler.GetFeedColumnLayoutID(url));
        }

        // -----------------------------------------------------------------------------------
        // Group 2: The maxitemage == TimeSpan.MaxValue "unset" sentinel. IsPropertyValueSet
        //          treats a feed/category maxitemage equal to XmlConvert.ToString(MaxValue) as
        //          "not set", so the getter falls through to the inherited / instance default.
        // -----------------------------------------------------------------------------------

        [Test]
        public void Group2_MaxValueSentinel_IsTreatedAsUnset_FallsBackToInstanceDefault()
        {
            var handler = CreateLoadedHandler();
            handler.MaxItemAge = TimeSpan.FromDays(30); // distinctive instance-level default
            const string url = "http://example.com/g2-sentinel.xml";
            INewsFeed feed = AddBareFeed(handler, url); // no category

            handler.SetMaxItemAge(url, TimeSpan.MaxValue);

            // The sentinel string IS physically stored on the feed...
            Assert.AreEqual(XmlConvert.ToString(TimeSpan.MaxValue), feed.maxitemage,
                "TimeSpan.MaxValue is stored as its XmlConvert string.");
            // ...but it is interpreted as "unset", so the getter returns the instance default,
            // NOT TimeSpan.MaxValue. SURPRISE: setting a feed's max-item-age to MaxValue is
            // effectively the same as clearing it.
            Assert.AreEqual(TimeSpan.FromDays(30), handler.GetMaxItemAge(url),
                "MaxValue sentinel makes GetMaxItemAge fall through to the instance default.");
        }

        [Test]
        public void Group2_NeverSet_FallsBackToInstanceDefault()
        {
            var handler = CreateLoadedHandler();
            handler.MaxItemAge = TimeSpan.FromDays(42);
            const string url = "http://example.com/g2-unset.xml";
            AddBareFeed(handler, url); // nothing specified, no category

            Assert.AreEqual(TimeSpan.FromDays(42), handler.GetMaxItemAge(url),
                "With nothing specified, GetMaxItemAge returns the instance-level MaxItemAge.");
        }

        // -----------------------------------------------------------------------------------
        // Group 3: Category-chain inheritance. GetMaxItemAge(feed) inherits up the feed's
        //          category.parent chain until a value "is set". The inheritance setup writes
        //          directly to the category objects so it stays deterministic and independent
        //          of the (order-unstable) prefix-match SetCategory* path exercised in Group 5.
        // -----------------------------------------------------------------------------------

        [Test]
        public void Group3_FeedInheritsMaxItemAge_FromAncestorCategoryChain()
        {
            var handler = CreateLoadedHandler();
            handler.MaxItemAge = TimeSpan.FromDays(30); // instance default, distinct from all below

            INewsFeedCategory parent = handler.AddCategory("Sports");
            INewsFeedCategory child = handler.AddCategory(@"Sports\Football"); // child.parent == parent

            const string url = "http://example.com/g3-football.xml";
            AddBareFeed(handler, url, @"Sports\Football");

            // (a) Nothing set on either category -> feed falls through to the instance default.
            Assert.AreEqual(TimeSpan.FromDays(30), handler.GetMaxItemAge(url),
                "No category value set -> instance default.");

            // (b) Value set on the *parent* only -> feed inherits the parent's value.
            parent.maxitemage = XmlConvert.ToString(TimeSpan.FromDays(7));
            Assert.AreEqual(TimeSpan.FromDays(7), handler.GetMaxItemAge(url),
                "Feed inherits the nearest ancestor category that has a value set (the parent).");

            // (c) Value also set on the *nearer* (child) category -> nearer wins over farther.
            child.maxitemage = XmlConvert.ToString(TimeSpan.FromDays(2));
            Assert.AreEqual(TimeSpan.FromDays(2), handler.GetMaxItemAge(url),
                "The nearer category in the chain wins over the farther one.");
        }

        [Test]
        public void Group3_GetCategoryMaxItemAge_AlsoWalksParentChain()
        {
            var handler = CreateLoadedHandler();
            handler.MaxItemAge = TimeSpan.FromDays(30);

            INewsFeedCategory parent = handler.AddCategory("Music");
            handler.AddCategory(@"Music\Jazz");

            parent.maxitemage = XmlConvert.ToString(TimeSpan.FromDays(9));

            // The category-level getter inherits from ancestor categories too (no inheritCategory
            // flag at category level - it always walks the chain).
            Assert.AreEqual(TimeSpan.FromDays(9), handler.GetCategoryMaxItemAge(@"Music\Jazz"),
                "GetCategoryMaxItemAge walks up to the parent category for an unset child.");
        }

        // -----------------------------------------------------------------------------------
        // Group 4: Default fallback when nothing is specified on the feed or its category.
        //          refreshrate  -> the configuration's RefreshRate (FeedSource.RefreshRate)
        //          stylesheet   -> the static FeedSource.Stylesheet
        //          markitemsreadonexit -> the static FeedSource.MarkItemsReadOnExit
        //          listviewlayout -> the instance default, which is null
        // -----------------------------------------------------------------------------------

        [Test]
        public void Group4_GetRefreshRate_FallsBackToConfigurationRefreshRate()
        {
            var handler = CreateLoadedHandler();
            const string url = "http://example.com/g4-refresh.xml";
            AddBareFeed(handler, url); // unspecified, no category

            Assert.AreEqual(handler.RefreshRate, handler.GetRefreshRate(url),
                "Unspecified feed refresh rate falls back to the configuration's RefreshRate.");
        }

        [Test]
        public void Group4_GetStyleSheet_FallsBackToStaticStylesheet()
        {
            var handler = CreateLoadedHandler();
            const string url = "http://example.com/g4-style.xml";
            AddBareFeed(handler, url);

            FeedSource.Stylesheet = "global-default.xslt";

            Assert.AreEqual("global-default.xslt", handler.GetStyleSheet(url),
                "Unspecified feed stylesheet falls back to the static FeedSource.Stylesheet.");
        }

        [Test]
        public void Group4_GetMarkItemsReadOnExit_FallsBackToStaticDefault()
        {
            var handler = CreateLoadedHandler();
            const string url = "http://example.com/g4-mark.xml";
            AddBareFeed(handler, url);

            FeedSource.MarkItemsReadOnExit = true;
            Assert.IsTrue(handler.GetMarkItemsReadOnExit(url),
                "Unspecified feed falls back to static MarkItemsReadOnExit (true).");

            FeedSource.MarkItemsReadOnExit = false;
            Assert.IsFalse(handler.GetMarkItemsReadOnExit(url),
                "Unspecified feed falls back to static MarkItemsReadOnExit (false).");
        }

        [Test]
        public void Group4_GetFeedColumnLayoutID_FallsBackToNull()
        {
            var handler = CreateLoadedHandler();
            const string url = "http://example.com/g4-layout.xml";
            AddBareFeed(handler, url);

            Assert.IsNull(handler.GetFeedColumnLayoutID(url),
                "The instance-level listviewlayout default is null, so an unspecified feed returns null.");
        }

        [Test]
        public void Group4_FeedLevelStringGetters_DoNotInheritFromCategory()
        {
            // SURPRISE / asymmetry pinned: at FEED level only maxitemage and refreshrate inherit
            // the category chain (GetFeedProperty is called with inheritCategory:true). stylesheet,
            // listviewlayout and markitemsreadonexit use inheritCategory:false, so a value set on
            // the feed's category is ignored by the feed-level getter - it goes straight to the
            // instance/static default. (The category-level getter, by contrast, DOES inherit -
            // see the second assertion.)
            var handler = CreateLoadedHandler();
            FeedSource.Stylesheet = "global.xslt";

            INewsFeedCategory parent = handler.AddCategory("Tech");
            handler.AddCategory(@"Tech\Gadgets");
            parent.stylesheet = "category.xslt";

            const string url = "http://example.com/g4-noinherit.xml";
            AddBareFeed(handler, url, @"Tech\Gadgets");

            Assert.AreEqual("global.xslt", handler.GetStyleSheet(url),
                "Feed-level GetStyleSheet does NOT inherit the category's stylesheet.");
            Assert.AreEqual("category.xslt", handler.GetCategoryStyleSheet(@"Tech\Gadgets"),
                "Category-level GetCategoryStyleSheet DOES inherit the parent category's stylesheet.");
        }

        // -----------------------------------------------------------------------------------
        // Group 5: SetCategoryProperty prefix-match semantics. A category set applies to the
        //          first category whose Value either equals the name or starts with
        //          (name + CategorySeparator). Each case below is constructed so that exactly
        //          ONE category can match, keeping the assertion deterministic despite the
        //          unordered ConcurrentDictionary backing store.
        // -----------------------------------------------------------------------------------

        [Test]
        public void Group5_SetCategory_ExactMatch_AppliesToThatCategory()
        {
            var handler = CreateLoadedHandler();
            // "Development" is a loaded, leaf category with no "Development\..." children.
            handler.SetCategoryMaxItemAge("Development", TimeSpan.FromDays(5));

            Assert.AreEqual(TimeSpan.FromDays(5), handler.GetCategoryMaxItemAge("Development"),
                "SetCategory* on an exact category name applies to that category (Value.Equals branch).");
        }

        [Test]
        public void Group5_SetCategory_PrefixMatch_AppliesToChildViaStartsWith()
        {
            var handler = CreateLoadedHandler();
            // Add ONLY the child object (no ancestor "TechBlogs" is created), so the sole category
            // that can match the name "TechBlogs" is "TechBlogs\Daily" via the StartsWith branch.
            handler.AddCategory(new category(@"TechBlogs\Daily"));

            handler.SetCategoryRefreshRate("TechBlogs", 1234);

            Assert.AreEqual(1234, handler.GetCategoryRefreshRate(@"TechBlogs\Daily"),
                "SetCategory* with a parent-prefix name lands on the child via the StartsWith(name + separator) branch.");
        }

        // -----------------------------------------------------------------------------------
        // Group 6: The stringly-typed key + Specified convention. Round-trip every one of the
        //          five properties at both feed and category level, so a future typo in the key
        //          mapping (which would silently hit the GetSharedPropertyValue 'default' branch)
        //          is caught.
        // -----------------------------------------------------------------------------------

        [Test]
        public void Group6_AllFiveFeedProperties_RoundTrip()
        {
            var handler = CreateLoadedHandler();
            const string url = "http://example.com/g6-feed.xml";
            AddBareFeed(handler, url);

            handler.SetMaxItemAge(url, TimeSpan.FromDays(11));          // "maxitemage"
            handler.SetRefreshRate(url, 4242);                          // "refreshrate"
            handler.SetStyleSheet(url, "feed-rt.xslt");                 // "stylesheet"
            handler.SetFeedColumnLayoutID(url, "feed-layout-7");        // "listviewlayout"
            handler.SetMarkItemsReadOnExit(url, true);                  // "markitemsreadonexit"

            Assert.AreEqual(TimeSpan.FromDays(11), handler.GetMaxItemAge(url));
            Assert.AreEqual(4242, handler.GetRefreshRate(url));
            Assert.AreEqual("feed-rt.xslt", handler.GetStyleSheet(url));
            Assert.AreEqual("feed-layout-7", handler.GetFeedColumnLayoutID(url));
            Assert.IsTrue(handler.GetMarkItemsReadOnExit(url));
        }

        [Test]
        public void Group6_AllFiveCategoryProperties_RoundTrip()
        {
            var handler = CreateLoadedHandler();
            // A single leaf category so every SetCategory* exact-matches exactly one category.
            handler.AddCategory("RoundTripCat");

            handler.SetCategoryMaxItemAge("RoundTripCat", TimeSpan.FromDays(13));   // "maxitemage"
            handler.SetCategoryRefreshRate("RoundTripCat", 5151);                  // "refreshrate"
            handler.SetCategoryStyleSheet("RoundTripCat", "cat-rt.xslt");          // "stylesheet"
            handler.SetCategoryFeedColumnLayoutID("RoundTripCat", "cat-layout");   // "listviewlayout"
            handler.SetCategoryMarkItemsReadOnExit("RoundTripCat", true);          // "markitemsreadonexit"

            Assert.AreEqual(TimeSpan.FromDays(13), handler.GetCategoryMaxItemAge("RoundTripCat"));
            Assert.AreEqual(5151, handler.GetCategoryRefreshRate("RoundTripCat"));
            Assert.AreEqual("cat-rt.xslt", handler.GetCategoryStyleSheet("RoundTripCat"));
            Assert.AreEqual("cat-layout", handler.GetCategoryFeedColumnLayoutID("RoundTripCat"));
            Assert.IsTrue(handler.GetCategoryMarkItemsReadOnExit("RoundTripCat"));
        }
    }
}
