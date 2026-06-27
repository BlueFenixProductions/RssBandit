using System;
using RssBandit.ViewModels;

namespace RssBandit.ViewModels.Tests
{
    /// <summary>
    /// In-memory faked core for the presentation-layer tests: a mutable POCO implementing the
    /// <see cref="IReadStateItem"/> seam so the view-models can be exercised without the engine.
    /// </summary>
    internal sealed class FakeReadStateItem : IReadStateItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public bool BeenRead { get; set; }
    }
}
