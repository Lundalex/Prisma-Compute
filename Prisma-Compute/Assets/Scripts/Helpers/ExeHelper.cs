using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

public static class ExeHelper
{
    public static int Launch(string exeName, string subfolder = null, bool outerFolder = true)
    {
        return Launch(exeName, subfolder, outerFolder, null);
    }

    public static int Launch(string exeName, string subfolder, bool outerFolder, string[] args)
    {
        if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            exeName += ".exe";

        var baseDir = Path.GetDirectoryName(Application.dataPath);
        if (outerFolder && !string.IsNullOrEmpty(baseDir))
            baseDir = Path.GetDirectoryName(baseDir);

        var folder = string.IsNullOrEmpty(subfolder) ? baseDir : Path.Combine(baseDir, subfolder);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            UnityEngine.Debug.LogError("Folder not found: " + folder);
            return -1;
        }

        var exePath = Path.Combine(folder, exeName);
        if (!File.Exists(exePath))
        {
            UnityEngine.Debug.LogError("Executable not found: " + exePath);
            return -1;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = folder,
                UseShellExecute = false,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };

            if (args != null && args.Length > 0)
                psi.Arguments = string.Join(" ", args);

            var p = Process.Start(psi);
            return p != null ? p.Id : -1;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("Failed to launch exe: " + e.Message);
            return -1;
        }
    }

    public static void Close(int pid)
    {
        try
        {
            var p = Process.GetProcessById(pid);
            if (p.HasExited) return;
            try { p.CloseMainWindow(); p.WaitForExit(2000); } catch { }
            if (!p.HasExited) { try { p.Kill(); } catch { } }
        }
        catch { }
    }
}