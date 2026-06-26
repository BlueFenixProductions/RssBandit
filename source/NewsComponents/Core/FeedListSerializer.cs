using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Xsl;
using NewsComponents.Feed;
using NewsComponents.Utils;

namespace NewsComponents
{
    /// <summary>
    /// Owns the feed-list <em>save/serialize</em> engine extracted verbatim from the
    /// <see cref="FeedSource"/> god-object (Slice 3a of the FeedSource decomposition).
    /// <see cref="FeedSource"/> keeps its public <c>SaveFeedList</c> overloads, with the core
    /// 4-arg writer delegating 1:1 onto an instance of this class.
    /// </summary>
    /// <remarks>
    /// This is a behavior-preserving move. The engine reads the <em>live</em> <c>categories</c> and
    /// <c>itemsTable</c> dictionaries owned by the originating <see cref="FeedSource"/> (passed by
    /// reference, never copied). <c>itemsTable</c> is <c>readonly</c> on the owner and never swapped,
    /// but <c>categories</c> is reassigned by <c>ImportFeedlist(replace:true)</c>; the owner handles
    /// that by re-creating this component on a <c>categories</c> <c>ReferenceEquals</c> mismatch
    /// (see <see cref="Categories"/>), so it always serializes against the current category set.
    ///
    /// Every quirk pinned by <c>FeedListSaveTests</c> is reproduced exactly: the OPML branch's
    /// UTF-8-BOM indented <see cref="XmlTextWriter"/>; the native branch's no-BOM
    /// <see cref="StreamWriter"/> deliberately left open (<c>//writer.Close(); DON'T CLOSE STREAM</c>);
    /// the seven forced-false <c>*Specified</c> flags; the <c>AddViewedStory</c> save side effect;
    /// and the <c>categories = null</c> / <c>identities = null</c> empty-element omissions.
    /// </remarks>
    internal sealed class FeedListSerializer : IFeedListSerializer
    {
        /// <summary>The live categories table owned by the FeedSource (same reference, not a copy).</summary>
        private readonly IDictionary<string, INewsFeedCategory> categories;

        /// <summary>The live items table owned by the FeedSource (same reference, not a copy).</summary>
        private readonly IDictionary<string, IFeedDetails> itemsTable;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedListSerializer"/> class.
        /// </summary>
        /// <param name="categories">The FeedSource's live <c>categories</c> table (same reference, not a copy).</param>
        /// <param name="itemsTable">The FeedSource's live <c>itemsTable</c> (same reference, not a copy).</param>
        public FeedListSerializer(
            IDictionary<string, INewsFeedCategory> categories,
            IDictionary<string, IFeedDetails> itemsTable)
        {
            this.categories = categories;
            this.itemsTable = itemsTable;
        }

        /// <summary>The live categories table this component reads; used by the owner to detect a swap.</summary>
        internal IDictionary<string, INewsFeedCategory> Categories
        {
            get { return categories; }
        }

        /// <summary>
        /// Saves the provided feed list to the specified stream.
        /// </summary>
        /// <param name="feedStream">The feedStream to save the feed list to</param>
        /// <param name="format">The format to save the stream as. </param>
        /// <param name="feeds">FeedsCollection containing the feeds to save.
        /// Can contain a subset of the owned feeds collection</param>
        /// <param name="includeEmptyCategories">Set to true, if categories without a contained feed should be included</param>
        /// <exception cref="InvalidOperationException">If anything wrong goes on with XmlSerializer</exception>
        /// <exception cref="ArgumentNullException">If feedStream is null</exception>
        public void WriteFeedList(Stream feedStream, FeedListFormat format, IDictionary<string, INewsFeed> feeds,
                                  bool includeEmptyCategories)
        {
            if (feedStream == null)
                throw new ArgumentNullException("feedStream");

            if (format.Equals(FeedListFormat.OPML))
            {
                var opmlDoc = new XmlDocument();
                opmlDoc.LoadXml("<opml version='1.0'><head /><body /></opml>");

                var categoryTable = new Dictionary<string, XmlElement>(categories.Count);

                foreach (INewsFeed f in feeds.Values)
                {
                    XmlElement outline = opmlDoc.CreateElement("outline");
                    outline.SetAttribute("title", f.title);
                    outline.SetAttribute("xmlUrl", f.link);
                    outline.SetAttribute("type", "rss");
                    outline.SetAttribute("text", f.title);

                    IFeedDetails fi;
                    bool success = itemsTable.TryGetValue(f.link, out fi);

                    if (success)
                    {
                        outline.SetAttribute("htmlUrl", fi.Link);
                        outline.SetAttribute("description", fi.Description);
                    }

                    string category = (f.category ?? String.Empty);

                    XmlElement catnode;
                    if (categoryTable.ContainsKey(category))
                        catnode = categoryTable[category];
                    else
                    {
                        catnode = CreateCategoryHive((XmlElement) opmlDoc.DocumentElement.ChildNodes[1], category);
                        categoryTable.Add(category, catnode);
                    }

                    catnode.AppendChild(outline);
                }

                if (includeEmptyCategories)
                {
                    //add categories, we don't already have
                    foreach (var category in categories.Keys)
                    {
                        CreateCategoryHive((XmlElement) opmlDoc.DocumentElement.ChildNodes[1], category);
                    }
                }

                var opmlWriter = new XmlTextWriter(feedStream, Encoding.UTF8);
                opmlWriter.Formatting = Formatting.Indented;
                opmlDoc.Save(opmlWriter);
            }
            else if (format.Equals(FeedListFormat.NewsHandler) || format.Equals(FeedListFormat.NewsHandlerLite))
            {
                XmlSerializer serializer = XmlHelper.SerializerCache.GetSerializer(typeof (feeds));
                var feedlist = new feeds();

                if (feeds != null)
                {


                    // refactored props that do not need anymore stored in feedlist:
                    feedlist.markitemsreadonexitSpecified = false;
                    feedlist.downloadenclosuresSpecified = false;
                    feedlist.enclosurealertSpecified = false;
                    feedlist.refreshrateSpecified = false;
                    feedlist.createsubfoldersforenclosuresSpecified = false;
                    feedlist.numtodownloadonnewfeedSpecified = false;
                    feedlist.enclosurecachesizeSpecified = false;

                    foreach (var f in feeds.Values)
                    {
                        if (f is NewsFeed)
                            feedlist.feed.Add((NewsFeed) f);
                        else
                            feedlist.feed.Add(new NewsFeed(f));

                        if (itemsTable.ContainsKey(f.link))
                        {
                            IList<INewsItem> items = itemsTable[f.link].ItemsList;

                            // Taken out because it meant that when we sync we lose information
                            // about stuff we've read from other instances of RSS Bandit synced from
                            // if its cache is older than this one.
                            /* f.storiesrecentlyviewed.Clear(); */


                            if (!format.Equals(FeedListFormat.NewsHandlerLite))
                            {
                                foreach (var ri in items)
                                {
                                    if (ri.BeenRead && !f.storiesrecentlyviewed.Contains(ri.Id))
                                    {
                                        //THIS MAY BE SLOW
                                        f.AddViewedStory(ri.Id);
                                    }
                                }
                            } //foreach
                        } //if
                    } //foreach
                } //if(feeds != null)


                var c = new List<category>(categories.Count);
                /* sometimes we get nulls in the arraylist */
                foreach (var cat in categories.Values)
                {
                    if (!string.IsNullOrWhiteSpace(cat.Value))
                    {
                        c.Add(new category(cat));
                    }
                }

                //we don't want to write out empty <categories /> into the schema.
                feedlist.categories = c.Count == 0 ? null : c;

                // saved separately:
                feedlist.identities = null;

                //var ids = new List<UserIdentity>(identities.Values);

                ////we don't want to write out empty <user-identities /> into the schema.
                //feedlist.identities = ids.Count == 0 ? null : ids;


                TextWriter writer = new StreamWriter(feedStream);
                serializer.Serialize(writer, feedlist);
                //writer.Close(); DON'T CLOSE STREAM
            }
        }

        /// <summary>
        /// Parses a feed-list stream into a <see cref="feeds"/> object. Loads the stream into an
        /// <see cref="XmlDocument"/>, normalizes it via <see cref="ConvertFeedList"/> and
        /// deserializes the result. The state-mutating merge stays on <c>FeedSource</c>.
        /// </summary>
        /// <param name="feedlist">The stream containing the feed list.</param>
        /// <returns>The deserialized <see cref="feeds"/> object.</returns>
        public feeds ParseFeedList(Stream feedlist)
        {
            var doc = new XmlDocument();
            doc.Load(feedlist);

            //convert feed list to RSS Bandit format
            doc = ConvertFeedList(doc);

            //load up
            var reader = new XmlNodeReader(doc);
            XmlSerializer serializer = XmlHelper.SerializerCache.GetSerializer(typeof (feeds));
            var myFeeds = (feeds) serializer.Deserialize(reader);
            reader.Close();

            return myFeeds;
        }

        /// <summary>
        /// Converts the input XML document from OCS, OPML or SIAM to the RSS Bandit feed list
        /// format.
        /// </summary>
        /// <param name="doc">The input feed list</param>
        /// <returns>The converted feed list</returns>
        /// <exception cref="ApplicationException">if the feed list format is unknown</exception>
        public XmlDocument ConvertFeedList(XmlDocument doc)
        {
            var importFilter = new ImportFilter(doc);

            XslTransform transform = importFilter.GetImportXsl();

            if (transform != null)
            {
                // We have a format other than Bandit
                // Apply the import filter (transform)
                var temp = new XmlDocument();
                temp.Load(transform.Transform(doc, null));
                doc = temp;
            }
            else
            {
                // see if we have a Bandit format
                if (importFilter.Format == ImportFeedFormat.Bandit)
                {
                    // load and validate the Bandit feed file
                    //validate document
                    var context =
                        new XmlParserContext(null, new RssBanditXmlNamespaceResolver(), null, XmlSpace.None);
                    XmlReader vr = new RssBanditXmlReader(doc.OuterXml, XmlNodeType.Document, context);
                    doc.Load(vr);
                    vr.Close();
                }
                else
                {
                    // We have an unknown format
                    throw new ApplicationException("Unknown Feed Format.", null);
                }
            }

            return doc;
        }

        /// <summary>
        /// Creates an OPML category outline hive (one outline element per category path segment),
        /// reusing existing nodes where possible.
        /// </summary>
        /// <param name="startNode">Node to start with</param>
        /// <param name="category">A category path, e.g. 'Category1\SubCategory1'.</param>
        /// <returns>The leaf category node.</returns>
        /// <remarks>If one category in the path is not found, it will be created.</remarks>
        private static XmlElement CreateCategoryHive(XmlElement startNode, string category)
        {
            if (string.IsNullOrEmpty(category) || startNode == null) return startNode;

            string[] catHives = category.Split(FeedSource.CategorySeparator.ToCharArray());
            XmlElement n;
            bool wasNew = false;

            foreach (var catHive in catHives)
            {
                if (!wasNew)
                {
                    string xpath = "child::outline[@title=" + FeedSource.buildXPathString(catHive) + " and (count(@*)= 1)]";
                    n = (XmlElement)startNode.SelectSingleNode(xpath);
                }
                else
                {
                    n = null;
                }

                if (n == null)
                {
                    n = startNode.OwnerDocument.CreateElement("outline");
                    n.SetAttribute("title", catHive);
                    startNode.AppendChild(n);
                    wasNew = true; // shorten search
                }

                startNode = n;
            } //foreach

            return startNode;
        }
    }
}
