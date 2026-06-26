using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;
using NewsComponents.Feed;
using RssBandit.AppServices.Core;

namespace NewsComponents
{
    /// <summary>
    /// Owns the per-feed and per-category settings accessor engine extracted verbatim from
    /// the <see cref="FeedSource"/> god-object (Concern Q of the FeedSource decomposition;
    /// Slice 1, Task 2). <see cref="FeedSource"/> keeps its public accessors as thin
    /// delegations onto an instance of this class.
    /// </summary>
    /// <remarks>
    /// This is a behavior-preserving move: the engine reads and writes the <em>live</em>
    /// feed/category dictionaries owned by the originating <see cref="FeedSource"/> (passed by
    /// reference, never copied) and falls back to that source's <see cref="ISharedProperty"/>
    /// instance for the top-level defaults. The dictionaries are <c>ConcurrentDictionary</c>
    /// instances read without locks today; that is preserved exactly. None of the surprising
    /// quirks pinned by <c>FeedAndCategorySettingsTests</c> (the <see cref="TimeSpan.MaxValue"/>
    /// silent-clear sentinel, the feed-level inherit-only-for-maxitemage/refreshrate asymmetry,
    /// the lack of a "Specified" flag for string properties) are altered.
    /// </remarks>
    internal sealed class FeedAndCategorySettings : IFeedAndCategorySettings
    {
        /// <summary>The originating FeedSource, supplying the top-level (instance/static) defaults.</summary>
        private readonly ISharedProperty topLevelDefaults;

        /// <summary>The live feeds table owned by the FeedSource (same reference, not a copy).</summary>
        private readonly IDictionary<string, INewsFeed> feedsTable;

        /// <summary>The live categories table owned by the FeedSource (same reference, not a copy).</summary>
        private readonly IDictionary<string, INewsFeedCategory> categories;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedAndCategorySettings"/> class.
        /// </summary>
        /// <param name="topLevelDefaults">
        /// The owning <see cref="FeedSource"/> as its <see cref="ISharedProperty"/> implementation;
        /// supplies the defaults that the original engine read via <c>GetSharedPropertyValue(this, ...)</c>.
        /// </param>
        /// <param name="feeds">The FeedSource's live <c>feedsTable</c> (same reference, not a copy).</param>
        /// <param name="categories">The FeedSource's live <c>categories</c> (same reference, not a copy).</param>
        public FeedAndCategorySettings(
            ISharedProperty topLevelDefaults,
            IDictionary<string, INewsFeed> feeds,
            IDictionary<string, INewsFeedCategory> categories)
        {
            this.topLevelDefaults = topLevelDefaults;
            this.feedsTable = feeds;
            this.categories = categories;
        }

        /// <summary>The live feeds table this component reads/writes; used by the owner to detect a table swap.</summary>
        internal IDictionary<string, INewsFeed> FeedsTable
        {
            get { return feedsTable; }
        }

        /// <summary>The live categories table this component reads/writes; used by the owner to detect a table swap.</summary>
        internal IDictionary<string, INewsFeedCategory> Categories
        {
            get { return categories; }
        }

        #region per-feed accessors

        /// <inheritdoc />
        public void SetMaxItemAge(string feedUrl, TimeSpan age)
        {
            SetFeedProperty(feedUrl, "maxitemage", age);
        }

        /// <inheritdoc />
        public TimeSpan GetMaxItemAge(string feedUrl)
        {
            return (TimeSpan) GetFeedProperty(feedUrl, "maxitemage", true);
        }

        /// <inheritdoc />
        public void SetRefreshRate(string feedUrl, int refreshRate)
        {
            SetFeedProperty(feedUrl, "refreshrate", refreshRate);
        }

        /// <inheritdoc />
        public int GetRefreshRate(string feedUrl)
        {
            return (int) GetFeedProperty(feedUrl, "refreshrate", true);
        }

        /// <inheritdoc />
        public void SetStyleSheet(string feedUrl, string style)
        {
            SetFeedProperty(feedUrl, "stylesheet", style);
        }

        /// <inheritdoc />
        public string GetStyleSheet(string feedUrl)
        {
            return (string) GetFeedProperty(feedUrl, "stylesheet");
        }

        /// <inheritdoc />
        public void SetFeedColumnLayoutID(string feedUrl, string layout)
        {
            SetFeedProperty(feedUrl, "listviewlayout", layout);
        }

        /// <inheritdoc />
        public string GetFeedColumnLayoutID(string feedUrl)
        {
            return (string) GetFeedProperty(feedUrl, "listviewlayout");
        }

        /// <inheritdoc />
        public void SetMarkItemsReadOnExit(string feedUrl, bool markitemsread)
        {
            SetFeedProperty(feedUrl, "markitemsreadonexit", markitemsread);
        }

        /// <inheritdoc />
        public bool GetMarkItemsReadOnExit(string feedUrl)
        {
            return (bool) GetFeedProperty(feedUrl, "markitemsreadonexit");
        }

        #endregion

        #region per-category accessors

        /// <inheritdoc />
        public void SetCategoryMaxItemAge(string category, TimeSpan age)
        {
            SetCategoryProperty(category, "maxitemage", age);
        }

        /// <inheritdoc />
        public TimeSpan GetCategoryMaxItemAge(string category)
        {
            return (TimeSpan) GetCategoryProperty(category, "maxitemage");
        }

        /// <inheritdoc />
        public void SetCategoryRefreshRate(string category, int refreshRate)
        {
            SetCategoryProperty(category, "refreshrate", refreshRate);
        }

        /// <inheritdoc />
        public int GetCategoryRefreshRate(string category)
        {
            return (int) GetCategoryProperty(category, "refreshrate");
        }

        /// <inheritdoc />
        public void SetCategoryStyleSheet(string category, string style)
        {
            SetCategoryProperty(category, "stylesheet", style);
        }

        /// <inheritdoc />
        public string GetCategoryStyleSheet(string category)
        {
            return (string) GetCategoryProperty(category, "stylesheet");
        }

        /// <inheritdoc />
        public void SetCategoryFeedColumnLayoutID(string category, string layout)
        {
            SetCategoryProperty(category, "listviewlayout", layout);
        }

        /// <inheritdoc />
        public string GetCategoryFeedColumnLayoutID(string category)
        {
            return (string) GetCategoryProperty(category, "listviewlayout");
        }

        /// <inheritdoc />
        public void SetCategoryMarkItemsReadOnExit(string category, bool markitemsread)
        {
            SetCategoryProperty(category, "markitemsreadonexit", markitemsread);
        }

        /// <inheritdoc />
        public bool GetCategoryMarkItemsReadOnExit(string category)
        {
            return (bool) GetCategoryProperty(category, "markitemsreadonexit");
        }

        #endregion

        #region engine (moved verbatim from FeedSource)

        /// <summary>
        /// Tests whether a particular propery value is set
        /// </summary>
        /// <param name="value">the value to test</param>
        /// <param name="propertyName">Name of the property to set</param>
        /// <param name="owner">the object which the property comes from</param>
        /// <returns>true if it is set and false otherwise</returns>
        private static bool IsPropertyValueSet(object value, string propertyName, ISharedProperty owner)
        {
            if (value == null)
            {
                return false;
            }

            if (value is string)
            {
                bool isSet = !string.IsNullOrEmpty((string) value);

                if (propertyName.Equals("maxitemage") && isSet)
                {
                    isSet = !value.Equals(XmlConvert.ToString(TimeSpan.MaxValue));
                }

                return isSet;
            }


            return (bool) GetSharedPropertyValue(owner, propertyName + "Specified");
            //return (bool) owner.GetType().GetProperty(propertyName + "Specified").GetValue(owner, null);
        }


        /// <summary>
        /// Gets the value of a feed's property. This does not inherit the properties of parent
        /// categories.
        /// </summary>
        /// <param name="feedUrl">the feed URL</param>
        /// <param name="propertyName">the name of the property</param>
        /// <returns>the value of the property</returns>
        private object GetFeedProperty(string feedUrl, string propertyName)
        {
            return GetFeedProperty(feedUrl, propertyName, false);
        }

        /// <summary>
        /// Gets the value of a feed's property
        /// </summary>
        /// <param name="feedUrl">the feed URL</param>
        /// <param name="propertyName">the name of the property</param>
        /// <param name="inheritCategory">indicates whether the settings from the parent category should be inherited or not</param>
        /// <returns>the value of the property</returns>
        private object GetFeedProperty(string feedUrl, string propertyName, bool inheritCategory)
        {
            object value = GetSharedPropertyValue(topLevelDefaults, propertyName);
            //this.GetType().GetField(propertyName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this);
            if (propertyName.Equals("maxitemage"))
            {
                value = XmlConvert.ToTimeSpan((string) value);
            }

            if (feedsTable.ContainsKey(feedUrl))
            {
                INewsFeed f = feedsTable[feedUrl];
                object f_value = GetSharedPropertyValue(f, propertyName);
                // f.GetType().GetProperty(propertyName).GetValue(f, null);

                if (IsPropertyValueSet(f_value, propertyName, f))
                {
                    if (propertyName.Equals("maxitemage"))
                    {
                        f_value = XmlConvert.ToTimeSpan((string) f_value);
                    }

                    value = f_value;
                }
                else if (inheritCategory && !string.IsNullOrEmpty(f.category))
                {
                    INewsFeedCategory c;
                    categories.TryGetValue(f.category, out c);

                    while (c != null)
                    {
                        object c_value = GetSharedPropertyValue(c, propertyName);
                        // c.GetType().GetProperty(propertyName).GetValue(c, null);

                        if (IsPropertyValueSet(c_value, propertyName, c))
                        {
                            if (propertyName.Equals("maxitemage"))
                            {
                                c_value = XmlConvert.ToTimeSpan((string) c_value);
                            }
                            value = c_value;
                            break;
                        }
                        else
                        {
                            c = c.parent;
                        }
                    } //while
                } //else if(!string.IsNullOrEmpty(f.category))
            } //if(feedsTable.ContainsKey(feedUrl)){


            return value;
        }

        /// <summary>
        /// Sets the value of a feed property.
        /// </summary>
        /// <param name="feedUrl"></param>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        private void SetFeedProperty(string feedUrl, string propertyName, object value)
        {
            //TODO: Make this code more efficient

            if (feedsTable.ContainsKey(feedUrl))
            {
                INewsFeed f = feedsTable[feedUrl];

                if (value is TimeSpan)
                {
                    value = XmlConvert.ToString((TimeSpan) value);
                }
                SetSharedPropertyValue(f, propertyName, value);
                //f.GetType().GetProperty(propertyName).SetValue(f, value, null);

                if ((value != null) && !(value is string))
                {
                    SetSharedPropertyValue(f, propertyName + "Specified", true);
                    //f.GetType().GetProperty(propertyName + "Specified").SetValue(f, true, null);
                }
            }
        }

        /// <summary>
        /// Gets the value of a category's property
        /// </summary>
        /// <param name="category">the category name</param>
        /// <param name="propertyName">the name of the property</param>
        /// <returns>the value of the property</returns>
        private object GetCategoryProperty(string category, string propertyName)
        {
            object value = GetSharedPropertyValue(topLevelDefaults, propertyName);
            //this.GetType().GetField(propertyName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this);
            if (propertyName.Equals("maxitemage"))
            {
                value = XmlConvert.ToTimeSpan((string) value);
            }

            if (!string.IsNullOrEmpty(category))
            {
                INewsFeedCategory c;
                categories.TryGetValue(category, out c);

                while (c != null)
                {
                    object c_value = GetSharedPropertyValue(c, propertyName);
                    //c.GetType().GetProperty(propertyName).GetValue(c, null);

                    if (IsPropertyValueSet(c_value, propertyName, c))
                    {
                        if (propertyName.Equals("maxitemage"))
                        {
                            c_value = XmlConvert.ToTimeSpan((string) c_value);
                        }
                        value = c_value;
                        break;
                    }
                    else
                    {
                        c = c.parent;
                    }
                } //while
            } //if(!string.IsNullOrEmpty(category))


            return value;
        }

        /// <summary>
        /// Sets the value of a category's property.
        /// </summary>
        /// <param name="category">the category's name</param>
        /// <param name="propertyName">the name of the property</param>
        /// <param name="value">the new value</param>
        private void SetCategoryProperty(string category, string propertyName, object value)
        {
            //TODO: Make this code more efficient

            if (!string.IsNullOrEmpty(category))
            {
                //category c = this.Categories.GetByKey(category);

                foreach (category c in categories.Values)
                {
                    //if(c!= null){

                    if (c.Value.Equals(category) || c.Value.StartsWith(category + FeedSource.CategorySeparator))
                    {
                        if (value is TimeSpan)
                        {
                            value = XmlConvert.ToString((TimeSpan) value);
                        }

                        SetSharedPropertyValue(c, propertyName, value);
                        //c.GetType().GetProperty(propertyName).SetValue(c, value, null);

                        if ((value != null) && !(value is string))
                        {
                            SetSharedPropertyValue(c, propertyName + "Specified", true);
                            //c.GetType().GetProperty(propertyName + "Specified").SetValue(c, true, null);
                        }

                        break;
                    } //if(c!= null)
                } //foreach
            } //	if(!string.IsNullOrEmpty(category)){
        }

        private static object GetSharedPropertyValue(ISharedProperty instance, string propertyName)
        {
            switch (propertyName)
            {
                case "maxitemage":
                    return instance.maxitemage;
                case "downloadenclosures":
                    return instance.downloadenclosures;
                case "downloadenclosuresSpecified":
                    return instance.downloadenclosuresSpecified;
                case "enclosurealert":
                    return instance.enclosurealert;
                case "enclosurealertSpecified":
                    return instance.enclosurealertSpecified;
                case "enclosurefolder":
                    return instance.enclosurefolder;
                case "listviewlayout":
                    return instance.listviewlayout;
                case "markitemsreadonexit":
                    return instance.markitemsreadonexit;
                case "markitemsreadonexitSpecified":
                    return instance.markitemsreadonexitSpecified;
                case "refreshrate":
                    return instance.refreshrate;
                case "refreshrateSpecified":
                    return instance.refreshrateSpecified;
                case "stylesheet":
                    return instance.stylesheet;
                default:
                    Debug.Assert(true, "unknown shared property name: " + propertyName);
                    break;
            }
            return null;
        }

        private static void SetSharedPropertyValue(ISharedProperty instance, string propertyName, object value)
        {
        	string strval = value as string;
            switch (propertyName)
            {
                case "maxitemage":
					instance.maxitemage = strval;
                    break;
                case "downloadenclosures":
                    instance.downloadenclosures = (bool) value;
                    break;
                case "downloadenclosuresSpecified":
                    instance.downloadenclosuresSpecified = (bool) value;
                    break;
                case "enclosurealert":
                    instance.enclosurealert = (bool) value;
                    break;
                case "enclosurealertSpecified":
                    instance.enclosurealertSpecified = (bool) value;
                    break;
                case "enclosurefolder":
					instance.enclosurefolder = strval;
                    break;
                case "listviewlayout":
					instance.listviewlayout = strval;
                    break;
                case "markitemsreadonexit":
                    instance.markitemsreadonexit = (bool) value;
                    break;
                case "markitemsreadonexitSpecified":
                    instance.markitemsreadonexitSpecified = (bool) value;
                    break;
                case "refreshrate":
                    instance.refreshrate = (int) value;
                    break;
                case "refreshrateSpecified":
                    instance.refreshrateSpecified = (bool) value;
                    break;
                case "stylesheet":
					instance.stylesheet = strval;
                    break;
                default:
                    Debug.Assert(true, "unknown shared property name: " + propertyName);
                    break;
            }
        }

        #endregion
    }
}
