using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Storage;
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
        _unreadCount = Preferences.Default.Get(Key(url), 0); // restore last-known count (survives restarts)
        Node = new FeedNodeViewModel();
    }

    public string Url { get; }
    public FeedNodeViewModel Node { get; }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Unread count shown on the tree. Restored from MAUI Preferences in the ctor and persisted
    /// on every change, so counts survive an app restart. (Separate from <see cref="Node"/>'s count,
    /// which only exists once the feed's items are loaded.)</summary>
    [ObservableProperty]
    private int _unreadCount;
    partial void OnUnreadCountChanged(int value) => Preferences.Default.Set(Key(Url), value);

    /// <summary>Drop a feed's persisted count when it's unsubscribed.</summary>
    public static void Forget(string url) => Preferences.Default.Remove(Key(url));

    private static string Key(string url) => "unread:" + url;
}

/// <summary>A category of feeds (an OPML outline group). Acts as a MAUI CollectionView group.</summary>
public sealed class FeedCategoryGroup : ObservableCollection<SubscriptionItemViewModel>
{
    public FeedCategoryGroup(string name) => Name = name;
    public string Name { get; }
}
