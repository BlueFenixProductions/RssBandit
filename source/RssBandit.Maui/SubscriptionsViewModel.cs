using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Xsl;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using NewsComponents;
using NewsComponents.Feed;
using NewsComponents.Formatting;
using RssBandit.ViewModels;

namespace RssBandit.Maui;

/// <summary>
/// Owns the NewsComponents engine and presents the subscription tree. On first run it imports the
/// bundled blogroll OPML and persists it as the native feedlist; afterwards it loads the saved feedlist.
/// Feeds are grouped by their OPML category. Each feed is refreshed lazily (when opened), so the app
/// doesn't storm the network refreshing every subscription at once.
/// </summary>
public partial class SubscriptionsViewModel : ObservableObject
{
    private FeedSource? _source;
    private readonly NewsItemFormatter _formatter = new();
    private readonly Dictionary<string, SubscriptionItemViewModel> _byUrl = new();

    public ObservableCollection<FeedCategoryGroup> Categories { get; } = new();

    [ObservableProperty]
    private string _status = "Loading subscriptions…";

    /// <summary>Stand up the engine and build the tree. Idempotent; call from the page's OnAppearing.</summary>
    public async Task InitializeAsync()
    {
        if (_source != null)
            return;

        try
        {
            var cfg = (NewsComponentsConfiguration)NewsComponentsConfiguration.Default;
            cfg.SearchIndexBehavior = SearchIndexBehavior.NoIndexing;
            var dataPath = Path.Combine(FileSystem.AppDataDirectory, "RssBandit");
            Directory.CreateDirectory(dataPath); // SaveFeedList silently no-ops if the dir is missing
            cfg.UserApplicationDataPath = dataPath;
            cfg.UserLocalApplicationDataPath = dataPath;

            var feedlistPath = Path.Combine(dataPath, "feedlist.xml");
            var source = FeedSource.CreateFeedSource(
                1, FeedSourceType.DirectAccess, new SubscriptionLocation(feedlistPath), cfg);
            source.MaxItemAge = TimeSpan.MaxValue.Subtract(TimeSpan.FromDays(1));
            source.OnUpdatedFeed += (s, e) => HandleFeedUpdated(e.UpdatedFeedUri?.ToString());
            source.OnUpdateFeedException += (s, e) => HandleFeedError(e.ExceptionThrown);

            if (File.Exists(feedlistPath))
            {
                // ImportFeedlist (not LoadFeedlist) -- the latter routes the path through WebRequest,
                // which can't parse a bare Android file path. ImportFeedlist reads the stream directly.
                using var fs = File.OpenRead(feedlistPath);
                source.ImportFeedlist(fs);
            }
            else
            {
                // First run: import the bundled blogroll OPML (ImportFeedlist auto-detects OPML), then save.
                using var opml = await FileSystem.OpenAppPackageFileAsync("blogroll.opml");
                using var ms = new MemoryStream();
                await opml.CopyToAsync(ms); // app-package streams aren't seekable; copy to a seekable one
                ms.Position = 0;
                source.ImportFeedlist(ms);
                source.SaveFeedList();
            }

            _source = source;
            BuildGroups();
            Status = $"{_byUrl.Count} feeds in {Categories.Count} categories";
        }
        catch (Exception ex)
        {
            Status = "Load failed: " + (ex.InnerException?.Message ?? ex.Message);
        }
    }

    private void BuildGroups()
    {
        _byUrl.Clear();
        Categories.Clear();

        var groups = _source!.GetFeeds().Values
            .GroupBy(f => string.IsNullOrWhiteSpace(f.category) ? "Uncategorized" : f.category)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var g in groups)
        {
            var group = new FeedCategoryGroup(g.Key);
            foreach (var feed in g.OrderBy(f => f.title, StringComparer.OrdinalIgnoreCase))
            {
                var sub = new SubscriptionItemViewModel(feed.link, feed.title);
                _byUrl[Norm(feed.link)] = sub;
                group.Add(sub);
            }
            Categories.Add(group);
        }
    }

    /// <summary>Refresh a single feed over the network; items arrive via OnUpdatedFeed.</summary>
    public void RefreshFeed(SubscriptionItemViewModel sub)
    {
        var source = _source;
        if (source == null || sub.IsBusy)
            return;

        sub.IsBusy = true;
        Task.Run(() =>
        {
            try { source.AsyncGetItemsForFeed(sub.Url, true, true); }
            catch { MainThread.BeginInvokeOnMainThread(() => sub.IsBusy = false); }
        });
    }

    /// <summary>Subscribe to a feed by url. Returns null on success, or an error message to surface.</summary>
    public string? AddFeed(string? rawUrl)
    {
        var source = _source;
        if (source == null)
            return "Not ready yet.";
        if (string.IsNullOrWhiteSpace(rawUrl))
            return null; // cancelled / empty

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return "Enter a valid http(s) feed URL.";

        var url = uri.ToString();
        if (source.IsSubscribed(url) || _byUrl.ContainsKey(Norm(url)))
            return "Already subscribed to that feed.";

        try
        {
            source.AddFeed(new NewsFeed { link = url, title = url });
            source.SaveFeedList();
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        var sub = new SubscriptionItemViewModel(url, url); // title becomes the channel's after the fetch
        _byUrl[Norm(url)] = sub;
        AddToGroup("Uncategorized", sub);
        UpdateStatus();
        RefreshFeed(sub);
        return null;
    }

    /// <summary>Unsubscribe from a feed and drop it from the tree.</summary>
    public void RemoveFeed(SubscriptionItemViewModel sub)
    {
        var source = _source;
        if (source == null)
            return;

        try
        {
            source.DeleteFeed(sub.Url);
            source.SaveFeedList();
        }
        catch { /* best effort -- still drop it from the UI */ }

        _byUrl.Remove(Norm(sub.Url));
        foreach (var group in Categories.ToList())
        {
            if (group.Remove(sub))
            {
                if (group.Count == 0)
                    Categories.Remove(group);
                break;
            }
        }
        UpdateStatus();
    }

    private void AddToGroup(string categoryName, SubscriptionItemViewModel sub)
    {
        var group = Categories.FirstOrDefault(g => g.Name == categoryName);
        if (group == null)
        {
            group = new FeedCategoryGroup(categoryName);
            Categories.Add(group);
        }
        group.Add(sub);
    }

    private void UpdateStatus() => Status = $"{_byUrl.Count} feeds in {Categories.Count} categories";

    private void HandleFeedUpdated(string? url)
    {
        if (url == null || _source == null || !_byUrl.TryGetValue(Norm(url), out var sub))
            return;

        // Read + format on this background thread, then marshal the bound-collection mutation to the UI.
        IList<INewsItem> items = _source.GetCachedItemsForFeed(sub.Url);
        // After the first fetch the channel's real title is known -- adopt it (an added-by-url feed
        // starts out titled with its url).
        string? channelTitle = _source.GetFeeds().TryGetValue(sub.Url, out var feed) ? feed.title : null;
        var built = items
            .Select(it => new FeedItemViewModel(new NewsItemReadStateAdapter(it), SafeFormat(it), it.Link))
            .ToList();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!string.IsNullOrWhiteSpace(channelTitle))
                sub.Title = channelTitle!;
            sub.Node.Items.Clear();
            foreach (var vm in built)
                sub.Node.Items.Add(vm);
            sub.IsBusy = false;
        });
    }

    private void HandleFeedError(Exception? ex)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // A failed feed (many of the blogroll's 2008-era feeds are long dead) shouldn't hang a spinner.
            foreach (var group in Categories)
                foreach (var sub in group)
                    sub.IsBusy = false;
            Status = "A feed failed: " + (ex?.Message ?? "unknown");
        });
    }

    private string SafeFormat(INewsItem item)
    {
        try { return _formatter.ToHtml(string.Empty, item, new XsltArgumentList()); }
        catch { return item.Content ?? string.Empty; }
    }

    private static string Norm(string? url) => (url ?? string.Empty).TrimEnd('/').ToLowerInvariant();
}
