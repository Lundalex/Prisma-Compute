// WindowHelper.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class WindowHelper
{
    const uint SWP_NOZORDER = 0x0004;
    const uint SWP_NOACTIVATE = 0x0010;
    const uint SWP_NOSIZE = 0x0001;
    const uint SWP_NOMOVE = 0x0002;
    const uint SWP_FRAMECHANGED = 0x0020;
    const int SW_RESTORE = 9;
    const int SW_MINIMIZE = 6;
    const int GWL_STYLE = -16;
    const int GWL_EXSTYLE = -20;
    const int WS_CAPTION = 0x00C00000;
    const int WS_THICKFRAME = 0x00040000;
    const int WS_MINIMIZEBOX = 0x00020000;
    const int WS_MAXIMIZEBOX = 0x00010000;
    const int WS_SYSMENU = 0x00080000;
    const int WS_EX_DLGMODALFRAME = 0x00000001;
    const int WS_EX_CLIENTEDGE = 0x00000200;
    const int WS_EX_STATICEDGE = 0x00020000;

    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int nIndex);
    [DllImport("user32.dll")] static extern bool AdjustWindowRectEx(ref RECT lpRect, uint dwStyle, bool bMenu, uint dwExStyle);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int left, top, right, bottom; }

    static readonly Dictionary<IntPtr, (int style, int ex)> _origStyle = new Dictionary<IntPtr, (int, int)>();
    static readonly Dictionary<IntPtr, RECT> _origRect = new Dictionary<IntPtr, RECT>();
#if UNITY_EDITOR
    static bool _hooked;
#endif

    public static IntPtr ResolveSelfWindow()
    {
        var p = Process.GetCurrentProcess();
        var h = p.MainWindowHandle;
        if (h != IntPtr.Zero && IsWindow(h)) return h;

        IntPtr found = IntPtr.Zero;
        var self = p.Id;
        EnumWindows((wnd, l) =>
        {
            if (!IsWindow(wnd) || !IsWindowVisible(wnd)) return true;
            GetWindowThreadProcessId(wnd, out var pid);
            if (pid == self) { found = wnd; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static IntPtr WaitForMainWindow(int pid, int timeoutMs = 8000)
    {
        var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        Process p = null;
        try { p = Process.GetProcessById(pid); } catch { return IntPtr.Zero; }
        try { p.WaitForInputIdle(2000); } catch { }
        var h = p.MainWindowHandle;
        while (h == IntPtr.Zero && DateTime.UtcNow < end)
        {
            Thread.Sleep(50);
            try { h = p.MainWindowHandle; } catch { break; }
        }
        return h;
    }

    public static void AutoFitWindows(IntPtr[] handles, int padding)
    {
        var list = new List<IntPtr>();
        foreach (var h in handles) if (h != IntPtr.Zero && IsWindow(h) && IsWindowVisible(h)) list.Add(h);
        if (list.Count == 0) return;

        foreach (var h in list) MakeBorderless(h);

        int sw = GetSystemMetrics(0);
        int sh = GetSystemMetrics(1);

        int innerX = Math.Max(0, padding);
        int innerY = Math.Max(0, padding);
        int innerW = Math.Max(1, sw - 2 * padding);
        int innerH = Math.Max(1, sh - 2 * padding);

        int k = Mathf.CeilToInt(Mathf.Sqrt(list.Count));
        int cols = k;
        int rows = k;

        int cellW = Math.Max(1, (innerW - (cols - 1) * padding) / cols);
        int cellH = Math.Max(1, (innerH - (rows - 1) * padding) / rows);

        for (int i = 0; i < list.Count; i++)
        {
            var hWnd = list[i];
            int c = i % cols;
            int r = i / cols;

            int x0 = innerX + c * (cellW + padding);
            int y0 = innerY + r * (cellH + padding);

            var client = Fit16x9Inside(cellW, cellH);
            var outer = ClientToOuter(hWnd, client.w, client.h);

            int px = x0 + (cellW - outer.w) / 2;
            int py = y0 + (cellH - outer.h) / 2;

            ShowWindow(hWnd, SW_RESTORE);
            SetWindowPos(hWnd, IntPtr.Zero, px, py, outer.w, outer.h, SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }

    public static void MakeBorderless(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;
        if (!_origStyle.ContainsKey(hWnd))
        {
            int s0 = GetWindowLong(hWnd, GWL_STYLE);
            int e0 = GetWindowLong(hWnd, GWL_EXSTYLE);
            _origStyle[hWnd] = (s0, e0);
            if (GetWindowRect(hWnd, out var r)) _origRect[hWnd] = r;
        }

        int style = GetWindowLong(hWnd, GWL_STYLE);
        int ex = GetWindowLong(hWnd, GWL_EXSTYLE);
        int newStyle = style & ~WS_CAPTION & ~WS_THICKFRAME & ~WS_MINIMIZEBOX & ~WS_MAXIMIZEBOX & ~WS_SYSMENU;
        int newEx = ex & ~WS_EX_DLGMODALFRAME & ~WS_EX_CLIENTEDGE & ~WS_EX_STATICEDGE;
        if (newStyle != style) SetWindowLong(hWnd, GWL_STYLE, newStyle);
        if (newEx != ex) SetWindowLong(hWnd, GWL_EXSTYLE, newEx);
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED);
    }

    public static void RestoreBorder(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;
        if (!_origStyle.TryGetValue(hWnd, out var s)) return;
        SetWindowLong(hWnd, GWL_STYLE, s.style);
        SetWindowLong(hWnd, GWL_EXSTYLE, s.ex);
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED);
        if (_origRect.TryGetValue(hWnd, out var r))
        {
            int w = Math.Max(1, r.right - r.left);
            int h = Math.Max(1, r.bottom - r.top);
            SetWindowPos(hWnd, IntPtr.Zero, r.left, r.top, w, h, SWP_NOZORDER | SWP_NOACTIVATE);
        }
        _origStyle.Remove(hWnd);
        _origRect.Remove(hWnd);
    }

    public static void SetupEditorHeaderRestoreForSelf()
    {
#if UNITY_EDITOR
        if (_hooked) return;
        _hooked = true;
        EditorApplication.playModeStateChanged += (s) =>
        {
            if (s == PlayModeStateChange.ExitingPlayMode || s == PlayModeStateChange.EnteredEditMode)
            {
                var self = ResolveSelfWindow();
                if (self != IntPtr.Zero) RestoreBorder(self);
            }
        };
#endif
    }

    public static void MinimizeWindows(IntPtr[] handles)
    {
        if (handles == null || handles.Length == 0) return;
        for (int i = 0; i < handles.Length; i++)
        {
            var h = handles[i];
            if (h != IntPtr.Zero && IsWindow(h)) ShowWindow(h, SW_MINIMIZE);
        }
    }

    static (int w, int h) Fit16x9Inside(int availW, int availH)
    {
        int w1 = availW;
        int h1 = (int)Math.Floor(w1 / 16.0 * 9.0);
        if (h1 <= availH) return (w1, Math.Max(1, h1));
        int h2 = availH;
        int w2 = (int)Math.Floor(h2 / 9.0 * 16.0);
        return (Math.Max(1, w2), h2);
    }

    static (int w, int h) ClientToOuter(IntPtr hWnd, int clientW, int clientH)
    {
        int style = GetWindowLong(hWnd, GWL_STYLE);
        int ex = GetWindowLong(hWnd, GWL_EXSTYLE);
        var rc = new RECT { left = 0, top = 0, right = clientW, bottom = clientH };
        AdjustWindowRectEx(ref rc, unchecked((uint)style), false, unchecked((uint)ex));
        int w = Math.Max(1, rc.right - rc.left);
        int h = Math.Max(1, rc.bottom - rc.top);
        return (w, h);
    }
}