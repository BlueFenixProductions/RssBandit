using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RssBandit.ViewModels;

namespace RssBandit.Maui;

/// <summary>One subscribed feed in the subscription tree: its title, url, a busy flag while it refreshes,
/// and the shared <see cref="FeedNodeViewModel"/> that holds its items (filled lazily when opened).
/// Title is observable so a newly-added feed's title can update from the channel after its first fetch.</summary>
public partial class SubscriptionItemViewModel : ObservableObject
{
    public SubscriptionItemViewModel(string url, string? title)
    {
        Url = url;
        _title = string.IsNullOrWhiteSpace(title) ? url : title!;
        Node = new FeedNodeViewModel();
    }

    public string Url { get; }
    public FeedNodeViewModel Node { get; }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Unread count shown on the tree (from cache on startup, then kept current on refresh /
    /// when leaving the feed). Separate from <see cref="Node"/>'s count, which only exists once the
    /// feed's items are loaded.</summary>
    [ObservableProperty]
    private int _unreadCount;
}

/// <summary>A category of feeds (an OPML outline group). Acts as a MAUI CollectionView group.</summary>
public sealed class FeedCategoryGroup : ObservableCollection<SubscriptionItemViewModel>
{
    public FeedCategoryGroup(string name) => Name = name;
    public string Name { get; }
}
