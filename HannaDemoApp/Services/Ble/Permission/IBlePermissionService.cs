namespace HannaDemoApp.Services.Ble.Permission;
public interface IBlePermissionService
{
    Task<bool> EnsurePermissionsAsync();
}