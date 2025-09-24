using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
namespace MatchfishingApp.Platforms.Android
{
    public class BluetoothScanPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            new (string androidPermission, bool isRuntime)[]
            {
            (global::Android.Manifest.Permission.BluetoothScan, true)
            };
    }

    public class BluetoothConnectPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            new (string androidPermission, bool isRuntime)[]
            {
            (global::Android.Manifest.Permission.BluetoothConnect, true)
            };
    }
}
