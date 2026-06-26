using System;
using System.IO;
using System.Text;
using System.Xml;
using NewsComponents;
using NewsComponents.Feed;
using NewsComponents.Utils;
using NUnit.Framework;
using RssBandit.UnitTests;

namespace NewsComponents.UnitTests
{
    /// <summary>
    /// Characterization tests for <see cref="FeedSource"/>'s feed-list PARSE/convert path
    /// (the import side, the counterpart to Slice 3a's SAVE side):
    ///   - <c>ConvertFeedList(XmlDocument)</c> (FeedSource.cs ~4899-4934): the pure
    ///     <c>XmlDocument</c>-&gt;<c>XmlDocument</c> format normalizer, and
    ///   - the parse path observable through <c>ImportFeedlist(Stream, category, replace)</c>
    ///     (~4959-4975), which loads the stream, calls <c>ConvertFeedList</c>, deserializes the
    ///     <c>feeds</c> object (the fragment Slice 3b will extract as <c>ParseFeedList</c>) and
    ///     hands it to the in-place merge engine.
    /// This is Slice 3b (the PARSE side) of decomposing the FeedSource god-object.
    /// </summary>
    /// <remarks>
    /// These tests pin the CURRENT, observable behavior against the unchanged monolith. They are the
    /// safety net the upcoming <c>IFeedListSerializer.ParseFeedList</c> extraction must keep green.
    /// They intentionally assert what the code does TODAY, including quirks (flagged QUIRK). Do NOT
    /// "fix" behavior here - that belongs to the extraction task.
    ///
    /// Most of this surface is otherwise UNTESTED; the point of this fixture is to pin the three
    /// previously-uncovered branches of <c>ConvertFeedList</c>:
    ///   1. OPML import        -> ImportFilter detects "opml" root, runs the OPML XSLT, yielding a
    ///                            native &lt;feeds&gt; document in the CURRENT (2004) namespace.
    ///   2. Legacy 2003 ns     -> the Bandit branch round-trips the document through
    ///                            RssBanditXmlReader / RssBanditXmlNamespaceResolver, which rewrite
    ///                            the historical Feeds_v2003 namespace up to Feeds_vCurrent so the
    ///                            (2004-bound) deserializer accepts it.
    ///   3. Unknown format     -> ApplicationException("Unknown Feed Format.").
    /// Plus a native (already-current-namespace) anchor and the empty/malformed edge.
    ///
    /// Fixture: pure in-memory; ConvertFeedList is a stateless instance method, and the import path
    /// merges into the (initially empty) feedsTable, so no web server / port binding is required.
    /// Extends <see cref="BaseTestFixture"/> directly (same approach as FeedListSaveTests).
    /// </remarks>
    [TestFixture]
    public class FeedListParseTests : BaseTestFixture
    {
        // The live, current feed-list namespace (== NamespaceCore.Feeds_vCurrent == Feeds_v2004).
        private const string CurrentNs = "http://www.25hoursaday.com/2004/RSSBandit/feeds/";
        // The historical namespace legacy 1.2.* feed lists were stored in.
        private const string LegacyNs = "http://www.25hoursaday.com/2003/RSSBandit/feeds/";

        private string _origUserLocalApplicationDataPath;

        [SetUp]
        public void SetUp()
        {
            DeleteDirectory(UNPACK_DESTINATION);
            // Unpack one resource dir so WEBROOT_PATH / the cache path exist for handler construction.
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

        /// <summary>A handler with no feed list loaded; the import merges into an empty feedsTable.</summary>
        private FeedSource CreateEmptyHandler()
        {
            return new BanditFeedSource(BuildCfg(),
                new SubscriptionLocation(FixturePath("parse-characterization-feeds.xml")));
        }

        private static XmlDocument Doc(string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            return doc;
        }

        private static Stream StreamOf(string xml)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(xml));
        }

        // -----------------------------------------------------------------------------------
        // Sample documents
        // -----------------------------------------------------------------------------------

        private const string NativeCurrentNsFeeds =
            "<feeds xmlns=\"" + CurrentNs + "\">" +
            "  <feed><title>Native Anchor</title><link>http://native.example.com/feed.xml</link></feed>" +
            "</feeds>";

        // A native list stored in the historical 2003 namespace (the legacy back-compat path).
        private const string LegacyNsFeeds =
            "<feeds xmlns=\"" + LegacyNs + "\">" +
            "  <feed category=\"Legacy\"><title>Legacy Feed</title><link>http://legacy.example.com/feed.xml</link></feed>" +
            "</feeds>";

        // Minimal OPML 1.0: one category outline containing one feed outline.
        private const string OpmlDoc =
            "<opml version=\"1.0\">" +
            "  <head><title>Sample OPML</title></head>" +
            "  <body>" +
            "    <outline title=\"My Category\" text=\"My Category\">" +
            "      <outline title=\"Example Feed\" text=\"Example Feed\" xmlUrl=\"http://opml.example.com/feed.xml\" />" +
            "    </outline>" +
            "  </body>" +
            "</opml>";

        // Neither Bandit (2003/2004 ns), OPML (root local-name 'opml'), SIAM (its ns) nor OCS
        // (the InternetAlchemy directory namespace attribute) -> Unknown.
        private const string UnknownDoc =
            "<not-a-feedlist xmlns=\"http://example.com/some/other/format\"><thing/></not-a-feedlist>";

        // -----------------------------------------------------------------------------------
        // 1. Native anchor (the Bandit branch with an already-current namespace).
        // -----------------------------------------------------------------------------------

        [Test]
        public void Native_ConvertFeedList_RoundTripsAsFeedsInCurrentNamespace()
        {
            FeedSource handler = CreateEmptyHandler();

            XmlDocument result = handler.ConvertFeedList(Doc(NativeCurrentNsFeeds));

            // The Bandit branch loads the doc through RssBanditXmlReader and returns native <feeds>.
            Assert.AreEqual("feeds", result.DocumentElement.LocalName);
            Assert.AreEqual(CurrentNs, result.DocumentElement.NamespaceURI,
                "A current-namespace native list stays in the current namespace.");
            StringAssert.Contains("http://native.example.com/feed.xml", result.OuterXml,
                "The feed survives the Bandit round-trip.");
        }

        [Test]
        public void Native_ImportFeedlist_SubscribesTheFeed()
        {
            FeedSource handler = CreateEmptyHandler();

            using (Stream s = StreamOf(NativeCurrentNsFeeds))
            {
                handler.ImportFeedlist(s, string.Empty, false);
            }

            Assert.IsTrue(handler.IsSubscribed("http://native.example.com/feed.xml"),
                "A native (current-namespace) list imports its feed into the feedsTable.");
        }

        // -----------------------------------------------------------------------------------
        // 2. OPML import (the important new coverage: ImportFilter -> OPML XSLT).
        // -----------------------------------------------------------------------------------

        [Test]
        public void Opml_IsDetectedAsOpmlByImportFilter()
        {
            // Anchor the format-sniff: ImportFilter keys OPML off the root local-name "opml".
            var filter = new ImportFilter(Doc(OpmlDoc));
            Assert.AreEqual(ImportFeedFormat.OPML, filter.Format,
                "An <opml> root element is detected as the OPML import format.");
        }

        [Test]
        public void Opml_ConvertFeedList_TransformsToNativeFeedsDocument()
        {
            FeedSource handler = CreateEmptyHandler();

            XmlDocument result = handler.ConvertFeedList(Doc(OpmlDoc));

            // The OPML XSLT rewrites the <opml> document into a native <feeds> document whose
            // root sits in the CURRENT (2004) namespace.
            Assert.AreEqual("feeds", result.DocumentElement.LocalName,
                "OPML is transformed to a Bandit <feeds> document.");
            Assert.AreEqual(CurrentNs, result.DocumentElement.NamespaceURI,
                "The OPML XSLT emits feeds in the current (2004) namespace.");

            // The feed's xmlUrl becomes a <link>, and the parent outline becomes its category.
            StringAssert.Contains("http://opml.example.com/feed.xml", result.OuterXml,
                "The OPML outline's xmlUrl is carried over as the feed <link>.");
            StringAssert.Contains("My Category", result.OuterXml,
                "The parent outline title becomes the feed category.");
        }

        [Test]
        public void Opml_ImportFeedlist_SubscribesFeedWithCategoryFromOutline()
        {
            FeedSource handler = CreateEmptyHandler();

            using (Stream s = StreamOf(OpmlDoc))
            {
                handler.ImportFeedlist(s, string.Empty, false);
            }

            const string feedUrl = "http://opml.example.com/feed.xml";
            Assert.IsTrue(handler.IsSubscribed(feedUrl),
                "End-to-end OPML import subscribes the feed described by the outline.");
            Assert.AreEqual("My Category", handler.GetFeeds()[feedUrl].category,
                "The feed lands under the category derived from its parent outline.");
        }

        // -----------------------------------------------------------------------------------
        // 3. Legacy Feeds_v2003 namespace (the back-compat rewrite path).
        // -----------------------------------------------------------------------------------

        [Test]
        public void Legacy2003Namespace_IsDetectedAsBanditByImportFilter()
        {
            var filter = new ImportFilter(Doc(LegacyNsFeeds));
            Assert.AreEqual(ImportFeedFormat.Bandit, filter.Format,
                "The legacy 2003 namespace is still recognized as the Bandit format.");
        }

        [Test]
        public void Legacy2003Namespace_ConvertFeedList_RewritesNamespaceToCurrent()
        {
            FeedSource handler = CreateEmptyHandler();

            // Pre-condition: the input is physically in the historical 2003 namespace.
            XmlDocument input = Doc(LegacyNsFeeds);
            Assert.AreEqual(LegacyNs, input.DocumentElement.NamespaceURI,
                "Pre-condition: the input root is in the 2003 namespace.");

            XmlDocument result = handler.ConvertFeedList(input);

            // RssBanditXmlReader / RssBanditXmlNamespaceResolver rewrite 2003 -> current so the
            // (2004-bound) feeds deserializer will accept the legacy file.
            Assert.AreEqual("feeds", result.DocumentElement.LocalName);
            Assert.AreEqual(CurrentNs, result.DocumentElement.NamespaceURI,
                "The Bandit branch upgrades the legacy 2003 namespace to the current (2004) one.");
            StringAssert.Contains("http://legacy.example.com/feed.xml", result.OuterXml);
        }

        [Test]
        public void Legacy2003Namespace_ImportFeedlist_IsAcceptedAndSubscribes()
        {
            FeedSource handler = CreateEmptyHandler();

            using (Stream s = StreamOf(LegacyNsFeeds))
            {
                handler.ImportFeedlist(s, string.Empty, false);
            }

            Assert.IsTrue(handler.IsSubscribed("http://legacy.example.com/feed.xml"),
                "A legacy 2003-namespace list deserializes (after the ns rewrite) and imports.");
        }

        // -----------------------------------------------------------------------------------
        // 4. Unknown format -> ApplicationException("Unknown Feed Format.").
        // -----------------------------------------------------------------------------------

        [Test]
        public void UnknownFormat_ConvertFeedList_ThrowsApplicationException()
        {
            FeedSource handler = CreateEmptyHandler();

            var ex = Assert.Throws<ApplicationException>(
                () => handler.ConvertFeedList(Doc(UnknownDoc)),
                "A document that is neither OPML/OCS/SIAM nor Bandit is rejected.");
            Assert.AreEqual("Unknown Feed Format.", ex.Message,
                "The rejection message is pinned verbatim.");
        }

        [Test]
        public void UnknownFormat_ImportFeedlist_ThrowsApplicationException()
        {
            FeedSource handler = CreateEmptyHandler();

            // The 3-arg import calls ConvertFeedList directly, so the same exception surfaces.
            using (Stream s = StreamOf(UnknownDoc))
            {
                Assert.Throws<ApplicationException>(
                    () => handler.ImportFeedlist(s, string.Empty, false));
            }
        }

        // -----------------------------------------------------------------------------------
        // 5. Empty / malformed input (pin whatever the code does today).
        // -----------------------------------------------------------------------------------

        [Test]
        public void EmptyXmlDocument_ConvertFeedList_ThrowsNullReference()
        {
            FeedSource handler = CreateEmptyHandler();

            // QUIRK: an empty XmlDocument has no DocumentElement; ImportFilter.DetectFormat
            // dereferences it (DocumentElement.NamespaceURI) -> NullReferenceException, NOT the
            // tidy "Unknown Feed Format." ApplicationException.
            Assert.Throws<NullReferenceException>(
                () => handler.ConvertFeedList(new XmlDocument()));
        }

        [Test]
        public void EmptyStream_ImportFeedlist_ThrowsXmlException()
        {
            FeedSource handler = CreateEmptyHandler();

            // The import loads the stream into an XmlDocument first; an empty stream is not
            // well-formed XML, so the load throws XmlException before ConvertFeedList runs.
            using (Stream s = new MemoryStream(Array.Empty<byte>()))
            {
                Assert.Throws<XmlException>(
                    () => handler.ImportFeedlist(s, string.Empty, false));
            }
        }
    }
}
