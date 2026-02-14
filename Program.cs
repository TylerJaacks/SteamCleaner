using System.Diagnostics.CodeAnalysis;
using Microsoft.Win32;

#pragma warning disable CA1416

namespace SteamCleaner;

internal class InstalledApp(string displayName, string vendor, string installLocation, string uninstallString, string keyPath)
{
    public string DisplayName { get; set; } = displayName;
    public string Vendor { get; set; } = vendor;
    public string InstallLocation { get; set; } = installLocation;
    public string UninstallString { get; set; } = uninstallString;
    public string KeyPath { get; set; } = keyPath;
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
class Program
{
    internal static List<InstalledApp> GetInstalledSteamApps()
    {
        IEnumerable<InstalledApp> installedApps = new List<InstalledApp>();

        const string registryKey32 = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        const string registryKey64 = @"SOFTWARE\WoW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

        var win32AppsCU = GetInstalledApplication(Registry.CurrentUser, registryKey32);
        var win32AppsLM = GetInstalledApplication(Registry.LocalMachine, registryKey32);
        var win64AppsCU = GetInstalledApplication(Registry.CurrentUser, registryKey64);
        var win64AppsLM = GetInstalledApplication(Registry.LocalMachine, registryKey64);

        installedApps = installedApps
            .Concat(win32AppsCU)
            .Concat(win32AppsLM)
            .Concat(win64AppsCU)
            .Concat(win64AppsLM);

        installedApps = installedApps.GroupBy(d => d.DisplayName)
            .Select(g => g.First())
            .ToList();

        return installedApps.OrderBy(o => o.DisplayName).ToList();
    }

    internal static List<InstalledApp> GetInstalledApplication(RegistryKey registryKey, string registryKeyStr)
    {
        List<InstalledApp> installedApps = [];

        using var key = registryKey.OpenSubKey(registryKeyStr, true);

        if (key == null)
            return installedApps;

        foreach (var subKeyName in key.GetSubKeyNames())
        {
            using var subkey = key.OpenSubKey(subKeyName, true);

            var displayName = subkey?.GetValue("DisplayName") as string ?? string.Empty;
            var vendor = subkey?.GetValue("Publisher") as string ?? string.Empty;
            var installLocation = subkey?.GetValue("InstallLocation") as string ?? string.Empty;
            var uninstallString = subkey?.GetValue("UninstallString") as string ?? string.Empty;

            if (!uninstallString.Contains(@"C:\Program Files (x86)\Steam\steam.exe",
                    StringComparison.OrdinalIgnoreCase)) continue;

            if (subkey == null) continue;

            var keyPath = subkey.Name;

            if (string.IsNullOrEmpty(displayName) && string.IsNullOrEmpty(keyPath)) continue;

            installedApps.Add(new InstalledApp(displayName, vendor, installLocation, uninstallString, keyPath));
        }

        return installedApps;
    }

    internal static void RemoveRegistryKey(string keyPath)
    {
        var rootKeyPath = keyPath[..keyPath.IndexOf($"\\", StringComparison.Ordinal)];

        var rootRegistryKey = rootKeyPath switch
        {
            $"HKEY_CURRENT_USER" => Registry.CurrentUser,
            $"HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            $"HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
            $"HKEY_USERS" => Registry.Users,
            $"HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
            _ => null
        };

        if (rootRegistryKey == null)
            return;

        var relativeKeyPath = keyPath[(rootKeyPath.Length + 1)..];

        using var parentKey = rootRegistryKey.OpenSubKey(relativeKeyPath[..relativeKeyPath.LastIndexOf($"\\", StringComparison.Ordinal)], true);

        if (parentKey == null)
            return;

        var keyToDeleteName = relativeKeyPath[(relativeKeyPath.LastIndexOf($"\\", StringComparison.Ordinal) + 1)..];

        try
        {
            parentKey.DeleteSubKeyTree(keyToDeleteName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving registry key: {ex.Message}");
        }
    }

    internal static void SaveRegistryKeyToFile(RegistryKey? registryKey, string name)
    {
        var fileName = $@"D:\Desktop\{name}.reg";

        using var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
        using var streamWriter = new StreamWriter(fileStream);

        streamWriter.WriteLine($"Windows Registry Editor Version 5.00");
        streamWriter.WriteLine($"[{registryKey?.Name}]");

        foreach (var valueName in registryKey?.GetValueNames()!)
        {
            var value = registryKey.GetValue(valueName);

            streamWriter.WriteLine($"\"{valueName}\"=\"{value}\"");
        }
    }

    internal static void BackupRegistryKey(string keyPath, string name)
    {
        var rootKeyPath = keyPath[..keyPath.IndexOf($"\\", StringComparison.Ordinal)];

        var rootRegistryKey = rootKeyPath switch
        {
            $"HKEY_CURRENT_USER" => Registry.CurrentUser,
            $"HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            $"HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
            $"HKEY_USERS" => Registry.Users,
            $"HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
            _ => null
        };

        if (rootRegistryKey == null)
            return;

        var relativeKeyPath = keyPath[(rootKeyPath.Length + 1)..];

        using var parentKey = rootRegistryKey.OpenSubKey(relativeKeyPath[..relativeKeyPath.LastIndexOf($"\\", StringComparison.Ordinal)], true);

        if (parentKey == null)
            return;

        var keyToBackup = relativeKeyPath[(relativeKeyPath.LastIndexOf($"\\", StringComparison.Ordinal) + 1)..];

        try
        {
            SaveRegistryKeyToFile(parentKey.OpenSubKey(keyToBackup, true), name);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving registry key: {ex.Message}");
        }
    }

    internal static void ConfirmRegistryKeyDeletion()
    {
        Console.WriteLine("Are you sure you want to delete the registry keys? (y/n)");

        var input = Console.ReadLine();

        Console.WriteLine(input?.ToLower() == "y" ? "Registry keys will be deleted." : "Registry keys will not be deleted.");

        if (input?.ToLower() == "y")
            return;

        Console.Write("Good Bye!");

        Console.WriteLine("Press any key to exit.");

        Console.ReadKey();

        Environment.Exit(0);
    }

    public static void Main(string[] args)
    {
        ConfirmRegistryKeyDeletion();

        foreach (var item in GetInstalledSteamApps())
        {
            // Display the properties of the installed app
            Console.WriteLine($"DisplayName: {item.DisplayName}");
            Console.WriteLine($"Vendor: {item.Vendor}");
            Console.WriteLine($"InstallLocation: {item.InstallLocation}");
            Console.WriteLine($"UninstallString: {item.UninstallString}");
            Console.WriteLine($"KeyPath: {item.KeyPath}");
            Console.WriteLine();

            // Backup the registry key
            //BackupRegistryKey(item.KeyPath, $"Registry_{item.DisplayName}");
            // Remove the registry key
            RemoveRegistryKey(item.KeyPath);
        }
    }
}