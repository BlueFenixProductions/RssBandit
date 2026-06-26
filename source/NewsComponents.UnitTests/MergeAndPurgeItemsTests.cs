using System;
using System.Collections.Generic;
using NewsComponents;
using NewsComponents.Collections;
using NewsComponents.Feed;
using NUnit.Framework;

namespace NewsComponents.UnitTests
{
    /// <summary>
    /// Characterization tests pinning the CURRENT behavior of the pure item-reconciliation
    /// function <see cref="FeedSource.MergeAndPurgeItems"/>
    /// (<c>source/NewsComponents/Core/FeedSource.cs</c>, <c>public static</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is Task 1 of "Refresh-loop S1": pin the behavior so a later task can lift the body into a
    /// pure <c>ItemMerger</c> and prove behavior is unchanged. <strong>Tests only — no production change.</strong>
    /// </para>
    /// <para>
    /// The function is <c>public static</c>, so each test calls it directly with hand-built
    /// <see cref="NewsItem"/> fixtures — no handler instance, no async, no WireMock. Item matching is by
    /// <see cref="NewsItem.Equals(NewsItem)"/>, which keys on <c>Id</c> AND owner <c>FeedLink</c>
    /// (the feed's <c>link</c>); so a "match" is forced by giving the old and new items the SAME feed
    /// instance and the SAME <c>Id</c>, and a "miss" by giving them different <c>Id</c>s. The static side
    /// effects (<c>ReceivingNewsChannelServices.ProcessItem</c>, <c>RelationCosmosRemove/AddRange</c>) are
    /// inert here (no channels registered) and are neither mocked nor asserted on.
    /// </para>
    /// <para>
    /// The two subtle distinctions deliberately pinned: (a) <c>Date</c> is carried from the old item
    /// ALWAYS, but <c>BeenRead</c>/<c>FlagStatus</c> are carried ONLY when <c>respectOldItemState</c>;
    /// and (b) a new item whose id is in <c>deletedItems</c> is SUPPRESSED (the "purge" half).
    /// </para>
    /// </remarks>
    [TestFixture]
    public class MergeAndPurgeItemsTests
    {
        private const string FeedLink = "http://example.com/merge-feed.xml";

        /// <summary>A feed whose <c>link</c> drives <see cref="NewsItem.FeedLink"/> for identity matching.</summary>
        private static INewsFeed MakeFeed(string link = FeedLink)
        {
            return new NewsFeed { link = link, title = "Title for " + link, category = null };
        }

        /// <summary>
        /// Builds a <see cref="NewsItem"/> via the 8-arg ctor (mirrors SearchIndexSinkTests ~:114):
        /// (feed, title, link, content, date, subject, id, parentId). The <c>link</c> is deliberately
        /// distinct from the <c>id</c> so the two are not aliased inside the ctor.
        /// </summary>
        /// <remarks>
        /// <c>Enclosures</c> is seeded to the shared empty sentinel exactly as the RSS parser
        /// (<c>RssParser.cs ~:744</c>) and XML deserialization (<c>NewsItem.cs ~:1423</c>) leave a
        /// real, enclosure-free item. The merge reads <c>olditem.Enclosures.Count</c> unguarded on the
        /// matched path, so a non-null list is the genuine caller invariant — not a test convenience.
        /// </remarks>
        private static NewsItem MakeItem(INewsFeed feed, string id, DateTime date)
        {
            return new NewsItem(feed, "Title " + id, "http://example.com/item/" + id, "body",
                date, null, id, null)
            {
                Enclosures = GetList<IEnclosure>.Empty
            };
        }

        private static readonly DateTime OldDate = new DateTime(2020, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime NewDate = new DateTime(2024, 6, 1, 9, 30, 0, DateTimeKind.Utc);

        // ===================================================================================
        // 1. Genuinely-new item: appended to BOTH receivedNewItems and oldItems.
        // ===================================================================================

        [Test]
        public void GenuinelyNewItem_NotInOldAndNotDeleted_IsAppendedToBothLists()
        {
            INewsFeed feed = MakeFeed();
            NewsItem existing = MakeItem(feed, "old-1", OldDate);
            NewsItem fresh = MakeItem(feed, "new-1", NewDate);

            var oldItems = new List<INewsItem> { existing };
            var newItems = new List<INewsItem> { fresh };

            FeedSource.MergeAndPurgeItems(oldItems, newItems, new List<string>(),
                out List<INewsItem> received, onlyKeepNewItems: false, respectOldItemState: false);

            Assert.AreEqual(1, received.Count, "The one genuinely-new item must be reported as received.");
            Assert.AreSame(fresh, received[0]);
            CollectionAssert.Contains(oldItems, fresh, "The genuinely-new item must be added to oldItems.");
            CollectionAssert.Contains(oldItems, existing, "The pre-existing item must remain in oldItems.");
            Assert.AreEqual(2, oldItems.Count);
        }

        // ===================================================================================
        // 2. New item whose id IS in deletedItems: SUPPRESSED (the purge half).
        // ===================================================================================

        [Test]
        public void NewItemWhoseIdIsDeleted_IsSuppressedFromBothLists()
        {
            INewsFeed feed = MakeFeed();
            NewsItem fresh = MakeItem(feed, "deleted-1", NewDate);

            var oldItems = new List<INewsItem>();
            var newItems = new List<INewsItem> { fresh };
            var deleted = new List<string> { "deleted-1" };

            FeedSource.MergeAndPurgeItems(oldItems, newItems, deleted,
                out List<INewsItem> received, onlyKeepNewItems: false, respectOldItemState: false);

            Assert.AreEqual(0, received.Count, "A deleted-id new item must NOT be reported as received.");
            Assert.AreEqual(0, oldItems.Count, "A deleted-id new item must NOT be added to oldItems.");
        }

        // ===================================================================================
        // 3. Matching item, respectOldItemState = true: BeenRead + FlagStatus carried from old.
        // ===================================================================================

        [Test]
        public void MatchingItem_RespectOldItemStateTrue_CarriesBeenReadAndFlagStatus()
        {
            INewsFeed feed = MakeFeed();
            NewsItem olditem = MakeItem(feed, "match-1", OldDate);
            olditem.BeenRead = true;
            olditem.FlagStatus = Flagged.FollowUp;

            NewsItem newitem = MakeItem(feed, "match-1", NewDate);
            newitem.BeenRead = false;
            newitem.FlagStatus = Flagged.None;

            var oldItems = new List<INewsItem> { olditem };
            var newItems = new List<INewsItem> { newitem };

            FeedSource.MergeAndPurgeItems(oldItems, newItems, new List<string>(),
                out List<INewsItem> received, onlyKeepNewItems: false, respectOldItemState: true);

            Assert.AreEqual(0, received.Count, "A matched item is not a 'received new' item.");
            Assert.IsTrue(newitem.BeenRead, "BeenRead must be carried from the old item when respectOldItemState.");
            Assert.AreEqual(Flagged.FollowUp, newitem.FlagStatus,
                "FlagStatus must be carried from the old item when respectOldItemState.");
            Assert.AreEqual(OldDate, newitem.Date, "Date is carried from the old item (always).");
        }

        // ===================================================================================
        // 4. Matching item, respectOldItemState = false: BeenRead/FlagStatus NOT carried,
        //    but Date IS carried (the always-vs-conditional distinction).
        // ===================================================================================

        [Test]
        public void MatchingItem_RespectOldItemStateFalse_DoesNotCarryReadOrFlag_ButCarriesDate()
        {
            INewsFeed feed = MakeFeed();
            NewsItem olditem = MakeItem(feed, "match-2", OldDate);
            olditem.BeenRead = true;
            olditem.FlagStatus = Flagged.FollowUp;

            NewsItem newitem = MakeItem(feed, "match-2", NewDate);
            newitem.BeenRead = false;
            newitem.FlagStatus = Flagged.None;

            var oldItems = new List<INewsItem> { olditem };
            var newItems = new List<INewsItem> { newitem };

            FeedSource.MergeAndPurgeItems(oldItems, newItems, new List<string>(),
                out List<INewsItem> received, onlyKeepNewItems: false, respectOldItemState: false);

            Assert.IsFalse(newitem.BeenRead, "BeenRead must NOT be carried when respectOldItemState is false.");
            Assert.AreEqual(Flagged.None, newitem.FlagStatus,
                "FlagStatus must NOT be carried when respectOldItemState is false.");
            Assert.AreEqual(OldDate, newitem.Date,
                "Date is carried from the old item EVEN when respectOldItemState is false.");
        }

        // ===================================================================================
        // 5a. Comment merge: when new item's CommentCount == NoComments, it inherits old's count.
        // ===================================================================================

        [Test]
        public void MatchingItem_NewItemHasNoCommentCount_InheritsOldCommentCount()
        {
            INewsFeed feed = MakeFeed();
            NewsItem olditem = MakeItem(feed, "comments-1", OldDate);
            olditem.CommentCount = 5;          // a real, previously-fetched count
            olditem.WatchComments = false;     // isolate the NoComments-inherit branch

            NewsItem newitem = MakeItem(feed, "comments-1", NewDate);
            // newitem.CommentCount defaults to NewsItem.NoComments (the feed brought no <slash:comments>).
            Assume.That(newitem.CommentCount, Is.EqualTo(NewsItem.NoComments));

            var oldItems = new List<INewsItem> { olditem };
            var newItems = new List<INewsItem> { newitem };

            FeedSource.MergeAndPurgeItems(oldItems, newItems, new List<string>(),
                out _, onlyKeepNewItems: false, respectOldItemState: false);

            Assert.AreEqual(5, newitem.CommentCount,
                "When the new item reports NoComments, it inherits the old item's CommentCount.");
            Assert.IsFalse(newitem.WatchComments, "WatchComments stays false when the old item did not watch.");
        }

        // ===================================================================================
        // 5b. Comment merge: old item watching + a higher new count -> WatchComments + HasNewComments.
        // ===================================================================================

        [Test]
        public void MatchingItem_OldWatchesAndNewHasMoreComments_SetsWatchAndHasNewComments()
        {
            INewsFeed feed = MakeFeed();
            NewsItem olditem = MakeItem(feed, "comments-2", OldDate);
            olditem.WatchComments = true;
            olditem.HasNewComments = false;
            olditem.CommentCount = 3;

            NewsItem newitem = MakeItem(feed, "comments-2", NewDate);
            newitem.CommentCount = 10;          // strictly greater than old's 3

            var oldItems = new List<INewsItem> { olditem };
            var newItems = new List<INewsItem> { newitem };

            FeedSource.MergeAndPurgeItems(oldItems, newItems, new List<string>(),
                out _, onlyKeepNewItems: false, respectOldItemState: false);

            Assert.IsTrue(newitem.WatchComments,
                "WatchComments must be carried to the new item when the old item watched comments.");
            Assert.IsTrue(newitem.HasNewComments,
                "HasNewComments must be set when the watched old item had fewer comments than the new item.");
            Assert.AreEqual(10, newitem.CommentCount,
                "A real new CommentCount (not NoComments) is preserved, not overwritten by the old count.");
        }

        // ===================================================================================
        // 6a. Enclosure merge: a new enclosure equal to an old one inherits old's Downloaded flag.
        // ===================================================================================

        [Test]
        public void MatchingItem_NewEnclosureEqualsOld_InheritsDownloadedFlag()
        {
            const string encUrl = "http://example.com/media/episode.mp3";
            INewsFeed feed = MakeFeed();

            NewsItem olditem = MakeItem(feed, "enc-1", OldDate);
            var oldEnc = new Enclosure("audio/mpeg", 1234, encUrl, "episode") { Downloaded = true };
            olditem.Enclosures = new List<IEnclosure> { oldEnc };

            NewsItem newitem = MakeItem(feed, "enc-1", NewDate);
            var newEnc = new Enclosure("audio/mpeg", 1234, encUrl, "episode") { Downloaded = false };
            newitem.Enclosures = new List<IEnclosure> { newEnc };

            var oldItems = new List<INewsItem> { olditem };
            var newItems = new List<INewsItem> { newitem };

            FeedSource.MergeAndPurgeItems(oldItems, newItems, new List<string>(),
                out _, onlyKeepNewItems: false, respectOldItemState: false);

            Assert.AreEqual(1, newitem.Enclosures.Count, "No duplicate enclosure should be appended for a match.");
            Assert.IsTrue(newitem.Enclosures[0].Downloaded,
                "A new enclosure equal (by URL) to an old one inherits the old enclosure's Downloaded flag.");
        }

        // ===================================================================================
        // 6b. Enclosure merge: an old enclosure not present on the new item is appended.
        // ===================================================================================

        [Test]
        public void MatchingItem_OldEnclosureMissingFromNew_IsAppendedToNew()
        {
            INewsFeed feed = MakeFeed();

            NewsItem olditem = MakeItem(feed, "enc-2", OldDate);
            var oldOnlyEnc = new Enclosure("audio/mpeg", 999, "http://example.com/media/old-only.mp3", "old")
            { Downloaded = true };
            olditem.Enclosures = new List<IEnclosure> { oldOnlyEnc };

            NewsItem newitem = MakeItem(feed, "enc-2", NewDate);
            newitem.Enclosures = new List<IEnclosure>(); // present but does not contain the old enclosure

            var oldItems = new List<INewsItem> { olditem };
            var newItems = new List<INewsItem> { newitem };

            FeedSource.MergeAndPurgeItems(oldItems, newItems, new List<string>(),
                out _, onlyKeepNewItems: false, respectOldItemState: false);

            Assert.AreEqual(1, newitem.Enclosures.Count,
                "An old enclosure absent from the new item must be appended to the new item.");
            Assert.AreSame(oldOnlyEnc, newitem.Enclosures[0],
                "The appended enclosure is the old enclosure instance itself.");
        }

        // ===================================================================================
        // 7. Matched item is replaced: oldItems holds the NEW instance for that id afterwards.
        // ===================================================================================

        [Test]
        public void MatchingItem_OldInstanceReplacedByNewInstanceInOldItems()
        {
            INewsFeed feed = MakeFeed();
            NewsItem olditem = MakeItem(feed, "replace-1", OldDate);
            NewsItem newitem = MakeItem(feed, "replace-1", NewDate);

            var oldItems = new List<INewsItem> { olditem };
            var newItems = new List<INewsItem> { newitem };

            FeedSource.MergeAndPurgeItems(oldItems, newItems, new List<string>(),
                out _, onlyKeepNewItems: false, respectOldItemState: false);

            Assert.AreEqual(1, oldItems.Count, "The matched id occupies exactly one slot after merge.");
            Assert.AreSame(newitem, oldItems[0],
                "After merge the new instance replaces the old instance for the matched id (by reference).");
            Assert.IsFalse(ReferenceEquals(olditem, oldItems[0]),
                "The old instance must have been removed (the surviving instance is NOT the old one).");
        }

        // ===================================================================================
        // 8. Return value is by reference: newItems when onlyKeepNewItems else oldItems.
        // ===================================================================================

        [Test]
        public void Return_OnlyKeepNewItemsTrue_ReturnsNewItemsListByReference()
        {
            INewsFeed feed = MakeFeed();
            var oldItems = new List<INewsItem> { MakeItem(feed, "ret-old", OldDate) };
            var newItems = new List<INewsItem> { MakeItem(feed, "ret-new", NewDate) };

            List<INewsItem> result = FeedSource.MergeAndPurgeItems(oldItems, newItems, new List<string>(),
                out _, onlyKeepNewItems: true, respectOldItemState: false);

            Assert.AreSame(newItems, result, "onlyKeepNewItems=true must return the newItems list instance.");
        }

        [Test]
        public void Return_OnlyKeepNewItemsFalse_ReturnsOldItemsListByReference()
        {
            INewsFeed feed = MakeFeed();
            var oldItems = new List<INewsItem> { MakeItem(feed, "ret-old", OldDate) };
            var newItems = new List<INewsItem> { MakeItem(feed, "ret-new", NewDate) };

            List<INewsItem> result = FeedSource.MergeAndPurgeItems(oldItems, newItems, new List<string>(),
                out _, onlyKeepNewItems: false, respectOldItemState: false);

            Assert.AreSame(oldItems, result, "onlyKeepNewItems=false must return the oldItems list instance.");
        }

        // ===================================================================================
        // 9. Empty newItems: no received items, oldItems unchanged.
        // ===================================================================================

        [Test]
        public void EmptyNewItems_ProducesNoReceivedItemsAndLeavesOldItemsUnchanged()
        {
            INewsFeed feed = MakeFeed();
            NewsItem existing = MakeItem(feed, "keep-1", OldDate);
            var oldItems = new List<INewsItem> { existing };
            var newItems = new List<INewsItem>();

            FeedSource.MergeAndPurgeItems(oldItems, newItems, new List<string>(),
                out List<INewsItem> received, onlyKeepNewItems: false, respectOldItemState: false);

            Assert.AreEqual(0, received.Count, "No new items means no received items.");
            Assert.AreEqual(1, oldItems.Count, "oldItems must be unchanged when there are no new items.");
            Assert.AreSame(existing, oldItems[0]);
        }
    }
}
