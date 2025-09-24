// ViewModels/HomeViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MatchfishingApp.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MatchfishingApp.Models.ViewModels;

namespace MatchfishingApp.Models.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly MatchDb _db;

    [ObservableProperty] private bool hasActiveMatch;
    [ObservableProperty] private string activeMatchText = "A match is in progress.";

    public HomeViewModel(MatchDb db) => _db = db;

    public async Task RefreshAsync()
    {
        var (m, _) = await _db.LoadActiveAsync();
        HasActiveMatch = m != null;
        if (m != null) ActiveMatchText = "You have an active match. Resume?";
    }

    // IMPORTANT: public + [RelayCommand] so the generator produces DiscardCommand / ResumeCommand
    [RelayCommand]
    public async Task DiscardAsync()
    {
        await _db.DiscardActiveAsync();
        HasActiveMatch = false;
    }

    [RelayCommand]
    public async Task ResumeAsync()
    {
        // NavigationPage-safe resume
        var vm = App.Services.GetRequiredService<MatchDataViewModel>();
        var logger = App.Services.GetRequiredService<ILogger<MatchfishingApp.Pages.MatchTracker>>();
        var db = App.Services.GetRequiredService<MatchfishingApp.Data.MatchDb>();

        var page = new MatchfishingApp.Pages.MatchTracker(vm, logger, db);
        var nav = Application.Current?.MainPage?.Navigation;
        if (nav is not null) await nav.PushAsync(page);
        else throw new InvalidOperationException("No Navigation available.");
    }
}
