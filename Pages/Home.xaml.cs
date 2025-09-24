namespace MatchfishingApp.Pages;

using MatchfishingApp.Models.ViewModels; // HomeViewModel + MatchDataViewModel
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MatchfishingApp.Data;


public partial class Home : ContentPage
{
    private readonly HomeViewModel _vm;
    private readonly IServiceProvider _sp;

    // Inject IServiceProvider so we can resolve what we need here
    public Home(HomeViewModel vm, IServiceProvider sp)
    {
        InitializeComponent();
        _vm = vm;
        _sp = sp;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.RefreshAsync();
    }


    private async void OnNewMatchClicked(object sender, EventArgs e)
    {
        // Create a new MatchDataViewModel instance
        var matchDataViewModel = new MatchDataViewModel();

        // Navigate to the MatchSetupPage, passing the view model
        await Navigation.PushAsync(new MatchSetup(matchDataViewModel));
    }

    private async void OnResumeActiveMatchClicked(object sender, EventArgs e)
    {
        var mvm = _sp.GetRequiredService<MatchfishingApp.Models.ViewModels.MatchDataViewModel>();
        var logger = _sp.GetRequiredService<ILogger<MatchfishingApp.Pages.MatchTracker>>();
        var db = _sp.GetRequiredService<MatchDb>();

        var page = new MatchfishingApp.Pages.MatchTracker(mvm, logger, db);
        await Navigation.PushAsync(page);
    }

    private async void OnDiscardActiveMatchClicked(object sender, EventArgs e)
        => await _vm.DiscardAsync(); // stays in VM; it already has MatchDb
}
