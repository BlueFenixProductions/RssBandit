using System;
using System.Linq;

namespace RssBandit.Maui;

/// <summary>Form for subscribing to a feed: URL, an optional name, and a category (pick an existing
/// section or type a new one).</summary>
public partial class AddFeedPage : ContentPage
{
    private readonly SubscriptionsViewModel _vm;

    public AddFeedPage(SubscriptionsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        CategoryPicker.ItemsSource = _vm.CategoryNames.ToList();
    }

    async void OnAdd(object sender, EventArgs e)
    {
        // a typed new category wins over the picker selection
        var category = !string.IsNullOrWhiteSpace(NewCategoryEntry.Text)
            ? NewCategoryEntry.Text
            : CategoryPicker.SelectedItem as string;

        var error = _vm.AddFeed(UrlEntry.Text, NameEntry.Text, category);
        if (error != null)
        {
            await DisplayAlert("Add feed", error, "OK");
            return;
        }
        await Navigation.PopAsync();
    }
}
