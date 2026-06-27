using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RssBandit.ViewModels
{
    /// <summary>
    /// Portable presentation model for a single feed item. Wraps an <see cref="IReadStateItem"/> and
    /// exposes its read-state as an observable <see cref="IsRead"/> property with a write-through to
    /// the model, plus a <see cref="ToggleReadCommand"/>. The WinForms head uses zero data binding for
    /// read-state (it pushes imperatively), so this is a testable presentation model a future MAUI head
    /// binds to and the WinForms head pushes from (D2) — not a passive bound source.
    /// </summary>
    public partial class FeedItemViewModel : ObservableObject
    {
        private readonly IReadStateItem _item;

        public FeedItemViewModel(IReadStateItem item, string? html = null, string? link = null)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _isRead = item.BeenRead;
            Html = html ?? string.Empty;
            Link = link;
        }

        /// <summary>
        /// Pre-rendered article HTML for the detail view (the engine renders this in the host layer,
        /// keeping this portable model engine-free). Empty when not supplied.
        /// </summary>
        public string Html { get; }

        /// <summary>The item's source link, used as the detail WebView's base URL. Null when not supplied.</summary>
        public string? Link { get; }

        /// <summary>The unique identifier of the item, projected from the model.</summary>
        public string Id => _item.Id;

        /// <summary>The title of the item, projected from the model.</summary>
        public string Title => _item.Title;

        /// <summary>The date of the item, projected from the model.</summary>
        public DateTime Date => _item.Date;

        /// <summary>
        /// Observable read-state. Changes write through to the underlying model's <c>BeenRead</c>.
        /// </summary>
        [ObservableProperty]
        private bool _isRead;

        partial void OnIsReadChanged(bool value) => _item.BeenRead = value;

        /// <summary>Flips the read-state (always executable).</summary>
        [RelayCommand]
        private void ToggleRead() => IsRead = !IsRead;
    }
}
