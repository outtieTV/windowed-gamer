// Target Framework: .NET 9.0 (assuming Windows Desktop environment)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using static BorderlessGamingUtility.Win32;

namespace BorderlessGamingUtility
{
    // ====================================================================
    // 1. P/Invoke Declarations for Win32 API
    // ====================================================================

    public class Win32
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr handle, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;

        // Main Styles to remove
        public const int WS_BORDER = 0x00800000;
        public const int WS_DLGFRAME = 0x00400000;
        public const int WS_CAPTION = WS_BORDER | WS_DLGFRAME;
        public const int WS_SYSMENU = 0x00080000;
        public const int WS_MINIMIZEBOX = 0x00020000;
        public const int WS_MAXIMIZEBOX = 0x00010000;
        public const int WS_THICKFRAME = 0x00040000; // Sizing border

        // Extended Styles to remove
        public const int WS_EX_CLIENTEDGE = 0x00000200;
        public const int WS_EX_WINDOWEDGE = 0x00000100;
        public const int WS_EX_STATICEDGE = 0x00020000;
        public const int WS_EX_DLGMODALFRAME = 0x0001;

        public const uint SWP_FRAMECHANGED = 0x0020;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public static readonly IntPtr HWND_TOP = new IntPtr(0);

        // Monitor Constants
        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        // ShowWindow Constants
        public const int SW_RESTORE = 9;
        public const int SW_SHOWMINIMIZED = 2;


        // --- Win32 Structures ---
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor; // Full monitor coordinates
            public RECT rcWork;    // Working area coordinates
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }
    }

    // ====================================================================
    // 2. Data Models and Information Storage
    // ====================================================================

    public class WindowInfo
    {
        public IntPtr Hwnd { get; set; }
        public string Title { get; set; }
        public string ExeName { get; set; }
        public uint ProcessId { get; set; }
        public bool IsFullscreen { get; set; } = false;

        public override string ToString() => $"{(IsFullscreen ? "[F] " : "")}{Title} ({ExeName})";
    }

    // State storage for restoring the windowed position/size
    public class WindowState
    {
        public Win32.WINDOWPLACEMENT LastPlacement { get; set; }
        public int LastStyle { get; set; }
        public int LastExStyle { get; set; }
    }

    // ====================================================================
    // 3. Main Application Form
    // ====================================================================

    public class Form1 : Form
    {
        private List<WindowInfo> _openApplications = new List<WindowInfo>();
        private Dictionary<IntPtr, WindowState> _manualWindowStates = new Dictionary<IntPtr, WindowState>();

        private ListView _openAppsListView;
        private Button _fullscreenButton;
        private Button _windowedButton;
        private Button _refreshButton;

        // Timer for auto-refresh, disabled by default but useful for monitoring state
        // private System.Windows.Forms.Timer _refreshTimer; 

        public Form1()
        {
            Text = "Borderless Gaming Utility";
            MinimumSize = new Size(800, 500);
            InitializeComponents();
            RefreshWindowList();
        }

        // ====================================================================
        // 3.1 GUI Initialization
        // ====================================================================

        private void InitializeComponents()
        {
            // Set up main layout (TableLayoutPanel for list and a footer)
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(10)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 90));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 10));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // --- 1. Open Applications List (Top) ---
            _openAppsListView = CreateListView("Open Applications", "Title", "EXE");
            mainLayout.Controls.Add(_openAppsListView, 0, 0);

            // --- 2. Bottom Panel (Fullscreen/Windowed/Refresh) ---
            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 5, 0, 0),
            };

            // Fullscreen Icon: Unicode Square with four corners (U+26F6)
            _fullscreenButton = CreateButton("⛶ Fullscreen", "Apply borderless fullscreen to selected app", FullscreenClicked);
            // Windowed Icon: Unicode Window (U+1F5D7)
            _windowedButton = CreateButton("⇲ Windowed", "Restore selected app to its original windowed state", WindowedClicked);
            // Manual Refresh button
            _refreshButton = CreateButton("🔄 Refresh List", "Manually refresh the list of open windows", (s, e) => RefreshWindowList());
            _refreshButton.Font = new Font(_refreshButton.Font.FontFamily, 10, FontStyle.Regular);

            _fullscreenButton.Font = new Font(_fullscreenButton.Font.FontFamily, 10, FontStyle.Bold);
            _windowedButton.Font = new Font(_windowedButton.Font.FontFamily, 10, FontStyle.Regular);

            bottomPanel.Controls.Add(_refreshButton);
            bottomPanel.Controls.Add(_fullscreenButton);
            bottomPanel.Controls.Add(_windowedButton);

            mainLayout.Controls.Add(bottomPanel, 0, 1);

            Controls.Add(mainLayout);
        }

        private ListView CreateListView(string title, string col1, string col2)
        {
            var lv = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                BorderStyle = BorderStyle.FixedSingle,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
            };
            lv.Columns.Add(title, 350, HorizontalAlignment.Left);
            lv.Columns.Add(col1, 200, HorizontalAlignment.Left);
            lv.Columns.Add(col2, 150, HorizontalAlignment.Left);
            return lv;
        }

        private Button CreateButton(string text, string tooltip, EventHandler clickHandler)
        {
            var button = new Button
            {
                Text = text,
                Width = 140,
                Height = 35,
                Margin = new Padding(5),
                Cursor = Cursors.Hand,
            };
            button.Click += clickHandler;
            new ToolTip().SetToolTip(button, tooltip);
            return button;
        }

        // ====================================================================
        // 3.2 Main Logic: Window Discovery and List Update
        // ====================================================================

        private void RefreshWindowList()
        {
            // 1. Find all open applications
            _openApplications = FindOpenApplications();

            // 2. Update the Open Applications ListView
            UpdateOpenAppsListView();
        }

        private void UpdateOpenAppsListView()
        {
            _openAppsListView.BeginUpdate();
            _openAppsListView.Items.Clear();

            foreach (var app in _openApplications)
            {
                // Check if this window is currently tracked as fullscreen
                app.IsFullscreen = _manualWindowStates.ContainsKey(app.Hwnd);

                var item = new ListViewItem(app.ToString());
                item.SubItems.Add(app.Title);
                item.SubItems.Add(app.ExeName);
                item.Tag = app;
                _openAppsListView.Items.Add(item);
            }

            _openAppsListView.EndUpdate();
        }

        private List<WindowInfo> FindOpenApplications()
        {
            var windows = new List<WindowInfo>();
            IntPtr shellWindow = Win32.GetShellWindow();

            Win32.EnumWindows(new Win32.EnumWindowsProc((hWnd, lParam) =>
            {
                if (hWnd == shellWindow || !Win32.IsWindowVisible(hWnd))
                    return true;

                StringBuilder sbTitle = new StringBuilder(256);
                Win32.GetWindowText(hWnd, sbTitle, sbTitle.Capacity);
                string title = sbTitle.ToString().Trim();

                if (string.IsNullOrEmpty(title) || title.Length < 3)
                    return true;

                // Try to get process ID and EXE name
                Win32.GetWindowThreadProcessId(hWnd, out uint pId);
                string exeName = "Unknown.exe";
                try
                {
                    using (var process = Process.GetProcessById((int)pId))
                    {
                        exeName = process.ProcessName + ".exe";
                    }
                }
                catch { /* Access denied or process exited */ }

                // Add to list only if it looks like a real application window
                if (title.Length > 0 && !title.StartsWith("Default IME") && !title.StartsWith("MSCTFIME UI"))
                {
                    windows.Add(new WindowInfo { Hwnd = hWnd, Title = title, ExeName = exeName, ProcessId = pId });
                }

                return true;
            }), IntPtr.Zero);

            return windows.OrderBy(w => w.Title).ToList();
        }

        // ====================================================================
        // 3.3 Window State Manipulation Functions
        // ====================================================================

        private void MakeBorderlessFullscreen(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || _manualWindowStates.ContainsKey(hWnd)) return;

            var state = new WindowState();

            // --- 1. Save Original State ---
            Win32.WINDOWPLACEMENT placement = new Win32.WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);
            Win32.GetWindowPlacement(hWnd, ref placement);
            state.LastPlacement = placement;

            // Save original styles
            state.LastStyle = Win32.GetWindowLong(hWnd, Win32.GWL_STYLE);
            state.LastExStyle = Win32.GetWindowLong(hWnd, Win32.GWL_EXSTYLE);
            _manualWindowStates.Add(hWnd, state);


            // --- 2. Remove Window Chrome (Styles) ---
            int style = Win32.GetWindowLong(hWnd, Win32.GWL_STYLE);

            // Remove all borders, title bar, and sizing frame
            style &= ~Win32.WS_CAPTION;
            style &= ~Win32.WS_SYSMENU;
            style &= ~Win32.WS_MINIMIZEBOX;
            style &= ~Win32.WS_MAXIMIZEBOX;
            style &= ~Win32.WS_BORDER;
            style &= ~Win32.WS_DLGFRAME;
            style &= ~Win32.WS_THICKFRAME;

            Win32.SetWindowLong(hWnd, Win32.GWL_STYLE, style);

            // Remove extended styles for shadows/edges
            int exStyle = Win32.GetWindowLong(hWnd, Win32.GWL_EXSTYLE);
            exStyle &= ~Win32.WS_EX_CLIENTEDGE;
            exStyle &= ~Win32.WS_EX_WINDOWEDGE;
            exStyle &= ~Win32.WS_EX_STATICEDGE;
            exStyle &= ~Win32.WS_EX_DLGMODALFRAME;
            Win32.SetWindowLong(hWnd, Win32.GWL_EXSTYLE, exStyle);

            // --- 3. Get Monitor Bounds and Reposition ---
            IntPtr hMonitor = Win32.MonitorFromWindow(hWnd, Win32.MONITOR_DEFAULTTONEAREST);

            Win32.MONITORINFO monitorInfo = new Win32.MONITORINFO();
            monitorInfo.cbSize = Marshal.SizeOf(monitorInfo);

            if (Win32.GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                // Use rcMonitor (full screen bounds) for edge-to-edge fullscreen
                int x = monitorInfo.rcMonitor.Left;
                int y = monitorInfo.rcMonitor.Top;
                int width = monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left;
                int height = monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top;

                // Resize and reposition to cover the entire screen precisely
                const uint SWP_FLAGS = Win32.SWP_NOZORDER | Win32.SWP_FRAMECHANGED | Win32.SWP_SHOWWINDOW;

                Win32.SetWindowPos(
                    hWnd,
                    Win32.HWND_TOP, // Bring to the front
                    x,
                    y,
                    width,
                    height,
                    SWP_FLAGS
                );
            }
        }

        private void RestoreWindowed(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !_manualWindowStates.ContainsKey(hWnd)) return;

            WindowState state = _manualWindowStates[hWnd];

            // --- 1. Restore Original Styles ---
            Win32.SetWindowLong(hWnd, Win32.GWL_STYLE, state.LastStyle);
            Win32.SetWindowLong(hWnd, Win32.GWL_EXSTYLE, state.LastExStyle);

            // --- 2. Restore Original Placement (Position, Size, and State) ---
            Win32.WINDOWPLACEMENT placement = state.LastPlacement;
            placement.length = Marshal.SizeOf(placement);
            Win32.SetWindowPlacement(hWnd, ref placement);

            // --- 3. Force Repaint and Redraw Frame ---
            const uint SWP_FLAGS = Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_FRAMECHANGED;

            Win32.SetWindowPos(
                hWnd,
                IntPtr.Zero,
                0, 0, 0, 0,
                SWP_FLAGS
            );

            // If the window was minimized before fullscreen, we need to explicitly restore it
            if (state.LastPlacement.showCmd == Win32.SW_SHOWMINIMIZED)
            {
                Win32.ShowWindow(hWnd, Win32.SW_RESTORE);
            }

            // Remove state from tracking dictionary
            _manualWindowStates.Remove(hWnd);
        }

        // ====================================================================
        // 3.4 Button Handlers
        // ====================================================================

        private void FullscreenClicked(object sender, EventArgs e)
        {
            if (_openAppsListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select an application from the list.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var appInfo = _openAppsListView.SelectedItems[0].Tag as WindowInfo;
            IntPtr hWnd = appInfo.Hwnd;

            if (_manualWindowStates.ContainsKey(hWnd))
            {
                MessageBox.Show($"{appInfo.Title} is already in borderless fullscreen mode.", "Already Fullscreen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MakeBorderlessFullscreen(hWnd);
            RefreshWindowList();
        }

        private void WindowedClicked(object sender, EventArgs e)
        {
            if (_openAppsListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select an application from the list.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var appInfo = _openAppsListView.SelectedItems[0].Tag as WindowInfo;
            IntPtr hWnd = appInfo.Hwnd;

            if (!_manualWindowStates.ContainsKey(hWnd))
            {
                MessageBox.Show($"{appInfo.Title} is not being tracked as borderless fullscreen. Select a window currently in [F] status.", "Not Fullscreen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RestoreWindowed(hWnd);
            RefreshWindowList();
        }
    }

    // ====================================================================
    // 4. Program Entry Point
    // ====================================================================

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
