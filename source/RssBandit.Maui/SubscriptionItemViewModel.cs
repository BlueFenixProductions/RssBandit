using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RssBandit.ViewModels;

namespace RssBandit.Maui;

/// <summary>One subscribed feed in the subscription tree: its title, url, a busy flag while it refreshes,
/// and the shared <see cref="FeedNodeViewModel"/> that holds its items (filled lazily when opened).</summary>
public partial class SubscriptionItemViewModel : ObservableObject
{
    public SubscriptionItemViewModel(string url, string? title)
    {
        Url = url;
        Title = string.IsNullOrWhiteSpace(title) ? url : title!;
        Node = new FeedNodeViewModel();
    }

    public string Url { get; }
    public string Title { get; }
    public FeedNodeViewModel Node { get; }

    [ObservableProperty]
    private bool _isBusy;
}

/// <summary>A category of feeds (an OPML outline group). Acts as a MAUI CollectionView group.</summary>
public sealed class FeedCategoryGroup : ObservableCollection<SubscriptionItemViewModel>
{
    public FeedCategoryGroup(string name) => Name = name;
    public string Name { get; }
}
