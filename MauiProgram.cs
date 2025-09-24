using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using MatchfishingApp.Data;
using System.IO;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui;   // make sure this is at the top
using SkiaSharp.Views.Maui.Controls.Hosting;   // for UseSkiaSharp()
using LiveChartsCore.SkiaSharpView.Maui;       // for UseLiveCharts()
using LiveChartsCore;                          // if you use LiveCharts.Configure(...)
using SQLitePCL;
using SkiaSharp.Views.Maui.Controls.Hosting;      // ✅ add this
using LiveChartsCore.SkiaSharpView.Maui;          // (optional but fine to keep)

using LiveChartsCore.SkiaSharpView.Maui;   // for UseLiveCharts()
using SkiaSharp.Views.Maui.Controls.Hosting; // for UseSkiaSharp()
using CommunityToolkit.Maui;
//using Accessibility;



namespace MatchfishingApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()          // must come first in the chain
            .UseMauiCommunityToolkit()
            .UseLiveCharts()            // required by LiveCharts rc5+
            .UseSkiaSharp()            // Skia handlers for the charts
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("Technology - Bold.ttf", "Technology"); 
            });

        // DB path + DI
        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "match.db3");
        builder.Services.AddSingleton(_ => new MatchDb(dbPath));
        builder.Services.AddTransient<Pages.Home>();
        builder.Services.AddTransient<Pages.MatchTracker>();
        builder.Services.AddTransient<Models.ViewModels.HomeViewModel>();
        builder.Services.AddTransient<Models.ViewModels.MatchDataViewModel>();


        var app = builder.Build();

        // ✅ make services visible to VMs
        App.Services = app.Services;

        // optional: ensure tables exist
        var db = app.Services.GetRequiredService<MatchDb>();
        _ = db.InitializeAsync();

        return app;
    }
}