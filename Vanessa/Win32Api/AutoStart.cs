using System;
using Microsoft.Win32;

namespace Kxnrl.Vanessa.Win32Api;

internal static class AutoStart
{
    internal static void Set(bool enable)
    {
        try
        {
            // Open Base Key.
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64)
                                           .OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);

            if (baseKey == null)
            {
                // wtf?
                Console.WriteLine(@"Cannot find Software\Microsoft\Windows\CurrentVersion\Run");

                return;
            }

            if (enable)

            {
                baseKey.SetValue("NCM-DiscordRpc", $"{AppContext.BaseDirectory}MusicRpc.exe");
                Console.WriteLine("AutoStartup has been set.");
            }
            else
            {
                baseKey.DeleteValue("NCM-DiscordRpc", false);
                Console.WriteLine("AutoStartup has been deleted.");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to set autostartup: {e.Message}");
        }
    }

    public static bool Check()
    {
        try
        {
            // Open Base Key.
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64)
                                           .OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);

            if (baseKey == null)
            {
                // wtf?
                Console.WriteLine(@"Cannot find Software\Microsoft\Windows\CurrentVersion\Run");

                return false;
            }

            var ace = baseKey.GetValue("NCM-DiscordRpc");

            var exe = $"{AppContext.BaseDirectory}MusicRpc.exe";

            return exe.Equals(ace);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to set autostartup: {e.Message}");
        }

        return false;
    }
}
