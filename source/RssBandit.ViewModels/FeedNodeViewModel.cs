using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RssBandit.ViewModels
{
    /// <summary>
    /// Portable presentation model for a feed node (a feed). Holds an observable collection of
    /// <see cref="FeedItemViewModel"/> children and exposes <see cref="UnreadCount"/>, recomputed as
    /// the number of unread children. It subscribes to each child's read-state changes and to its own
    /// collection changes so the count stays current.
    /// <para>
    /// This mirrors the engine's <c>count(!BeenRead)</c> over a feed's items only; it deliberately does
    /// NOT add tree-rollup (descendant-aggregation) semantics, which are WinForms-tree mechanics and out
    /// of scope for the portable presentation layer.
    /// </para>
    /// </summary>
    public partial class FeedNodeViewModel : ObservableObject
    {
        /// <summary>The child item view-models for this node.</summary>
        public ObservableCollection<FeedItemViewModel> Items { get; }

        /// <summary>The number of unread children.</summary>
        [ObservableProperty]
        private int _unreadCount;

        public FeedNodeViewModel(IEnumerable<FeedItemViewModel>? items = null)
        {
            Items = new ObservableCollection<FeedItemViewModel>();
            Items.CollectionChanged += OnItemsCollectionChanged;

            if (items != null)
            {
                foreach (var item in items)
                {
                    Items.Add(item);
                }
            }

            RecomputeUnreadCount();
        }

        private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (FeedItemViewModel child in e.OldItems)
                {
                    child.PropertyChanged -= OnChildPropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (FeedItemViewModel child in e.NewItems)
                {
                    child.PropertyChanged += OnChildPropertyChanged;
                }
            }

            RecomputeUnreadCount();
        }

        private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FeedItemViewModel.IsRead))
            {
                RecomputeUnreadCount();
            }
        }

        private void RecomputeUnreadCount() => UnreadCount = Items.Count(i => !i.IsRead);
    }
}
