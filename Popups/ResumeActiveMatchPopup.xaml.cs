using CommunityToolkit.Maui.Views;

namespace MatchfishingApp.Popups;

public partial class ResumeActiveMatchPopup : Popup
{
    public ResumeActiveMatchPopup() => InitializeComponent();

    private void btnResume_Clicked(object sender, EventArgs e) => Close("resume");
    private void btnDiscard_Clicked(object sender, EventArgs e) => Close("discard");
}
