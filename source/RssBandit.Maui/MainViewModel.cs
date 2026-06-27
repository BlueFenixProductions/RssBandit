using System;
using RssBandit.ViewModels;

namespace RssBandit.Maui;

/// <summary>
/// First-screen view-model. For this slice it seeds a <see cref="FeedNodeViewModel"/> with a few
/// items so the page proves the shared, portable MVVM layer binds and reacts in MAUI. The real
/// engine feed-load (FeedSourceManager, cache, refresh) replaces the seed in a subsequent slice.
/// </summary>
public sealed class MainViewModel
{
    public FeedNodeViewModel Node { get; } = new();

    public MainViewModel()
    {
        var seed = new[]
        {
            new DemoReadStateItem("1", "Welcome to RSS Bandit on .NET MAUI", new DateTime(2026, 6, 27), beenRead: false),
            new DemoReadStateItem("2", "The engine, contracts, and view-models are portable net10.0 now", new DateTime(2026, 6, 26), beenRead: false),
            new DemoReadStateItem("3", "Tap Toggle to flip read state; the unread count reacts live", new DateTime(2026, 6, 25), beenRead: true),
            new DemoReadStateItem("4", "Same FeedNodeViewModel the WinForms head consumes", new DateTime(2026, 6, 24), beenRead: true),
        };

        foreach (var item in seed)
            Node.Items.Add(new FeedItemViewModel(item));
    }
}
