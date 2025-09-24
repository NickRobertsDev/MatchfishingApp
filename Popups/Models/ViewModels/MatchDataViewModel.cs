

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace MatchfishingApp.Models.ViewModels
{
    public partial class MatchDataViewModel : ObservableObject
    {
        [ObservableProperty]
        private MatchData matchData = new();

        public MatchDataViewModel()
        {
            // Pre-populate data only in debug mode
#if DEBUG
            matchData.VenueName = "Test Venue";
            matchData.LakeName = "Test Lake";
            matchData.MatchDurationHours = 2;
            matchData.MatchDurationMinutes = 30;
            matchData.DateTime = DateTime.Now.ToString();
            matchData.Nets[0].NetName = "Keepnet 1";
            matchData.Nets[0].WeightLimit = 11;
#endif
        }
    }
}
