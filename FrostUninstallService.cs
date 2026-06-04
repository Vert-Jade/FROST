using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace FROST;

internal sealed record FrostUninstallTexts(
    string Preparing,
    string ClosingApp,
    string Shortcuts,
    string Registry,
    string Files,
    string Done,
    string Scheduled)
{
    public static FrostUninstallTexts Default { get; } = new(
        "Préparation de la désinstallation...",
        "Fermeture des instances FROST...",
        "Suppression des raccourcis...",
        "Nettoyage de Windows...",
        "Suppression des fichiers...",
        "Désinstallation terminée.",
        "Les derniers fichiers seront supprimés après fermeture.");
}

internal static class FrostUninstallService
{
    public static string AppDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FROST");

    private const string UninstallShortcutName = "Desinstaller FROST.lnk";
    private const string LegacyUninstallScriptName = "Uninstall-FROST.ps1";

    public static void Run(bool removeUserData, IProgress<(double Pct, string Status)>? progress, FrostUninstallTexts texts)
    {
        string exePath = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? Path.Combine(AppContext.BaseDirectory, "FROST.exe");
        string installDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;

        progress?.Report((5, texts.Preparing));

        progress?.Report((20, texts.ClosingApp));
        CloseOtherFrostInstances(exePath);

        progress?.Report((42, texts.Shortcuts));
        RemoveShortcuts(installDir);

        progress?.Report((62, texts.Registry));
        RemoveRegistryKey();

        progress?.Report((82, texts.Files));
        TryDeleteFile(Path.Combine(installDir, LegacyUninstallScriptName));
        TryDeleteFile(Path.Combine(installDir, UninstallShortcutName));

        ScheduleFinalFileRemoval(exePath, installDir, removeUserData);

        progress?.Report((96, texts.Scheduled));
        Thread.Sleep(250);
        progress?.Report((100, texts.Done));
    }

    private static void CloseOtherFrostInstances(string currentExePath)
    {
        int currentProcessId = Environment.ProcessId;
        foreach (Process process in Process.GetProcessesByName("FROST"))
        {
            try
            {
                if (process.Id == currentProcessId)
                    continue;

                string? otherPath = null;
                try { otherPath = process.MainModule?.FileName; } catch { }
                if (!string.IsNullOrWhiteSpace(otherPath) &&
                    !PathsEqual(otherPath, currentExePath))
                {
                    continue;
                }

                if (process.CloseMainWindow() && process.WaitForExit(2500))
                    continue;

                process.Kill(entireProcessTree: true);
                process.WaitForExit(2500);
            }
            catch
            {
                // Best effort: Windows may deny access for a process that is already closing.
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void RemoveShortcuts(string installDir)
    {
        string desktopShortcut = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "FROST.lnk");
        string startMenuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs",
            "FROST");
        string uninstallShortcut = Path.Combine(installDir, UninstallShortcutName);

        TryDeleteFile(desktopShortcut);
        TryDeleteFile(uninstallShortcut);
        TryDeleteDirectory(startMenuDir);
    }

    private static void RemoveRegistryKey()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\FROST",
                throwOnMissingSubKey: false);
        }
        catch
        {
            // Non-fatal: the app files can still be removed.
        }
    }

    private static void ScheduleFinalFileRemoval(string exePath, string installDir, bool removeUserData)
    {
        string appDataDir = AppDataDirectory;
        List<string> commands =
        [
            "ping 127.0.0.1 -n 3 > nul",
            $"del /f /q {CmdQuote(exePath)} > nul 2> nul",
            $"del /f /q {CmdQuote(Path.Combine(installDir, LegacyUninstallScriptName))} > nul 2> nul",
            $"del /f /q {CmdQuote(Path.Combine(installDir, UninstallShortcutName))} > nul 2> nul"
        ];

        if (!PathsEqual(installDir, appDataDir))
        {
            commands.Add($"rmdir /s /q {CmdQuote(installDir)} > nul 2> nul");
        }

        if (removeUserData)
        {
            commands.Add($"rmdir /s /q {CmdQuote(appDataDir)} > nul 2> nul");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/d /c " + string.Join(" & ", commands),
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false
        });
    }

    private static string CmdQuote(string value)
    {
        return "\"" + value.Replace("\"", string.Empty) + "\"";
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch { }
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            string normalizedLeft = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedRight = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
