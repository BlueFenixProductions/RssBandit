using System.Collections.Generic;
using NewsComponents.Collections;

namespace NewsComponents
{
    /// <summary>
    /// Owns the <em>merge-and-purge</em> item-reconciliation engine extracted verbatim from the
    /// <see cref="FeedSource"/> god-object (final slice of the FeedSource decomposition).
    /// <see cref="FeedSource"/> keeps its public static <c>MergeAndPurgeItems</c> entry point, which
    /// now delegates 1:1 onto <see cref="Merge"/>.
    /// </summary>
    /// <remarks>
    /// This is a behavior-preserving move. Every quirk pinned by <c>MergeAndPurgeItemsTests</c> is
    /// reproduced exactly: the <c>lock (oldItems)</c> on the caller-supplied list; the
    /// <em>unguarded</em> <c>olditem.Enclosures.Count</c> read (a null <c>Enclosures</c> throws an
    /// <see cref="System.NullReferenceException"/> there by design); and the
    /// <c>ReferenceEquals(newitem.Enclosures, GetList&lt;IEnclosure&gt;.Empty)</c> shared-empty
    /// sentinel check. The two <c>RelationCosmos*</c> helpers and the
    /// <c>ReceivingNewsChannelServices</c> processor remain <c>internal static</c> members of
    /// <see cref="FeedSource"/> (same assembly), reached here via explicit <c>FeedSource.</c>
    /// qualification — no visibility change.
    /// </remarks>
    internal static class ItemMerger
    {
        /// <summary>
        /// Merge and purge items.
        /// </summary>
        /// <param name="oldItems">List with the old items</param>
        /// <param name="newItems">List with the new items</param>
        /// <param name="deletedItems">List with the IDs of deleted items</param>
        /// <param name="receivedNewItems">IList with the really new (received) items.</param>
        /// <param name="onlyKeepNewItems">Indicates that we only want the items from newItems to be kept. If this value is true
        /// then this method merely copies over item state of any oldItems that are in newItems then returns newItems</param>
        /// <param name="respectOldItemState">Indicates that read and flag status from the old items should be respected</param>
        /// <returns>IList merge/purge result</returns>
        public static List<INewsItem> Merge(List<INewsItem> oldItems, List<INewsItem> newItems,
                                            ICollection<string> deletedItems,
                                            out List<INewsItem> receivedNewItems,
                                            bool onlyKeepNewItems, bool respectOldItemState)
        {
            receivedNewItems = new List<INewsItem>();
            //ArrayList removedOldItems = new ArrayList();

            lock (oldItems)
            {
                foreach (NewsItem newitem in newItems)
                {
                    int index = oldItems.IndexOf(newitem);
                    if (index == -1)
                    {
                        if (!deletedItems.Contains(newitem.Id))
                        {
                            receivedNewItems.Add(newitem);
                            oldItems.Add(newitem);
                            //perform whatever processing is needed
                            FeedSource.ReceivingNewsChannelServices.ProcessItem(newitem);
                        }
                    }
                    else
                    {
                        INewsItem olditem = oldItems[index];

                        if (respectOldItemState)
                        {
                            newitem.BeenRead = olditem.BeenRead;
                        }

                        /*
						COMMENTED OUT BECAUSE WE WON'T SAVE NEWLY DOWNLOADED TEXT IF THE
						FEED IS UPDATED WITH THE CODE BELOW.

						//We don't need strings in memory if we've read it. However we have to
						//account for the edge case where the feed list was imported and this was
						//read but hasn't yet been saved to the cache.
						//
						if(!feedListImported && newitem.BeenRead){
							newitem.SetContent((string) null, newitem.ContentType);
						} */
                        newitem.Date = olditem.Date; //so the date is from when it was first fetched

                        if (respectOldItemState)
                        {
                            newitem.FlagStatus = olditem.FlagStatus;
                        }

                        if (olditem.WatchComments)
                        {
                            newitem.WatchComments = true;

                            if ((olditem.HasNewComments) || (olditem.CommentCount < newitem.CommentCount))
                            {
                                newitem.HasNewComments = true;
                            }
                        } //if(olditem.WatchComments)

                        //feed doesn't support <slash:comments>, so we use the existing comment count
                        //in case we previously obtained it by fetching the CommentRssUrl
                        if (newitem.CommentCount == NewsItem.NoComments)
                        {
                            newitem.CommentCount = olditem.CommentCount;
                        }

                        //see if we've downloaded any of the enclosures on the old item
                        if (olditem.Enclosures.Count > 0)
                        {
                            foreach (Enclosure enc in olditem.Enclosures)
                            {
                                int j = newitem.Enclosures.IndexOf(enc);

                                if (j != -1)
                                {
                                    IEnclosure newEnc = newitem.Enclosures[j];
                                    newEnc.Downloaded = enc.Downloaded;
                                }
                                else
                                {
                                    if (ReferenceEquals(newitem.Enclosures, GetList<IEnclosure>.Empty))
                                    {
                                        newitem.Enclosures = new List<IEnclosure>();
                                    }
                                    newitem.Enclosures.Add(enc);
                                }
                            }
                        }

                        oldItems.RemoveAt(index);
                        oldItems.Add(newitem);
                        FeedSource.RelationCosmosRemove(olditem);
                        //	removedOldItems.Add(olditem);
                    }
                } //foreach

                //remove old objects from relation cosmos and add newly downloaded items to relationcosmos
                //FeedSource.RelationCosmosRemoveRange(removedOldItems);
                FeedSource.RelationCosmosAddRange(receivedNewItems);
            } //lock

            if (onlyKeepNewItems)
            {
                return newItems;
            }

            return oldItems;
        }
    }
}
