using System.Linq;
using RssBandit.ViewModels;

namespace RssBandit.Maui;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();

        // Bind the real reader: stands up the engine, adds a feed, fetches + parses it on Refresh.
        BindingContext = new FeedListViewModel();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Auto-load on first appearance so the reader shows real items without a manual tap.
        if (BindingContext is FeedListViewModel vm && vm.Node.Items.Count == 0 && vm.RefreshCommand.CanExecute(null))
            vm.RefreshCommand.Execute(null);
    }

    // Tap an item -> push the article detail (a WebView of the engine-rendered HTML).
    async void OnItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is FeedItemViewModel item)
            await Navigation.PushAsync(new ItemDetailPage(item));
        ((CollectionView)sender).SelectedItem = null; // allow re-tapping the same row
    }
}
