using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ExeManager : MonoBehaviour
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

    void OnDestroy() => CloseAll();

    public void LaunchAll()
    {
        if (_pids.Count > 0) return;

        var args = new List<string>();
        if (openAsHost) args.Add("--open-as-host");
        if (hideWindows) args.Add("--hide-windows");

        WindowHelper.SetupEditorHeaderRestoreForSelf();

        var handles = new List<IntPtr>();

        var self = WindowHelper.ResolveSelfWindow();
        if (self != IntPtr.Zero) handles.Add(self);

        for (int i = 0; i < Mathf.Max(0, instances); i++)
        {
            var pid = ExeHelper.Launch(exeName, subfolder, outerFolder, args.ToArray());
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
}