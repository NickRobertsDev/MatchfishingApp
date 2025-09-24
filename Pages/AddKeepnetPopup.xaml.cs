using CommunityToolkit.Maui.Views;
using MatchfishingApp.Models;
using MatchfishingApp.Models.ViewModels;

namespace MatchfishingApp.Pages;

public partial class AddKeepnetPopup : Popup
{
    private Net _originalNet;

    public AddKeepnetPopup(Net originalNet)
    {
        InitializeComponent();

        // Pre-populate with the first keepnet's data
        _originalNet = originalNet;
        //entryNetName.Text = originalNet.NetName;
        //entryWeightLimit.Text = originalNet.WeightLimit.ToString();
    }

    private void OnSaveClicked(object sender, EventArgs e)
    {
        // Create a new keepnet with the entered data
        var newKeepnet = new Net
        {
            NetName = _originalNet.NetName.ToString(),
            WeightLimit = int.TryParse(_originalNet.WeightLimit.ToString(), out var weightLimit) ? weightLimit : 0
        };

        // Return the new keepnet
        Close(newKeepnet);
    }
}
