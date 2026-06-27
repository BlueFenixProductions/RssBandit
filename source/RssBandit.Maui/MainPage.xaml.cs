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
}
