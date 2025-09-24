using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Android.Views;
using CommunityToolkit.Mvvm.Messaging;
using System;

namespace MatchfishingApp
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        public override bool DispatchKeyEvent(KeyEvent e)
        {
            var desc = $"{e.KeyCode}";

            if (e.Action == KeyEventActions.Down && e.RepeatCount == 0)
            {
                    WeakReferenceMessenger.Default.Send(new KeyPressedMessage(desc));
                    return true;
            }

            return base.DispatchKeyEvent(e);
        }

    }



}
