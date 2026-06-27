using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using NewsComponents;
using NewsComponents.Feed;
using RssBandit.ViewModels;

namespace RssBandit.Maui;

/// <summary>
/// The first *real* reader view-model: it stands up the actual NewsComponents engine on the device,
/// adds one feed, fetches + parses it over HTTP, and surfaces the parsed items as the shared
/// <see cref="FeedItemViewModel"/>s. This replaces the demo seed -- the items here are real RSS.
/// </summary>
public partial class FeedListViewModel : ObservableObject
{
    // A real https feed for the first slice (add-feed UI + persistence are later slices).
    private const string FeedUrl = "https://hnrss.org/frontpage";

    private FeedSource? _source;

    public FeedNodeViewModel Node { get; } = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = "Tap Refresh to load the feed.";

    public FeedListViewModel()
    {
        try
        {
            SetupEngine();
        }
        catch (Exception ex)
        {
            // Don't take the page down if engine init fails -- surface it in the status line instead.
            Status = "Init failed: " + (ex.InnerException?.Message ?? ex.Message);
        }
    }

    private void SetupEngine()
    {
        // Configure the engine for the device sandbox, once. Mutating the shared Default (rather than a
        // fresh config) keeps its non-null PersistedSettings and disarms the process-wide Lucene singleton
        // via NoIndexing -- search is a later slice. Paths come from the MAUI app sandbox, never %APPDATA%.
        var cfg = (NewsComponentsConfiguration)NewsComponentsConfiguration.Default;
        cfg.SearchIndexBehavior = SearchIndexBehavior.NoIndexing;
        var dataPath = Path.Combine(FileSystem.AppDataDirectory, "RssBandit");
        cfg.UserApplicationDataPath = dataPath;
        cfg.UserLocalApplicationDataPath = dataPath;

        var location = new SubscriptionLocation(Path.Combine(dataPath, "feedlist.xml"));
        var source = FeedSource.CreateFeedSource(1, FeedSourceType.DirectAccess, location, cfg);
        // Don't age-purge items (some feeds carry older dates).
        source.MaxItemAge = TimeSpan.MaxValue.Subtract(TimeSpan.FromDays(1));

        // The refresh completes on a background thread; marshal every bound-collection mutation to the UI.
        source.OnAllAsyncRequestsCompleted += (s, e) =>
        {
            IList<INewsItem> items = source.GetCachedItemsForFeed(FeedUrl);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Node.Items.Clear();
                foreach (var item in items)
                    Node.Items.Add(new FeedItemViewModel(new NewsItemReadStateAdapter(item)));
                Status = items.Count == 0
                    ? "Completed, but no items (check connectivity)."
                    : $"{items.Count} items loaded.";
                IsBusy = false;
            });
        };
        source.OnUpdateFeedException += (s, e) =>
        {
            var ex = e.ExceptionThrown;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Status = "Feed error: " + (ex?.Message ?? "unknown");
                IsBusy = false;
            });
        };

        INewsFeed feed = new NewsFeed { link = FeedUrl, title = "Hacker News" };
        source.AddFeed(feed);
        _source = source;
    }

    [RelayCommand]
    private async Task Refresh()
    {
        var source = _source;
        if (IsBusy || source is null)
            return;

        IsBusy = true;
        Status = "Refreshing…";
        try
        {
            // RefreshFeeds blocks during the queue/header phase -> keep it off the UI thread.
            // Items land via OnAllAsyncRequestsCompleted (background -> UI marshal) after this returns.
            await Task.Run(() => source.RefreshFeeds(true));
        }
        catch (Exception ex)
        {
            Status = "Refresh failed: " + ex.Message;
            IsBusy = false;
        }
    }
}
