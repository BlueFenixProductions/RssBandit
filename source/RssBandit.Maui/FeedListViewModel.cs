using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using System.Xml.Xsl;
using NewsComponents;
using NewsComponents.Feed;
using NewsComponents.Formatting;
using RssBandit.ViewModels;

namespace RssBandit.Maui;

/// <summary>
/// The first *real* reader view-model: it stands up the actual NewsComponents engine on the device,
/// adds one feed, fetches + parses it over HTTP, and surfaces the parsed items as the shared
/// <see cref="FeedItemViewModel"/>s. This replaces the demo seed -- the items here are real RSS.
/// </summary>
public partial class FeedListViewModel : ObservableObject
{
    // A full-content https feed for the first slice (add-feed UI + persistence are later slices).
    // Chosen because its items carry the full article body (content:encoded) -- so the detail view
    // renders the actual article in-app, not just a link. (A link-aggregator feed like Hacker News
    // ships only URLs + metadata; a Cloudflare-fronted feed like the .NET blog serves the engine's
    // UA a bot-challenge page instead of RSS. A feedburner feed is reader-friendly and full-content.)
    private const string FeedUrl = "https://feeds.arstechnica.com/arstechnica/index";

    private FeedSource? _source;

    // One formatter reused across items -- it caches the compiled XSLT templates.
    private readonly NewsItemFormatter _formatter = new();

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
            // Render each item's article HTML here on the background thread (XSLT transform is cheap
            // and the compiled template is cached); then marshal only the collection mutation to the UI.
            var built = new List<FeedItemViewModel>(items.Count);
            foreach (var item in items)
            {
                string html;
                try { html = _formatter.ToHtml(string.Empty, item, new XsltArgumentList()); }
                catch { html = item.Content ?? string.Empty; }
                built.Add(new FeedItemViewModel(new NewsItemReadStateAdapter(item), html, item.Link));
            }
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Node.Items.Clear();
                foreach (var vm in built)
                    Node.Items.Add(vm);
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

        INewsFeed feed = new NewsFeed { link = FeedUrl, title = "Ars Technica" };
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
