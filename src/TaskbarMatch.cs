// TaskbarMatch 1.1.2 beta - Windows 11 x64 taskbar edge-color extension.
// SPDX-License-Identifier: GPL-3.0-or-later
// All TaskbarMatch runtime source is embedded in its single .cmd launcher.
// Backend: embedded TaskbarMatch.Xaml. Background and contrast share one XAML connection; flyouts remain native.
// No global accent changes, overlays, full-screen captures, or keyboard hooks.
// Scheduling targets are not measured end-to-end latency guarantees.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TaskbarMatch
{
    internal static class Program
    {
        internal static readonly string Root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskbarMatch");
        internal static readonly string Sid = WindowsIdentity.GetCurrent().User.Value;
        internal static readonly string StopName = "Local\\TaskbarMatch.Stop." + Sid;
        internal static readonly string MutexName = "Local\\TaskbarMatch.Run." + Sid;

        [STAThread]
        private static void Main(string[] args)
        {
            // The manifest already requests PerMonitorV2. The fallback is useful
            // if the executable is rebuilt without embedding that manifest.
            try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);

            bool ownsMutex = false;
            Mutex single = null;
            AppController controller = null;
            try
            {
                Core.SelfTest();
                Contrast.SelfTest();
                if (args.Length > 0 && args[0] == "--selftest") { return; }
                single = new Mutex(false, MutexName);
                try { ownsMutex = single.WaitOne(0); }
                catch (AbandonedMutexException) { ownsMutex = true; }
                if (!ownsMutex)
                {
                    try
                    {
                        using (EventWaitHandle stop = EventWaitHandle.OpenExisting(StopName)) { stop.Set(); }
                    }
                    catch (WaitHandleCannotBeOpenedException) { }
                    return;
                }
                if (!Environment.Is64BitProcess)
                    throw new InvalidOperationException("TaskbarMatch requires x64 Windows 11.");
                int session = Process.GetCurrentProcess().SessionId;
                foreach (Process p in Process.GetProcessesByName("TranslucentTB"))
                {
                    using (p)
                    {
                        try
                        {
                            if (p.SessionId == session)
                                throw new InvalidOperationException("Close TranslucentTB before starting TaskbarMatch. Two taskbar color controllers would conflict.");
                        }
                        catch (Win32Exception) { }
                    }
                }
                Directory.CreateDirectory(Root);
                Application.OleRequired();
                controller = new AppController();
                Application.Run(controller);
            }
            catch (Exception ex)
            {
                if (controller != null) { controller.Shutdown(); }
                Environment.ExitCode = 1;
                Log(ex.ToString());
                MessageBox.Show(ex.Message + "\r\n\r\nDetails: " + Path.Combine(Root, "runtime.log"),
                    "TaskbarMatch stopped", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (controller != null) { controller.Dispose(); }
                if (ownsMutex && single != null) { single.ReleaseMutex(); }
                if (single != null) { single.Dispose(); }
            }
        }

        internal static void Log(string text)
        {
            try
            {
                Directory.CreateDirectory(Root);
                string file = Path.Combine(Root, "runtime.log");
                if (File.Exists(file) && new FileInfo(file).Length > 131072)
                    File.WriteAllText(file, "Log rotated.\r\n");
                File.AppendAllText(file, DateTime.Now.ToString("s") + "  " + text + "\r\n");
            }
            catch { }
        }
    }

    internal sealed class AppController : ApplicationContext
    {
        private const int ContentPollMs = 250;
        private const int EventIntervalMs = 40;
        private const int SettleMs = 240;
        private const int WindowRefreshMs = 1000;
        private const int TopologyRefreshMs = 2000;
        private const int ReapplyMs = 2000;
        private const int WM_REFRESH = 0x8001;
        private const int WM_CLOSE = 0x0010;
        private const int WM_WTSSESSION_CHANGE = 0x02B1;
        private const int WM_POWERBROADCAST = 0x0218;
        private static readonly Guid DisplayStateGuid = new Guid("6fe69556-704a-47a0-8f24-c28d936fda47");

        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly MessageSink sink;
        private readonly NotifyIcon tray;
        private readonly ContextMenuStrip menu;
        private readonly ToolStripMenuItem pauseItem;
        private readonly ToolStripMenuItem startupItem;
        private readonly System.Windows.Forms.Timer contentTimer;
        private readonly System.Windows.Forms.Timer eventTimer;
        private readonly EventWaitHandle stopEvent;
        private readonly RegisteredWaitHandle stopWait;
        private readonly Native.WinEventProc eventProc;
        private readonly List<IntPtr> eventHooks = new List<IntPtr>();
        private readonly TapBackend backend = new TapBackend();
        private readonly List<TaskbarState> taskbars = new List<TaskbarState>();
        private readonly List<WindowInfo> windows = new List<WindowInfo>();
        private readonly Queue<long> shellRestarts = new Queue<long>();
        private readonly uint ownPid = (uint)Process.GetCurrentProcess().Id;
        private readonly uint taskbarCreated;
        private readonly IntPtr powerNotification;

        private bool closed;
        private bool paused;
        private bool sessionLocked;
        private bool displayOff;
        private bool suspended;
        private bool busy;
        private bool refreshPosted;
        private bool windowsDirty = true;
        private bool topologyDirty = true;
        private bool forceApply = true;
        private bool announced;
        private long lastUpdate = -10000;
        private long lastWindowScan = -10000;
        private long lastTopologyScan = -10000;
        private long settleUntil;
        private long nextConnect;
        private long connectStartedAt = -1;
        private long updateCount;
        private long appliedCount;
        private long captureCount;
        private long samplingFailures;
        private long updateTicks;
        private uint explorerPid;
        private int connectionFailures;

        internal AppController()
        {
            sink = new MessageSink(OnMessage);
            backend.ControllerHandle = sink.Handle;
            taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
            menu = new ContextMenuStrip();
            ToolStripMenuItem title = new ToolStripMenuItem("TaskbarMatch");
            title.Enabled = false;
            menu.Items.Add(title);
            menu.Items.Add(new ToolStripSeparator());
            pauseItem = new ToolStripMenuItem("Pause and restore");
            pauseItem.CheckOnClick = true;
            pauseItem.Click += delegate
            {
                paused = pauseItem.Checked;
                if (paused) { RestoreAll(); }
                else { QueueRefresh(true, true); }
                tray.Text = paused ? "TaskbarMatch: paused" : "TaskbarMatch: matching window edges";
            };
            menu.Items.Add(pauseItem);
            ToolStripMenuItem refresh = new ToolStripMenuItem("Refresh now");
            refresh.Click += delegate { QueueRefresh(true, true); };
            menu.Items.Add(refresh);
            startupItem = new ToolStripMenuItem("Start with Windows");
            startupItem.Checked = StartupEnabled();
            startupItem.Click += delegate { ToggleStartup(); };
            menu.Items.Add(startupItem);
            ToolStripMenuItem diagnostics = new ToolStripMenuItem("Diagnostics");
            diagnostics.Click += delegate { ShowDiagnostics(); };
            menu.Items.Add(diagnostics);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem exit = new ToolStripMenuItem("Exit and restore taskbars");
            exit.Click += delegate { ExitThread(); };
            menu.Items.Add(exit);
            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Application;
            tray.Text = "TaskbarMatch: connecting to taskbar";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.DoubleClick += delegate { ShowDiagnostics(); };

            eventTimer = new System.Windows.Forms.Timer();
            eventTimer.Interval = EventIntervalMs;
            eventTimer.Tick += delegate
            {
                if (closed) { return; }
                UpdateSafely();
                if (clock.ElapsedMilliseconds >= settleUntil) { eventTimer.Stop(); }
            };
            contentTimer = new System.Windows.Forms.Timer();
            contentTimer.Interval = ContentPollMs;
            contentTimer.Tick += delegate { UpdateSafely(); };

            stopEvent = new EventWaitHandle(false, EventResetMode.ManualReset, Program.StopName);
            IntPtr controllerHandle = sink.Handle;
            stopWait = ThreadPool.RegisterWaitForSingleObject(stopEvent, delegate(object state, bool timedOut)
            {
                Native.PostMessage(controllerHandle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }, null, Timeout.Infinite, true);

            // Only out-of-context accessibility notifications are used here.
            // Keep the delegate rooted for the complete lifetime of all hooks.
            eventProc = OnWinEvent;
            AddHook(0x0003, 0x0003); // foreground window
            AddHook(0x000A, 0x000B); // move/size start and finish
            AddHook(0x0016, 0x0017); // minimize and restore
            AddHook(0x0020, 0x0020); // desktop switch
            AddHook(0x8000, 0x8004); // create/destroy/show/hide/z-order
            AddHook(0x800A, 0x800B); // window state and position
            AddHook(0x8017, 0x8018); // cloak/uncloak (virtual desktops)
            Native.WTSRegisterSessionNotification(sink.Handle, 0);
            Guid powerGuid = DisplayStateGuid;
            powerNotification = Native.RegisterPowerSettingNotification(sink.Handle, ref powerGuid, 0);
            contentTimer.Start();
            QueueRefresh(true, true);
        }

        private void AddHook(uint first, uint last)
        {
            IntPtr hook = Native.SetWinEventHook(first, last, IntPtr.Zero, eventProc, 0, 0, 2);
            if (hook == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not register a window-change notification.");
            eventHooks.Add(hook);
        }

        private void OnWinEvent(IntPtr hook, uint kind, IntPtr hwnd, int objectId,
            int childId, uint threadId, uint eventTime)
        {
            if (closed) { return; }
            if (kind >= 0x8000)
            {
                if (objectId != 0 || childId != 0) { return; }
                // Destroyed handles are not safe to query. Reorder can name the
                // desktop itself. All other object notifications must be roots.
                if (kind != 0x8001 && kind != 0x8004 && hwnd != IntPtr.Zero &&
                    Native.GetAncestor(hwnd, 2) != hwnd) { return; }
            }
            // Never do capture or COM work inside a WinEvent callback: COM can
            // pump messages and re-enter this callback during a refresh.
            windowsDirty = true;
            QueueRefresh(false, kind == 0x0003 || kind == 0x0020);
        }

        private bool OnMessage(ref Message message)
        {
            if (message.Msg == WM_CLOSE)
            {
                ExitThread();
                return true;
            }
            if (closed) { return false; }
            if (message.Msg == WM_REFRESH)
            {
                refreshPosted = false;
                UpdateSafely();
                if (clock.ElapsedMilliseconds < settleUntil) { eventTimer.Start(); }
                return true;
            }
            if (message.Msg == (int)taskbarCreated || message.Msg == 0x007E)
            {
                QueueRefresh(true, true);
                return false;
            }
            if (message.Msg == 0x001A || message.Msg == 0x031A || message.Msg == 0x031E)
            {
                QueueRefresh(true, true);
                return false;
            }
            if (message.Msg == WM_WTSSESSION_CHANGE)
            {
                int change = message.WParam.ToInt32();
                if (change == 7) { sessionLocked = true; eventTimer.Stop(); }
                if (change == 8 || change == 1 || change == 3)
                {
                    sessionLocked = false;
                    QueueRefresh(true, true);
                }
                return true;
            }
            if (message.Msg == WM_POWERBROADCAST)
            {
                int change = message.WParam.ToInt32();
                if (change == 4) { suspended = true; eventTimer.Stop(); }
                if (change == 7 || change == 18)
                {
                    suspended = false;
                    QueueRefresh(true, true);
                }
                if (change == 0x8013 && message.LParam != IntPtr.Zero)
                {
                    Guid guid = (Guid)Marshal.PtrToStructure(message.LParam, typeof(Guid));
                    int length = Marshal.ReadInt32(message.LParam, 16);
                    if (guid == DisplayStateGuid && length >= 4)
                    {
                        displayOff = Marshal.ReadInt32(message.LParam, 20) == 0;
                        if (!displayOff) { QueueRefresh(true, true); }
                    }
                }
                message.Result = new IntPtr(1);
                return true;
            }
            if (message.Msg == 0x0011) // query end session
            {
                RestoreAll();
                message.Result = new IntPtr(1);
                return true;
            }
            if (message.Msg == 0x0016 && message.WParam != IntPtr.Zero)
            {
                ExitThread();
                return true;
            }
            return false;
        }

        private void QueueRefresh(bool topology, bool force)
        {
            if (closed) { return; }
            if (topology) { topologyDirty = true; }
            if (force) { forceApply = true; }
            windowsDirty = true;
            settleUntil = clock.ElapsedMilliseconds + SettleMs;
            if (!refreshPosted)
            {
                refreshPosted = true;
                Native.PostMessage(sink.Handle, WM_REFRESH, IntPtr.Zero, IntPtr.Zero);
            }
        }

        private void UpdateSafely()
        {
            if (closed || paused || sessionLocked || displayOff || suspended || busy) { return; }
            long now = clock.ElapsedMilliseconds;
            if (now - lastUpdate < EventIntervalMs) { return; }
            busy = true;
            lastUpdate = now;
            long began = Stopwatch.GetTimestamp();
            try
            {
                bool force = forceApply;
                forceApply = false;
                if (topologyDirty || now - lastTopologyScan >= TopologyRefreshMs)
                {
                    // Clear flags BEFORE work. Re-entrant notifications may set
                    // them again, and must not be overwritten at the end.
                    topologyDirty = false;
                    RefreshTopology(now);
                    lastTopologyScan = now;
                }
                if (taskbars.Count == 0) { return; }
                if (!backend.Connected)
                {
                    if (now < nextConnect) { return; }
                    TaskbarState primary = taskbars.Find(delegate(TaskbarState s) { return s.Primary; });
                    if (primary == null) { primary = taskbars[0]; }
                    if (connectStartedAt < 0) { connectStartedAt = now; }
                    if (!backend.TryConnect(primary.Handle, explorerPid))
                    {
                        if (now - connectStartedAt >= 15000)
                            throw new InvalidOperationException("The taskbar XAML connection did not become ready. Close other taskbar stylers. This beta requires the stock Windows 11 taskbar. If the old helper is still active, stop it before launching this version.");
                        nextConnect = now + 500;
                        return;
                    }
                    connectStartedAt = -1;
                    connectionFailures = 0;
                    force = true;
                    foreach (TaskbarState s in taskbars) { s.AppliedColor = null; }
                    Program.Log("Connected to Explorer PID " + explorerPid.ToString() + "; taskbars=" + taskbars.Count.ToString());
                    tray.Text = "TaskbarMatch: matching window edges";
                    if (!announced)
                    {
                        announced = true;
                        tray.ShowBalloonTip(3000, "TaskbarMatch running",
                            "Each monitor gets matching taskbar colors and contrasting text/icons. Run the file again to stop and restore.",
                            ToolTipIcon.Info);
                    }
                }
                if (windowsDirty || now - lastWindowScan >= WindowRefreshMs)
                {
                    windowsDirty = false;
                    ReadWindows();
                    lastWindowScan = now;
                }
                foreach (TaskbarState state in taskbars)
                {
                    UpdateTaskbar(state, now, force);
                }
                updateCount++;
            }
            catch (COMException ex)
            {
                // Explorer can disappear during COM calls. Reconnect on genuine
                // RPC disconnects, but do not repeatedly attach an incompatible
                // backend or silently apply a system-wide accent-color fallback.
                if (IsRpcDisconnect(ex.ErrorCode) && connectionFailures < 2)
                {
                    Program.Log("Explorer connection lost: " + ex.Message);
                    backend.Disconnect(false);
                    connectionFailures++;
                    nextConnect = now + 1000;
                    topologyDirty = true;
                    windowsDirty = true;
                    forceApply = true;
                }
                else { Fail(ex); }
            }
            catch (Exception ex) { Fail(ex); }
            finally
            {
                updateTicks += Stopwatch.GetTimestamp() - began;
                busy = false;
            }
        }

        private static bool IsRpcDisconnect(int hr)
        {
            return hr == unchecked((int)0x80010108) || hr == unchecked((int)0x800706BA) ||
                hr == unchecked((int)0x800706BE) || hr == unchecked((int)0x80010007) ||
                hr == unchecked((int)0x800401FD);
        }

        private void Fail(Exception ex)
        {
            Program.Log(ex.ToString());
            Environment.ExitCode = 1;
            Shutdown();
            MessageBox.Show(ex.Message + "\r\n\r\nTaskbarMatch has stopped. No global theme or accent settings were changed.\r\n\r\nLog: " +
                Path.Combine(Program.Root, "runtime.log"), "TaskbarMatch stopped", MessageBoxButtons.OK, MessageBoxIcon.Error);
            base.ExitThreadCore();
        }

        private void RefreshTopology(long now)
        {
            List<DisplayInfo> displays = new List<DisplayInfo>();
            Native.MonitorEnumProc enumerate = delegate(IntPtr monitor, IntPtr dc, ref Native.RECT rect, IntPtr data)
            {
                Native.MONITORINFOEX info = new Native.MONITORINFOEX();
                info.cbSize = (uint)Marshal.SizeOf(typeof(Native.MONITORINFOEX));
                if (Native.GetMonitorInfo(monitor, ref info))
                    displays.Add(new DisplayInfo(monitor, info.rcMonitor, info.rcWork, info.szDevice));
                return true;
            };
            if (!Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, enumerate, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not enumerate displays.");

            IntPtr main = Native.FindWindow("Shell_TrayWnd", null);
            uint newPid = 0;
            if (main != IntPtr.Zero) { Native.GetWindowThreadProcessId(main, out newPid); }
            if (newPid == 0)
            {
                foreach (TaskbarState item in taskbars) { item.Sampler.Dispose(); }
                taskbars.Clear();
                windowsDirty = true;
                return;
            }
            if (newPid != explorerPid)
            {
                if (explorerPid != 0)
                {
                    shellRestarts.Enqueue(now);
                    while (shellRestarts.Count > 0 && now - shellRestarts.Peek() > 60000) { shellRestarts.Dequeue(); }
                    if (shellRestarts.Count >= 3)
                        throw new InvalidOperationException("Explorer restarted repeatedly. TaskbarMatch stopped rather than repeatedly attaching to an unstable shell.");
                }
                backend.Disconnect(false);
                explorerPid = newPid;
                connectionFailures = 0;
                nextConnect = now;
                connectStartedAt = -1;
                forceApply = true;
                windowsDirty = true;
            }

            List<IntPtr> handles = new List<IntPtr>();
            handles.Add(main);
            IntPtr previous = IntPtr.Zero;
            while (true)
            {
                previous = Native.FindWindowEx(IntPtr.Zero, previous, "Shell_SecondaryTrayWnd", null);
                if (previous == IntPtr.Zero) { break; }
                uint pid;
                Native.GetWindowThreadProcessId(previous, out pid);
                if (pid == explorerPid) { handles.Add(previous); }
            }
            List<TaskbarState> fresh = new List<TaskbarState>();
            foreach (IntPtr handle in handles)
            {
                DisplayInfo display = null;
                // Auto-hidden taskbar rectangles can lie just over a neighboring
                // monitor's boundary. Ask the appbar service before using nearest.
                foreach (DisplayInfo d in displays)
                {
                    Native.APPBARDATA data = new Native.APPBARDATA();
                    data.cbSize = (uint)Marshal.SizeOf(typeof(Native.APPBARDATA));
                    data.uEdge = 3;
                    data.rc = d.Bounds;
                    if (Native.SHAppBarMessage(0x0000000B, ref data) == handle)
                    {
                        display = d;
                        break;
                    }
                }
                if (display == null)
                {
                    IntPtr monitor = Native.MonitorFromWindow(handle, 2);
                    display = displays.Find(delegate(DisplayInfo d) { return d.Handle == monitor; });
                }
                if (display == null) { continue; }
                TaskbarState old = taskbars.Find(delegate(TaskbarState s)
                {
                    return s.Handle == handle && s.Display.Handle == display.Handle && s.ExplorerPid == newPid;
                });
                TaskbarState item = old ?? new TaskbarState(handle, display, newPid, handle == main);
                item.Display = display;
                item.Dpi = Math.Max(96u, Native.GetDpiForWindow(handle));
                fresh.Add(item);
            }
            foreach (TaskbarState removed in taskbars)
            {
                if (!fresh.Contains(removed)) { removed.Sampler.Dispose(); }
            }
            taskbars.Clear();
            taskbars.AddRange(fresh);
        }

        private void ReadWindows()
        {
            List<WindowInfo> fresh = new List<WindowInfo>();
            Native.EnumWindowsProc callback = delegate(IntPtr hwnd, IntPtr data)
            {
                if (!Native.IsWindowVisible(hwnd) || Native.IsIconic(hwnd)) { return true; }
                uint pid;
                Native.GetWindowThreadProcessId(hwnd, out pid);
                if (pid == ownPid) { return true; }
                string className = Native.ClassName(hwnd);
                if (className == "Shell_TrayWnd" || className == "Shell_SecondaryTrayWnd" ||
                    className == "Progman" || className == "WorkerW") { return true; }
                int cloaked;
                if (Native.DwmGetWindowAttributeInt(hwnd, 14, out cloaked, 4) == 0 && cloaked != 0) { return true; }
                long exStyle = Native.GetWindowLongPtr(hwnd, -20).ToInt64();
                // Click-through layered overlays are not candidate applications.
                // Skip them as rectangular occluders because their bounds may
                // span an entire display even when their visible content does not.
                if ((exStyle & 0x00080020L) == 0x00080020L) { return true; }
                Native.RECT rect;
                if (Native.DwmGetWindowAttributeRect(hwnd, 9, out rect, 16) != 0 &&
                    !Native.GetWindowRect(hwnd, out rect)) { return true; }
                if (rect.Width < 1 || rect.Height < 1) { return true; }
                bool candidate = (exStyle & 0x80L) == 0 && Native.GetWindow(hwnd, 4) == IntPtr.Zero;
                if (className == "Windows.UI.Core.CoreWindow" || className == "XamlExplorerHostIslandWindow" ||
                    className == "Xaml_WindowedPopupClass") { candidate = false; }
                fresh.Add(new WindowInfo(hwnd, rect, Native.IsZoomed(hwnd), candidate, className));
                return true;
            };
            Native.EnumWindows(callback, IntPtr.Zero);
            windows.Clear();
            windows.AddRange(fresh);
        }

        private void UpdateTaskbar(TaskbarState state, long now, bool force)
        {
            Native.RECT bar;
            if (!Native.GetWindowRect(state.Handle, out bar))
            {
                topologyDirty = true;
                return;
            }
            Native.RECT m = state.Display.Bounds;
            int visibleHeight = Math.Min(bar.Bottom, m.Bottom) - Math.Max(bar.Top, m.Top);
            if (!Native.IsWindowVisible(state.Handle) || visibleHeight < Math.Max(8, (int)(state.Dpi / 12)))
            {
                state.Status = "Taskbar hidden; left hidden";
                return;
            }
            // This build is intentionally for the stock bottom Windows 11
            // taskbar, not an ExplorerPatcher/StartAllBack replacement.
            if (bar.Width < m.Width * 3 / 4 || bar.Top < m.Top + m.Height / 2)
            {
                state.Status = "Non-stock taskbar geometry; unchanged";
                RestoreOne(state);
                return;
            }
            int inset = Math.Max(3, (int)Math.Round(4.0 * state.Dpi / 96.0));
            WindowInfo candidate = null;
            int candidateIndex = -1;
            for (int index = 0; index < windows.Count; index++)
            {
                WindowInfo w = windows[index];
                if (!w.CanBeSource || !Core.IsBorderingSource(w.Bounds, w.Maximized, m, bar.Top, inset + 3)) { continue; }
                candidate = w;
                candidateIndex = index;
                break;
            }
            if (candidate == null)
            {
                state.Source = IntPtr.Zero;
                state.SourceClass = "";
                state.Status = "No bordering maximized window; Windows default";
                RestoreOne(state);
                return;
            }
            state.Source = candidate.Handle;
            state.SourceClass = candidate.ClassName;
            if (!Native.IsWindow(candidate.Handle) || !Native.IsWindowVisible(candidate.Handle) || Native.IsIconic(candidate.Handle))
            {
                windowsDirty = true;
                return;
            }

            // Respect true full-screen: if its surface actually covers the
            // taskbar, do not keep capturing a game/video or force the bar on top.
            if (Core.CoversMonitor(candidate.Bounds, m, inset + 3))
            {
                Native.POINT p = new Native.POINT(m.Left + m.Width / 2,
                    Math.Min(m.Bottom - 2, bar.Top + visibleHeight / 2));
                IntPtr hit = Native.WindowFromPoint(p);
                if (hit != IntPtr.Zero && Native.GetAncestor(hit, 2) == candidate.Handle)
                {
                    state.Status = "Full-screen covers taskbar; left hidden";
                    return;
                }
            }

            int left = Math.Max(m.Left, candidate.Bounds.Left) + inset;
            int right = Math.Min(m.Right, candidate.Bounds.Right) - inset;
            int top = Math.Min(bar.Top, candidate.Bounds.Bottom) - inset - StripSampler.Height;
            if (right - left < StripSampler.Columns || top < Math.Max(m.Top, candidate.Bounds.Top))
            {
                state.Status = "No usable window edge";
                return;
            }
            Native.RECT strip = new Native.RECT(left, top, right, top + StripSampler.Height);
            bool[] valid = state.SampleMask;
            int validCount = 0;
            for (int column = 0; column < StripSampler.Columns; column++)
            {
                int x = Core.SampleX(strip.Left, strip.Width, column, StripSampler.Columns);
                bool uncovered = true;
                for (int higher = 0; higher < candidateIndex; higher++)
                {
                    Native.RECT r = windows[higher].Bounds;
                    if (x >= r.Left && x < r.Right && r.Top < strip.Bottom && r.Bottom > strip.Top)
                    {
                        uncovered = false;
                        break;
                    }
                }
                valid[column] = uncovered;
                if (uncovered) { validCount++; }
            }
            if (validCount < 8)
            {
                state.Status = "Window edge occluded; previous color held";
                return;
            }
            uint color;
            if (!state.Sampler.TrySample(strip, valid, state.AppliedColor, out color))
            {
                samplingFailures++;
                state.Status = "Screen capture unavailable; previous color held";
                return;
            }
            captureCount++;
            if (force || !state.AppliedColor.HasValue || state.AppliedColor.Value != color || now - state.LastAppliedAt >= ReapplyMs)
            {
                bool setBorder = force || !state.AppliedColor.HasValue || now - state.LastAppliedAt >= ReapplyMs;
                if (!backend.SetColor(state.Handle, color, setBorder))
                {
                    state.Status = "Waiting for taskbar background/contrast controls";
                    return;
                }
                state.AppliedColor = color;
                state.LastAppliedAt = now;
                appliedCount++;
            }
            state.Status = "Matching " + Core.HexRgb(color) + " | " + backend.ContrastLabel(state.Handle);
        }

        private void RestoreOne(TaskbarState state)
        {
            if (!state.AppliedColor.HasValue || !backend.Connected) { return; }
            backend.RestoreOne(state.Handle);
            state.AppliedColor = null;
        }

        private void RestoreAll()
        {
            try { backend.RestoreAll(); }
            catch (Exception ex) { Program.Log("Restore: " + ex.Message); }
            foreach (TaskbarState s in taskbars) { s.AppliedColor = null; }
        }

        private bool StartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
                    return key != null && key.GetValue("TaskbarMatch") != null;
            }
            catch { return false; }
        }

        private void ToggleStartup()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
                {
                    if (StartupEnabled()) { key.DeleteValue("TaskbarMatch", false); }
                    else { key.SetValue("TaskbarMatch", "\"" + Application.ExecutablePath + "\" --autostart", RegistryValueKind.String); }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Could not change startup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            startupItem.Checked = StartupEnabled();
        }

        private void ShowDiagnostics()
        {
            if (closed) { return; }
            StringBuilder text = new StringBuilder();
            text.AppendLine("TaskbarMatch 1.1.2 beta | Windows 11 x64");
            text.AppendLine("Backend: " + (backend.Connected ? "TaskbarMatch.Xaml 1.1.2 beta connected" : "not connected"));
            text.AppendLine("State: " + (paused ? "paused" : "running"));
            text.AppendLine("Window events + 250 ms content polling; 40 ms event refresh cap.");
            text.AppendLine("No screenshots are stored or uploaded.");
            text.AppendLine();
            foreach (TaskbarState s in taskbars)
            {
                text.AppendLine(s.Display.Name + " | " + s.Dpi.ToString() + " DPI");
                text.AppendLine("  " + s.Status);
                if (s.Source != IntPtr.Zero) { text.AppendLine("  Source class: " + s.SourceClass); }
            }
            using (Process self = Process.GetCurrentProcess())
            {
                double seconds = Math.Max(clock.Elapsed.TotalSeconds, 0.001);
                double cpu = 100.0 * self.TotalProcessorTime.TotalSeconds / seconds / Environment.ProcessorCount;
                text.AppendLine();
                text.AppendLine("Average controller CPU / whole machine: " + cpu.ToString("F3") + "%");
                text.AppendLine("Controller working set: " + (self.WorkingSet64 / 1048576.0).ToString("F1") + " MiB");
                text.AppendLine("These figures exclude work performed inside Explorer and the GPU.");
            }
            text.AppendLine("Captures: " + captureCount.ToString() + "; taskbar writes: " + appliedCount.ToString());
            text.AppendLine("Capture failures: " + samplingFailures.ToString());
            if (updateCount > 0)
                text.AppendLine("Mean update wall time: " + (1000.0 * updateTicks / Stopwatch.Frequency / updateCount).ToString("F2") + " ms");
            text.AppendLine();
            text.AppendLine("Logs/source/cache: " + Program.Root);
            text.AppendLine("Exit restores taskbar background, separator, local themes and foreground brushes.");
            MessageBox.Show(text.ToString(), "TaskbarMatch diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        internal void Shutdown()
        {
            if (closed) { return; }
            closed = true;
            if (contentTimer != null) { contentTimer.Stop(); contentTimer.Dispose(); }
            if (eventTimer != null) { eventTimer.Stop(); eventTimer.Dispose(); }
            foreach (IntPtr hook in eventHooks) { Native.UnhookWinEvent(hook); }
            eventHooks.Clear();
            if (stopWait != null) { stopWait.Unregister(null); }
            RestoreAll();
            backend.Dispose();
            foreach (TaskbarState s in taskbars) { s.Sampler.Dispose(); }
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            if (menu != null) { menu.Dispose(); }
            if (powerNotification != IntPtr.Zero) { Native.UnregisterPowerSettingNotification(powerNotification); }
            if (sink != null)
            {
                Native.WTSUnRegisterSessionNotification(sink.Handle);
                sink.Dispose();
            }
            if (stopEvent != null) { stopEvent.Dispose(); }
            GC.KeepAlive(eventProc);
        }

        protected override void ExitThreadCore()
        {
            Shutdown();
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { Shutdown(); }
            base.Dispose(disposing);
        }
    }

    internal sealed class DisplayInfo
    {
        internal readonly IntPtr Handle;
        internal readonly Native.RECT Bounds;
        internal readonly Native.RECT WorkArea;
        internal readonly string Name;
        internal DisplayInfo(IntPtr handle, Native.RECT bounds, Native.RECT workArea, string name)
        {
            Handle = handle; Bounds = bounds; WorkArea = workArea; Name = name;
        }
    }

    internal sealed class TaskbarState
    {
        internal readonly IntPtr Handle;
        internal readonly uint ExplorerPid;
        internal readonly bool Primary;
        internal DisplayInfo Display;
        internal uint Dpi = 96;
        internal uint? AppliedColor;
        internal long LastAppliedAt;
        internal IntPtr Source;
        internal string SourceClass = "";
        internal string Status = "Ready";
        internal readonly bool[] SampleMask = new bool[StripSampler.Columns];
        internal readonly StripSampler Sampler = new StripSampler();
        internal TaskbarState(IntPtr handle, DisplayInfo display, uint pid, bool primary)
        {
            Handle = handle; Display = display; ExplorerPid = pid; Primary = primary;
        }
    }

    internal sealed class WindowInfo
    {
        internal readonly IntPtr Handle;
        internal readonly Native.RECT Bounds;
        internal readonly bool Maximized;
        internal readonly bool CanBeSource;
        internal readonly string ClassName;
        internal WindowInfo(IntPtr handle, Native.RECT bounds, bool maximized, bool candidate, string className)
        {
            Handle = handle; Bounds = bounds; Maximized = maximized; CanBeSource = candidate; ClassName = className;
        }
    }

    internal static class Core
    {
        internal static uint Abgr(int red, int green, int blue)
        {
            return 0xFF000000u | ((uint)blue << 16) | ((uint)green << 8) | (uint)red;
        }

        internal static int ColorBin(uint abgr)
        {
            // Quantize only for cluster selection. The applied color is still
            // one of the original, unquantized RGB colors in the sampled strip.
            return (int)(((abgr & 0xFFu) >> 4) << 8 |
                (((abgr >> 8) & 0xFFu) >> 4) << 4 |
                (((abgr >> 16) & 0xFFu) >> 4));
        }

        internal static string HexRgb(uint abgr)
        {
            return "#" + (abgr & 0xFFu).ToString("X2") +
                ((abgr >> 8) & 0xFFu).ToString("X2") + ((abgr >> 16) & 0xFFu).ToString("X2");
        }

        internal static int SampleX(int left, int width, int column, int count)
        {
            return left + (int)(((2L * column + 1) * width) / (2L * count));
        }

        internal static bool CoversMonitor(Native.RECT window, Native.RECT monitor, int tolerance)
        {
            return window.Left <= monitor.Left + tolerance && window.Top <= monitor.Top + tolerance &&
                window.Right >= monitor.Right - tolerance && window.Bottom >= monitor.Bottom - tolerance;
        }

        internal static bool IsBorderingSource(Native.RECT window, bool maximized,
            Native.RECT monitor, int taskbarTop, int tolerance)
        {
            if (!maximized && !CoversMonitor(window, monitor, tolerance)) { return false; }
            return window.Left <= monitor.Left + tolerance && window.Right >= monitor.Right - tolerance &&
                window.Bottom >= taskbarTop - tolerance && window.Top < taskbarTop - 12 &&
                window.Bottom > monitor.Top && window.Top < monitor.Bottom;
        }

        internal static void SelfTest()
        {
            if (Abgr(0x12, 0x34, 0x56) != 0xFF563412u || HexRgb(0xFF563412u) != "#123456")
                throw new InvalidOperationException("Internal color-format test failed.");
            if (ColorBin(Abgr(0x12, 0x34, 0x56)) != 0x135)
                throw new InvalidOperationException("Internal color-cluster test failed.");
            Native.RECT leftMonitor = new Native.RECT(-1920, -200, 0, 880);
            Native.RECT max = new Native.RECT(-1920, -200, 0, 832);
            Native.RECT floating = new Native.RECT(-1600, 0, -200, 700);
            if (!IsBorderingSource(max, true, leftMonitor, 832, 7) ||
                IsBorderingSource(floating, false, leftMonitor, 832, 7) ||
                CoversMonitor(max, leftMonitor, 7) || !CoversMonitor(leftMonitor, leftMonitor, 7))
                throw new InvalidOperationException("Internal monitor-geometry test failed.");
            if (SampleX(-1920, 1920, 0, 96) != -1910 || SampleX(-1920, 1920, 95, 96) != -10)
                throw new InvalidOperationException("Internal negative-coordinate test failed.");
        }
    }

    internal sealed class StripSampler : IDisposable
    {
        internal const int Height = 3;
        internal const int Columns = 96;
        internal long Captures;
        private IntPtr memoryDc;
        private IntPtr bitmap;
        private IntPtr oldBitmap;
        private IntPtr bits;
        private int allocatedWidth;
        private byte[] pixels;
        private readonly int[] counts = new int[4096];
        private readonly uint[] sampled = new uint[Columns * Height];
        private readonly Dictionary<uint, int> exactCounts = new Dictionary<uint, int>(Columns * Height);
        private bool disposed;

        internal bool TrySample(Native.RECT strip, bool[] valid, uint? previous, out uint color)
        {
            color = 0;
            if (disposed || strip.Width < Columns || strip.Width > 32768 || strip.Height != Height) { return false; }
            IntPtr desktopDc = Native.GetDC(IntPtr.Zero);
            if (desktopDc == IntPtr.Zero) { return false; }
            try
            {
                EnsureBuffer(desktopDc, strip.Width);
                // A single narrow BitBlt, not one GetPixel GPU readback for each
                // sample and not a full-screen screenshot. CAPTUREBLT includes
                // composited/layered content; the cursor is not explicitly drawn.
                if (!Native.BitBlt(memoryDc, 0, 0, strip.Width, Height, desktopDc,
                    strip.Left, strip.Top, 0x40CC0020u)) { return false; }
                Native.GdiFlush();
                Marshal.Copy(bits, pixels, 0, strip.Width * Height * 4);
                Captures++;
            }
            finally { Native.ReleaseDC(IntPtr.Zero, desktopDc); }

            Array.Clear(counts, 0, counts.Length);
            int used = 0;
            for (int column = 0; column < Columns; column++)
            {
                if (!valid[column]) { continue; }
                int localX = Core.SampleX(0, strip.Width, column, Columns);
                for (int row = 0; row < Height; row++)
                {
                    int offset = (row * allocatedWidth + localX) * 4;
                    uint value = Core.Abgr(pixels[offset + 2], pixels[offset + 1], pixels[offset]);
                    sampled[used++] = value;
                    counts[Core.ColorBin(value)]++;
                }
            }
            if (used == 0) { return false; }
            int winningBin = 0;
            int maximum = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > maximum) { maximum = counts[i]; winningBin = i; }
            }
            if (previous.HasValue)
            {
                int oldBin = Core.ColorBin(previous.Value);
                if (counts[oldBin] == maximum) { winningBin = oldBin; }
            }
            exactCounts.Clear();
            for (int i = 0; i < used; i++)
            {
                uint value = sampled[i];
                if (Core.ColorBin(value) != winningBin) { continue; }
                int frequency;
                exactCounts.TryGetValue(value, out frequency);
                exactCounts[value] = frequency + 1;
            }
            int bestFrequency = 0;
            foreach (KeyValuePair<uint, int> entry in exactCounts)
            {
                if (entry.Value > bestFrequency ||
                    (entry.Value == bestFrequency && previous.HasValue && entry.Key == previous.Value))
                {
                    color = entry.Key;
                    bestFrequency = entry.Value;
                }
            }
            return bestFrequency != 0;
        }

        private void EnsureBuffer(IntPtr desktopDc, int width)
        {
            if (memoryDc != IntPtr.Zero && allocatedWidth == width) { return; }
            FreeBuffer();
            memoryDc = Native.CreateCompatibleDC(desktopDc);
            if (memoryDc == IntPtr.Zero) { throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the edge-sampling buffer."); }
            Native.BITMAPINFO info = new Native.BITMAPINFO();
            info.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            info.bmiHeader.biWidth = width;
            info.bmiHeader.biHeight = -Height;
            info.bmiHeader.biPlanes = 1;
            info.bmiHeader.biBitCount = 32;
            info.bmiHeader.biCompression = 0;
            info.bmiHeader.biSizeImage = (uint)(width * Height * 4);
            bitmap = Native.CreateDIBSection(desktopDc, ref info, 0, out bits, IntPtr.Zero, 0);
            if (bitmap == IntPtr.Zero || bits == IntPtr.Zero)
            {
                FreeBuffer();
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not allocate the edge-sampling bitmap.");
            }
            oldBitmap = Native.SelectObject(memoryDc, bitmap);
            if (oldBitmap == IntPtr.Zero || oldBitmap == new IntPtr(-1))
            {
                oldBitmap = IntPtr.Zero;
                FreeBuffer();
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not activate the edge-sampling bitmap.");
            }
            allocatedWidth = width;
            pixels = new byte[width * Height * 4];
        }

        private void FreeBuffer()
        {
            if (memoryDc != IntPtr.Zero && oldBitmap != IntPtr.Zero) { Native.SelectObject(memoryDc, oldBitmap); }
            if (bitmap != IntPtr.Zero) { Native.DeleteObject(bitmap); }
            if (memoryDc != IntPtr.Zero) { Native.DeleteDC(memoryDc); }
            bitmap = IntPtr.Zero; memoryDc = IntPtr.Zero; oldBitmap = IntPtr.Zero;
            bits = IntPtr.Zero; allocatedWidth = 0; pixels = null;
        }

        public void Dispose()
        {
            if (disposed) { return; }
            disposed = true;
            FreeBuffer();
        }
    }

    // Local XAML changes only. Background and foreground share one diagnostics
    // connection, rather than competing with a second taskbar styling plugin.
    internal static class Contrast
    {
        private static readonly double[] Linear = BuildTable();
        private static double[] BuildTable()
        {
            double[] table = new double[256];
            for (int i = 0; i < table.Length; i++)
            {
                double c = i / 255.0;
                table[i] = c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }
            return table;
        }
        internal static double Luminance(uint abgr)
        {
            return 0.2126 * Linear[(int)(abgr & 255)] +
                0.7152 * Linear[(int)((abgr >> 8) & 255)] +
                0.0722 * Linear[(int)((abgr >> 16) & 255)];
        }
        internal static int Theme(uint abgr, int previous)
        {
            double light = Luminance(abgr);
            // ElementTheme.Light=1 gives dark glyphs; Dark=2 gives light glyphs.
            // Narrow hysteresis keeps ideal black/white contrast >=4.5:1.
            if (previous == 1) { return light < 0.175 ? 2 : 1; }
            if (previous == 2) { return light > 0.183 ? 1 : 2; }
            return light >= 0.179128784747792 ? 1 : 2;
        }
        internal static void SelfTest()
        {
            if (Theme(0xFFFFFFFFu, 0) != 1 || Theme(0xFF000000u, 0) != 2 ||
                Theme(0xFF0000FFu, 0) != 1 || Theme(0xFFFF0000u, 0) != 2 ||
                Theme(0xFF00FF00u, 0) != 1 || Theme(0xFF767676u, 1) != 1 ||
                Theme(0xFF767676u, 2) != 2)
                throw new InvalidOperationException("The contrast self-test failed.");
            for (uint v = 0; v < 256; v++)
            {
                uint gray = 0xFF000000u | v | (v << 8) | (v << 16);
                double l = Luminance(gray);
                for (int old = 0; old <= 2; old++)
                {
                    int theme = Theme(gray, old);
                    double ratio = theme == 1 ? (l + 0.05) / 0.05 : 1.05 / (l + 0.05);
                    if (ratio < 4.49)
                        throw new InvalidOperationException("Invalid black/white contrast threshold.");
                }
            }
        }
    }

    internal sealed class TapBackend : IDisposable
    {
        internal const string HelperHash = "1ca10b294157a0bd814e33b3888f72dc13d4c1401bbce947cc2c619972c47f62";
        internal const string HelperFolder = "xaml-1.1.2-b1-1ca10b294157";
        private const int Pending = unchecked((int)0x8000000A);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int EnsureDelegate(IntPtr taskbar);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int CommandDelegate(uint explorer, IntPtr sender, uint operation,
            IntPtr taskbar, uint abgr, uint theme, uint force);
        private IntPtr module;
        private EnsureDelegate ensure;
        private CommandDelegate command;
        private uint explorerPid;
        private bool connected;
        private readonly Dictionary<IntPtr, int> themes = new Dictionary<IntPtr, int>();
        private readonly Dictionary<IntPtr, long> pendingSince = new Dictionary<IntPtr, long>();
        internal IntPtr ControllerHandle;
        internal bool Connected { get { return connected; } }

        private static void ExtractEmbeddedHelper(string path)
        {
            using (Stream source = typeof(TapBackend).Assembly.GetManifestResourceStream("TaskbarMatch.Xaml.dll"))
            {
                if (source == null)
                    throw new FileNotFoundException("The embedded Taskbar Match helper is missing from this executable.");
                string directory = Path.GetDirectoryName(path);
                Directory.CreateDirectory(directory);
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (FileStream target = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        byte[] buffer = new byte[81920];
                        int read;
                        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                            target.Write(buffer, 0, read);
                    }
                    if (!File.Exists(path)) { File.Move(temporary, path); }
                }
                finally
                {
                    if (File.Exists(temporary)) { File.Delete(temporary); }
                }
            }
        }

        private void LoadHelper()
        {
            if (module != IntPtr.Zero) { return; }
            string path = Path.Combine(Program.Root, HelperFolder, "TaskbarMatch.Xaml.dll");
            if (!File.Exists(path)) { ExtractEmbeddedHelper(path); }
            if (!File.Exists(path))
                throw new FileNotFoundException("The embedded contrast helper could not be prepared.", path);
            using (SHA256 sha = SHA256.Create())
            using (FileStream file = File.OpenRead(path))
            {
                string hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
                if (!hash.Equals(HelperHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The contrast helper failed its SHA-256 check and was not loaded.");
            }
            module = Native.LoadLibraryEx(path, IntPtr.Zero, 0x1100);
            if (module == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not load TaskbarMatch's contrast helper.");
            IntPtr a = Native.GetProcAddress(module, "EB_EnsureInjected");
            IntPtr b = Native.GetProcAddress(module, "EB_Command");
            if (a == IntPtr.Zero || b == IntPtr.Zero)
                throw new InvalidOperationException("The contrast helper has an incompatible interface.");
            ensure = (EnsureDelegate)Marshal.GetDelegateForFunctionPointer(a, typeof(EnsureDelegate));
            command = (CommandDelegate)Marshal.GetDelegateForFunctionPointer(b, typeof(CommandDelegate));
        }
        internal bool TryConnect(IntPtr taskbar, uint pid)
        {
            if (Connected) { return true; }
            if (!ValidateTaskbar(taskbar, pid)) { return false; }
            if (ControllerHandle == IntPtr.Zero)
                throw new InvalidOperationException("The controller message window is missing.");
            LoadHelper();
            int hr = ensure(taskbar);
            if (hr == Pending) { return false; }
            Check(hr);
            hr = command(pid, ControllerHandle, 0, IntPtr.Zero, 0, 0, 0);
            if (hr == Pending) { return false; }
            Check(hr);
            explorerPid = pid;
            connected = true;
            return true;
        }

        private static bool ValidateTaskbar(IntPtr handle, uint pid)
        {
            bool hasStockModule = false;
            using (Process explorer = Process.GetProcessById((int)pid))
            {
                if (!explorer.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The detected taskbar does not belong to Windows Explorer.");
                string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Taskbar.dll");
                foreach (ProcessModule m in explorer.Modules)
                {
                    if (m.FileName.Equals(expected, StringComparison.OrdinalIgnoreCase))
                    {
                        hasStockModule = true;
                        break;
                    }
                }
            }
            int islands = 0;
            Native.EnumWindowsProc callback = delegate(IntPtr child, IntPtr unused)
            {
                if (Native.ClassName(child) == "Windows.UI.Composition.DesktopWindowContentBridge")
                {
                    StringBuilder title = new StringBuilder(96);
                    Native.GetWindowText(child, title, title.Capacity);
                    if (title.ToString() == "DesktopWindowXamlSource") { islands++; }
                }
                return true;
            };
            Native.EnumChildWindows(handle, callback, IntPtr.Zero);
            // During login or Explorer recovery, its taskbar window can exist
            // briefly before Taskbar.dll and the XAML island have initialized.
            if (!hasStockModule || islands == 0) { return false; }
            if (islands != 1)
                throw new InvalidOperationException("TaskbarMatch did not find the stock modern Windows 11 XAML taskbar. Legacy or replacement taskbars are not supported by this file. No system accent settings were changed.");
            return true;
        }

        private static void Check(int hr)
        {
            if (hr < 0)
                throw new COMException("TaskbarMatch could not update the taskbar's XAML controls (0x" +
                    hr.ToString("X8") + "). Close competing taskbar stylers. No global theme was changed.", hr);
        }
        internal bool SetColor(IntPtr taskbar, uint abgr, bool force)
        {
            if (!Connected) { return false; }
            int oldTheme;
            if (!themes.TryGetValue(taskbar, out oldTheme)) { oldTheme = 0; }
            int nextTheme = Contrast.Theme(abgr, oldTheme);
            int hr = command(explorerPid, ControllerHandle, 1, taskbar, abgr,
                (uint)nextTheme, force ? 1u : 0u);
            if (hr == Pending)
            {
                long began;
                if (!pendingSince.TryGetValue(taskbar, out began))
                {
                    began = Stopwatch.GetTimestamp();
                    pendingSince[taskbar] = began;
                }
                if ((Stopwatch.GetTimestamp() - began) / (double)Stopwatch.Frequency > 8.0)
                    throw new InvalidOperationException("The taskbar background and contrast controls could not be initialized. " +
                        "This Windows taskbar layout may be incompatible, or another taskbar styler is active. " +
                        "TaskbarMatch has not changed the global Windows theme.");
                return false;
            }
            Check(hr);
            pendingSince.Remove(taskbar);
            themes[taskbar] = nextTheme;
            return true;
        }
        internal string ContrastLabel(IntPtr taskbar)
        {
            int theme;
            if (!themes.TryGetValue(taskbar, out theme)) { return "native contrast"; }
            return theme == 1 ? "dark text/icons" : "light text/icons";
        }
        internal void RestoreOne(IntPtr taskbar)
        {
            if (!Connected) { return; }
            int hr = command(explorerPid, ControllerHandle, 2, taskbar, 0, 0, 0);
            if (hr != Pending) { Check(hr); }
            themes.Remove(taskbar);
            pendingSince.Remove(taskbar);
        }
        internal void RestoreAll()
        {
            if (Connected)
            {
                int hr = command(explorerPid, ControllerHandle, 3, IntPtr.Zero, 0, 0, 0);
                if (hr != Pending) { Check(hr); }
            }
            themes.Clear();
            pendingSince.Clear();
        }
        internal void Disconnect(bool restore)
        {
            if (restore) { try { RestoreAll(); } catch { } }
            connected = false;
            explorerPid = 0;
            themes.Clear();
            pendingSince.Clear();
        }
        public void Dispose()
        {
            Disconnect(true);
            // The Explorer copy is pinned until Explorer exits. It restores
            // saved local values on exit or controller process death.
            GC.KeepAlive(ensure);
            GC.KeepAlive(command);
        }
    }

    internal delegate bool MessageCallback(ref Message message);

    internal sealed class MessageSink : NativeWindow, IDisposable
    {
        private readonly MessageCallback callback;
        private bool disposed;
        internal MessageSink(MessageCallback callback)
        {
            this.callback = callback;
            CreateParams options = new CreateParams();
            options.Caption = "TaskbarMatch.Controller";
            options.Style = unchecked((int)0x80000000); // invisible top-level popup
            options.ExStyle = 0x08000080; // no activation, no taskbar button
            options.X = 0; options.Y = 0; options.Width = 0; options.Height = 0;
            CreateHandle(options);
        }
        protected override void WndProc(ref Message message)
        {
            if (!disposed && callback != null && callback(ref message)) { return; }
            base.WndProc(ref message);
        }
        public void Dispose()
        {
            if (disposed) { return; }
            disposed = true;
            DestroyHandle();
        }
    }

    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            internal int Left, Top, Right, Bottom;
            internal RECT(int l, int t, int r, int b) { Left = l; Top = t; Right = r; Bottom = b; }
            internal int Width { get { return Right - Left; } }
            internal int Height { get { return Bottom - Top; } }
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            internal int X, Y;
            internal POINT(int x, int y) { X = x; Y = y; }
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct MONITORINFOEX
        {
            internal uint cbSize;
            internal RECT rcMonitor, rcWork;
            internal uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string szDevice;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct APPBARDATA
        {
            internal uint cbSize;
            internal IntPtr hWnd;
            internal uint uCallbackMessage;
            internal uint uEdge;
            internal RECT rc;
            internal IntPtr lParam;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct BITMAPINFOHEADER
        {
            internal uint biSize;
            internal int biWidth, biHeight;
            internal ushort biPlanes, biBitCount;
            internal uint biCompression, biSizeImage;
            internal int biXPelsPerMeter, biYPelsPerMeter;
            internal uint biClrUsed, biClrImportant;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct BITMAPINFO
        {
            internal BITMAPINFOHEADER bmiHeader;
            internal uint bmiColors;
        }

        internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr data);
        internal delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref RECT rect, IntPtr data);
        internal delegate void WinEventProc(IntPtr hook, uint kind, IntPtr hwnd, int objectId, int childId, uint threadId, uint time);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string title);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr data);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr data);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnumProc callback, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);
        [DllImport("user32.dll")]
        internal static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
        [DllImport("user32.dll")]
        internal static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")]
        internal static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")]
        internal static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")]
        internal static extern bool IsZoomed(IntPtr hwnd);
        [DllImport("user32.dll")]
        internal static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindow(IntPtr hwnd, uint command);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        internal static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetClassName(IntPtr hwnd, StringBuilder name, int maximum);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maximum);
        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll")]
        internal static extern IntPtr WindowFromPoint(POINT point);
        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
        internal static extern int DwmGetWindowAttributeRect(IntPtr hwnd, uint attribute, out RECT value, uint size);
        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
        internal static extern int DwmGetWindowAttributeInt(IntPtr hwnd, uint attribute, out int value, uint size);
        [DllImport("shell32.dll")]
        internal static extern IntPtr SHAppBarMessage(uint message, ref APPBARDATA data);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, WinEventProc callback,
            uint processId, uint threadId, uint flags);
        [DllImport("user32.dll")]
        internal static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool PostMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern uint RegisterWindowMessage(string text);
        [DllImport("wtsapi32.dll", SetLastError = true)]
        internal static extern bool WTSRegisterSessionNotification(IntPtr hwnd, uint flags);
        [DllImport("wtsapi32.dll")]
        internal static extern bool WTSUnRegisterSessionNotification(IntPtr hwnd);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr RegisterPowerSettingNotification(IntPtr recipient, ref Guid setting, uint flags);
        [DllImport("user32.dll")]
        internal static extern bool UnregisterPowerSettingNotification(IntPtr handle);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        internal static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("user32.dll")]
        internal static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")]
        internal static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)]
        internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)]
        internal static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFO info, uint usage,
            out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll", SetLastError = true)]
        internal static extern IntPtr SelectObject(IntPtr dc, IntPtr item);
        [DllImport("gdi32.dll")]
        internal static extern bool DeleteObject(IntPtr item);
        [DllImport("gdi32.dll")]
        internal static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)]
        internal static extern bool BitBlt(IntPtr destination, int x, int y, int width, int height,
            IntPtr source, int sourceX, int sourceY, uint operation);
        [DllImport("gdi32.dll")]
        internal static extern bool GdiFlush();

        internal static string ClassName(IntPtr hwnd)
        {
            StringBuilder name = new StringBuilder(128);
            GetClassName(hwnd, name, name.Capacity);
            return name.ToString();
        }
    }
}
