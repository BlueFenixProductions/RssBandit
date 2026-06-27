using System;
using NewsComponents;

namespace RssBandit.ViewModels
{
    /// <summary>
    /// Production <see cref="IReadStateItem"/> adapter over the engine's <see cref="INewsItem"/>.
    /// Projects <c>Id</c>/<c>Title</c>/<c>Date</c> and delegates <c>BeenRead</c> get/set straight to the
    /// item, so marking read through the view-model writes through to the engine. Reusable by both heads
    /// (the WinForms head wraps its items in this when it pushes the view-models in D2).
    /// </summary>
    public sealed class NewsItemReadStateAdapter : IReadStateItem
    {
        private readonly INewsItem _item;

        public NewsItemReadStateAdapter(INewsItem item)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
        }

        public string Id => _item.Id;

        public string Title => _item.Title;

        public DateTime Date => _item.Date;

        public bool BeenRead
        {
            get => _item.BeenRead;
            set => _item.BeenRead = value;
        }
    }
}
