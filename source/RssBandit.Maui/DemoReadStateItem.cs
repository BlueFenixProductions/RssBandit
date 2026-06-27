using System;
using RssBandit.ViewModels;

namespace RssBandit.Maui;

/// <summary>
/// A tiny in-memory <see cref="IReadStateItem"/> so the first screen can bind the shared
/// <see cref="FeedItemViewModel"/> / <see cref="FeedNodeViewModel"/> without the full engine
/// feed-load pipeline (that is a later slice). It proves the portable view-model layer renders
/// and reacts inside a MAUI head, on the same VMs the WinForms head consumes.
/// </summary>
internal sealed class DemoReadStateItem : IReadStateItem
{
    public DemoReadStateItem(string id, string title, DateTime date, bool beenRead)
    {
        Id = id;
        Title = title;
        Date = date;
        BeenRead = beenRead;
    }

    public string Id { get; }
    public string Title { get; }
    public DateTime Date { get; }
    public bool BeenRead { get; set; }
}
