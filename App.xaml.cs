using Microsoft.Extensions.DependencyInjection;

namespace MatchfishingApp;

public partial class App : Application
{
    // ✅ Service locator for VMs that navigate
    public static IServiceProvider? Services { get; set; }

    public App(Pages.Home home)
    {
        InitializeComponent();

        MainPage = new NavigationPage(home)
        {
            BarBackgroundColor = Color.FromArgb("#181C3F")
        };
    }
}

