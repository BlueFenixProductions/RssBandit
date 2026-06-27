namespace RssBandit.Maui;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();

        // First slice: bind the shared, portable view-models directly. A later slice swaps the
        // seed for the real engine feed-load and moves construction into DI (MauiProgram).
        BindingContext = new MainViewModel();
    }
}
