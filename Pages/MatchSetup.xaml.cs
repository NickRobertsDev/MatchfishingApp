namespace MatchfishingApp.Pages;
using Microsoft.Extensions.DependencyInjection; // Add this at the top
using Microsoft.Maui.Controls;
using MatchfishingApp.Models.ViewModels;
using Microsoft.Extensions.Logging;
using MatchfishingApp.Data;

public partial class MatchSetup : ContentPage
{
	public MatchSetup(MatchDataViewModel matchDataViewModel)
    {
		InitializeComponent();
        BindingContext = matchDataViewModel;
    }

    private async void btnSetupNext_Clicked(object sender, EventArgs e)
    {
        try
        {
            var matchDataViewModel = (MatchDataViewModel)BindingContext;

            var logger = App.Services.GetService<ILogger<MatchTracker>>(); // <- optional
            var db = App.Services.GetRequiredService<MatchDb>();

            var matchTracker = new Pages.MatchTracker(matchDataViewModel, logger, db);
            await Navigation.PushAsync(matchTracker);
        }
        catch (Exception ex)
        {
            // Show the inner exception to see the real root cause if anything else is wrong
            await DisplayAlert("Match Setup Error", ex.ToString(), "OK");
        }
    }


}