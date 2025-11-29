using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class WorkerManager : MonoBehaviour
{
    [Header("File Location")]
    [SerializeField] string exeName = "OtherApp";
    [SerializeField] string subfolder = "";
    [SerializeField] bool outerFolder = true;

    [Header("Arguments")]
    [SerializeField] bool openAsHost = true;
    [SerializeField] bool hideWindows = false;

    [Header("Launch")]
    [SerializeField] int instances = 3;
    [SerializeField] bool launchOnStart = true;

    [Header("Positioning")]
    [SerializeField] int padding = 0;

    readonly List<int> _pids = new();

    void Start()
    {
        if (launchOnStart) LaunchAll();
    }

    void Update()
    {
        if (_pids.Count == 0) return;

        for (int i = _pids.Count - 1; i >= 0; i--)
        {
            int pid = _pids[i];
            if (!IsProcessAlive(pid))
            {
                _pids.RemoveAt(i);
                LaunchOne();
            }
        }
    }

    void OnDestroy() => CloseAll();

    public void LaunchAll()
    {
        if (_pids.Count > 0) return;

        var args = BuildArgs();

        WindowHelper.SetupEditorHeaderRestoreForSelf();

        var handles = new List<IntPtr>();

        var self = WindowHelper.ResolveSelfWindow();
        if (self != IntPtr.Zero) handles.Add(self);

        for (int i = 0; i < Mathf.Max(0, instances); i++)
        {
            var pid = ExeHelper.Launch(exeName, subfolder, outerFolder, args);
            if (pid > 0) _pids.Add(pid);
        }

        foreach (var pid in _pids)
        {
            var h = WindowHelper.WaitForMainWindow(pid, 8000);
            if (h != IntPtr.Zero) handles.Add(h);
        }

        if (hideWindows)
        {
            WindowHelper.MinimizeWindows(handles.ToArray());
            return;
        }

        WindowHelper.AutoFitWindows(handles.ToArray(), Mathf.Max(0, padding));
    }

    public void CloseAll()
    {
        if (_pids.Count == 0) return;
        foreach (var pid in _pids) ExeHelper.Close(pid);
        _pids.Clear();
    }

    string[] BuildArgs()
    {
        var args = new List<string>();
        if (openAsHost) args.Add("--open-as-host");
        if (hideWindows) args.Add("--hide-windows");
        return args.ToArray();
    }

    void LaunchOne()
    {
        var args = BuildArgs();
        var pid = ExeHelper.Launch(exeName, subfolder, outerFolder, args);
        if (pid <= 0) return;

        _pids.Add(pid);

        if (hideWindows)
        {
            var h = WindowHelper.WaitForMainWindow(pid, 8000);
            if (h != IntPtr.Zero) WindowHelper.MinimizeWindows(new[] { h });
        }
    }

    bool IsProcessAlive(int pid)
    {
        try
        {
            var p = Process.GetProcessById(pid);
            return p != null && !p.HasExited;
        }
        catch
        {
            return false;
        }
    }
}