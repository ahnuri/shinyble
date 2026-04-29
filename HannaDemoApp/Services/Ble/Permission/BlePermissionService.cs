namespace HannaDemoApp.Services.Ble.Permission;
public class BlePermissionService : IBlePermissionService
{
    public async Task<bool> EnsurePermissionsAsync()
    {
#if ANDROID
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

        return status == PermissionStatus.Granted;
#else
        // iOS does not need manual permission request
        return true;
#endif
    }
}