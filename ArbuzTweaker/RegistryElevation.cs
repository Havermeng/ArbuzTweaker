using System;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

namespace ArbuzTweaker;

/// <summary>
/// Пишет значения в разделы HKLM, которыми владеет TrustedInstaller и куда администратору
/// запись запрещена (например ActivatableClassId GameBarPresenceWriter на урезанных сборках
/// Windows). Раздел временно берётся во владение, админам выдаётся доступ, значение пишется,
/// затем исходный владелец (TrustedInstaller) и права восстанавливаются один в один.
/// Так же поступает оригинальный apply-registry.ps1 из PC-Tuning через MinSudo --TrustedInstaller.
/// </summary>
internal static class RegistryElevation
{
    private const AccessControlSections OwnerAndDacl =
        AccessControlSections.Owner | AccessControlSections.Access;

    public static bool SetValue(string keyPath, string name, object value, RegistryValueKind kind)
        => WithTemporaryOwnership(keyPath, key => key.SetValue(name, value, kind));

    public static bool DeleteValue(string keyPath, string name)
        => WithTemporaryOwnership(keyPath, key =>
        {
            if (key.GetValue(name) != null)
                key.DeleteValue(name, false);
        });

    private static bool WithTemporaryOwnership(string keyPath, Action<RegistryKey> write)
    {
        EnablePrivilege("SeTakeOwnershipPrivilege");
        EnablePrivilege("SeRestorePrivilege");

        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        string? originalSddl = null;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default);

            // 1. Запоминаем исходных владельца и права, чтобы вернуть их без изменений.
            using (var readKey = baseKey.OpenSubKey(keyPath, RegistryKeyPermissionCheck.ReadSubTree, RegistryRights.ReadPermissions))
            {
                if (readKey == null)
                    return false;

                originalSddl = readKey.GetAccessControl(OwnerAndDacl).GetSecurityDescriptorSddlForm(OwnerAndDacl);
            }

            // 2. Берём раздел во владение (владельцем становится группа «Администраторы»).
            using (var ownKey = baseKey.OpenSubKey(keyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.TakeOwnership))
            {
                if (ownKey == null)
                    return false;

                var ownerSecurity = new RegistrySecurity();
                ownerSecurity.SetOwner(administrators);
                ownKey.SetAccessControl(ownerSecurity);
            }

            // 3. Выдаём администраторам полный доступ, чтобы можно было записать значение.
            using (var permissionKey = baseKey.OpenSubKey(keyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions | RegistryRights.ReadPermissions))
            {
                if (permissionKey == null)
                    return false;

                var security = permissionKey.GetAccessControl(AccessControlSections.Access);
                security.AddAccessRule(new RegistryAccessRule(
                    administrators,
                    RegistryRights.FullControl,
                    InheritanceFlags.None,
                    PropagationFlags.None,
                    AccessControlType.Allow));
                permissionKey.SetAccessControl(security);
            }

            // 4. Собственно запись.
            using (var writeKey = baseKey.OpenSubKey(keyPath, true))
            {
                if (writeKey == null)
                    return false;

                write(writeKey);
            }

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            RestoreSecurity(keyPath, originalSddl);
        }
    }

    private static void RestoreSecurity(string keyPath, string? originalSddl)
    {
        if (originalSddl == null)
            return;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default);
            using var restoreKey = baseKey.OpenSubKey(
                keyPath,
                RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.TakeOwnership | RegistryRights.ChangePermissions);
            if (restoreKey == null)
                return;

            // SeRestorePrivilege позволяет вернуть владельцем TrustedInstaller.
            var original = new RegistrySecurity();
            original.SetSecurityDescriptorSddlForm(originalSddl, OwnerAndDacl);
            restoreKey.SetAccessControl(original);
        }
        catch
        {
            // Восстановление прав — лучшая попытка: значение уже записано, откат сработает.
        }
    }

    private static void EnablePrivilege(string privilege)
    {
        var process = GetCurrentProcess();
        if (!OpenProcessToken(process, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var token))
            return;

        try
        {
            if (!LookupPrivilegeValue(null, privilege, out var luid))
                return;

            var privileges = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = SE_PRIVILEGE_ENABLED
            };

            AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero);
        }
        finally
        {
            CloseHandle(token);
        }
    }

    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint SE_PRIVILEGE_ENABLED = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID Luid;
        public uint Attributes;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupPrivilegeValue(string? host, string name, out LUID luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr tokenHandle,
        bool disableAllPrivileges,
        ref TOKEN_PRIVILEGES newState,
        uint bufferLength,
        IntPtr previousState,
        IntPtr returnLength);
}
