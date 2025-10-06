// Target Framework: .NET 9.0 (assuming Windows Desktop environment)
using BorderlessGamingClone;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using static BorderlessGamingClone.Win32;

namespace BorderlessGamingClone
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

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("psapi.dll")]
        public static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpFilename, int nSize);

        public const int GWL_STYLE = -16;
        public const int WS_BORDER = 0x00800000;
        public const int WS_DLGFRAME = 0x00400000;
        public const int WS_CAPTION = WS_BORDER | WS_DLGFRAME;
        public const int WS_SYSMENU = 0x00080000;
        public const int WS_MINIMIZEBOX = 0x00020000;
        public const int WS_MAXIMIZEBOX = 0x00010000;

        public const uint SWP_FRAMECHANGED = 0x0020;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_SHOWWINDOW = 0x0040;

        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;

        public static readonly IntPtr HWND_TOP = new IntPtr(0);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        // Add required P/Invokes for borderless/fullscreen
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

        // --- Win32 Constants ---
        public const int GWL_EXSTYLE = -20; // Extended Style Index

        // Extended Styles to remove
        public const int WS_EX_CLIENTEDGE = 0x00000200;
        public const int WS_EX_WINDOWEDGE = 0x00000100;
        public const int WS_EX_STATICEDGE = 0x00020000;
        public const int WS_EX_DLGMODALFRAME = 0x0001; // Can also cause a border/shadow

        // Main Styles to remove
        public const int WS_THICKFRAME = 0x00040000; // The sizing border (Same as WS_SIZEBOX)

        // Monitor Constants
        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        // ShowWindow Constants
        public const int SW_RESTORE = 9;
        public const int SW_SHOWMINIMIZED = 2;

        // --- Win32 Structures ---
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

        public override string ToString() => $"{Title} ({ExeName})";
    }

    public class FavoriteAppInfo
    {
        public string Title { get; set; }
        public string ExeName { get; set; }
        public string MatchBy { get; set; } = "Title"; // "Title" or "Exe"

        // State storage for restoring the windowed position/size
        public Win32.RECT LastRect { get; set; }
        public int LastStyle { get; set; }
        public WINDOWPLACEMENT LastPlacement { get; set; }
        public bool IsFullscreen { get; set; } = false;

        public override string ToString() => $"{(IsFullscreen ? "[F] " : "")}{Title} ({MatchBy}: {(MatchBy == "Title" ? Title : ExeName)})";

        public string GetMatchString() => MatchBy == "Title" ? Title : ExeName;
        public int LastExStyle { get; set; }

    }

    // ====================================================================
    // 3. Main Application Form
    // ====================================================================

    public class Form1 : Form
    {
        private List<WindowInfo> _openApplications = new List<WindowInfo>();
        private List<FavoriteAppInfo> _favoriteApplications = new List<FavoriteAppInfo>();
        private System.Windows.Forms.Timer _refreshTimer;

        private ListView _openAppsListView;
        private ListView _favoritesListView;
        private Button _addToFavoritesButton;
        private Button _removeFromFavoritesButton;
        private Button _fullscreenButton;
        private Button _windowedButton;
        private Dictionary<IntPtr, FavoriteAppInfo> _manualFullscreenStates = new Dictionary<IntPtr, FavoriteAppInfo>();

        public Form1()
        {
            Text = "Borderless Gaming Utility (WinForms/.NET 9)";
            MinimumSize = new Size(1200, 700);
            InitializeComponents();
            RefreshWindowListsAndApplyFavorites();
            //StartRefreshTimer();
            // NOTE ON PERSISTENCE:
            // In a real application, favorites and matching preferences would be saved to a database (like Firestore
            // as requested) or a local config file. In this single-file context, we initialize an empty list,
            // but the logic is ready to handle persistence if a database connection were available.
        }

        // ====================================================================
        // 3.1 GUI Initialization
        // ====================================================================

        private void InitializeComponents()
        {
            // Set up main layout (TableLayoutPanel for two columns and a footer)
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 3,
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 90));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 10));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

            // --- 1. Open Applications List (Left) ---
            _openAppsListView = CreateListView("Open Applications (Click or Right-Click)", "Title", "EXE");
            _openAppsListView.MouseUp += OpenAppsListView_MouseUp;
            mainLayout.Controls.Add(_openAppsListView, 0, 0);

            // --- 2. Action Buttons (Center) ---
            var centerPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(10, 50, 10, 0),
                Anchor = AnchorStyles.None,
            };

            _addToFavoritesButton = CreateButton("⯈", "Move selected application to favorites", AddToFavorites);
            _removeFromFavoritesButton = CreateButton("⯇", "Remove selected application from favorites", RemoveFromFavorites);

            centerPanel.Controls.Add(_addToFavoritesButton);
            centerPanel.Controls.Add(_removeFromFavoritesButton);
            mainLayout.Controls.Add(centerPanel, 1, 0);

            // --- 3. Favorites List (Right) ---
            _favoritesListView = CreateListView("Favorites (Auto-Fullscreen)", "Match String", "Match By");
            _favoritesListView.MouseUp += FavoritesListView_MouseUp;
            mainLayout.Controls.Add(_favoritesListView, 2, 0);

            // --- 4. Bottom Panel (Fullscreen/Windowed/Control Center) ---
            var bottomPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(10, 5, 10, 5)
            };
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33)); // Fullscreen/Windowed
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34)); // Center Arrows (Spacer/Info)
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33)); // Spacer

            // Fullscreen/Windowed Buttons (Bottom Left)
            var modePanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Fill, WrapContents = false };

            // Fullscreen Icon: Unicode Square with four corners (U+26F6) or similar
            _fullscreenButton = CreateButton("⛶ Fullscreen", "Apply borderless fullscreen to selected app", FullscreenClicked);
            // Windowed Icon: Unicode Window (U+1F5D7) or similar
            _windowedButton = CreateButton("⇲ Windowed", "Restore selected app to its original windowed state", WindowedClicked);
            // Manual Refresh button
            var refreshButton = CreateButton("🔄 Refresh", "Manually refresh the window list", (s, e) => RefreshWindowListsAndApplyFavorites());
            refreshButton.Font = new Font(refreshButton.Font.FontFamily, 10, FontStyle.Regular);
            modePanel.Controls.Add(refreshButton);

            _fullscreenButton.Font = new Font(_fullscreenButton.Font.FontFamily, 10, FontStyle.Bold);
            _windowedButton.Font = new Font(_windowedButton.Font.FontFamily, 10, FontStyle.Regular);

            modePanel.Controls.Add(_fullscreenButton);
            modePanel.Controls.Add(_windowedButton);
            modePanel.Controls.Add(refreshButton);

            bottomPanel.Controls.Add(modePanel, 0, 0);

            // Add bottom panel to main layout

            mainLayout.Controls.Add(bottomPanel, 0, 1);
            mainLayout.SetColumnSpan(bottomPanel, 3);


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
            lv.Columns.Add(title, 200, HorizontalAlignment.Left);
            lv.Columns.Add(col1, 150, HorizontalAlignment.Left);
            lv.Columns.Add(col2, 100, HorizontalAlignment.Left);
            return lv;
        }

        private Button CreateButton(string text, string tooltip, EventHandler clickHandler)
        {
            var button = new Button
            {
                Text = text,
                Width = 120,
                Height = 35,
                Margin = new Padding(5),
                Cursor = Cursors.Hand,
            };
            button.Click += clickHandler;
            new ToolTip().SetToolTip(button, tooltip);
            return button;
        }

        // ====================================================================
        // 3.2 Main Logic: Window Discovery and Manipulation
        // ====================================================================

        private void RefreshWindowListsAndApplyFavorites()
        {
            // 1. Find all open windows
            _openApplications = FindOpenApplications();

            // 2. Refresh the Open Applications ListView
            UpdateOpenAppsListView();

            // 3. Apply borderless fullscreen to any favorited window that is currently open
            ApplyBorderlessToFavorites();

            // 4. Update the Favorites ListView display (for status changes like [F])
            UpdateFavoritesListView();
        }

        private void UpdateOpenAppsListView()
        {
            _openAppsListView.BeginUpdate();
            _openAppsListView.Items.Clear();

            foreach (var app in _openApplications)
            {
                // Check if this window is already a favorited and being tracked
                if (!_favoriteApplications.Any(f => f.Title == app.Title && f.ExeName == app.ExeName))
                {
                    var item = new ListViewItem(app.Title);
                    item.SubItems.Add(app.Title);
                    item.SubItems.Add(app.ExeName);
                    item.Tag = app;
                    _openAppsListView.Items.Add(item);
                }
            }

            _openAppsListView.EndUpdate();
        }

        private void UpdateFavoritesListView()
        {
            _favoritesListView.BeginUpdate();
            _favoritesListView.Items.Clear();

            foreach (var fav in _favoriteApplications)
            {
                var item = new ListViewItem(fav.ToString());
                item.SubItems.Add(fav.GetMatchString());
                item.SubItems.Add(fav.MatchBy);
                item.Tag = fav;
                _favoritesListView.Items.Add(item);
            }

            _favoritesListView.EndUpdate();
        }

        private void ApplyBorderlessToFavorites()
        {
            var currentOpenWindows = new Dictionary<string, WindowInfo>();
            foreach (var app in _openApplications)
            {
                currentOpenWindows[$"{app.Title}|{app.ExeName}"] = app;
            }

            foreach (var fav in _favoriteApplications)
            {
                WindowInfo targetWindow = null;

                // 1. Find the target window based on matching preference
                if (fav.MatchBy == "Title")
                {
                    targetWindow = _openApplications.FirstOrDefault(app => app.Title == fav.Title);
                }
                else // MatchBy == "Exe"
                {
                    targetWindow = _openApplications.FirstOrDefault(app => app.ExeName == fav.ExeName);
                }

                // 2. Apply borderless fullscreen if found
                if (targetWindow != null)
                {
                    MakeBorderlessFullscreen(targetWindow.Hwnd, fav);
                }
                else
                {
                    // If the window is not open, ensure the favorite status is reset if it was in fullscreen mode
                    fav.IsFullscreen = false;
                }
            }
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
                string exeName = "Unknown";
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
        private int RoundUpToHundred(int value)
        {
            return ((value + 99) / 100) * 100;
        }
    private void MakeBorderlessFullscreen(IntPtr hWnd, FavoriteAppInfo fav = null)
    {
        if (hWnd == IntPtr.Zero) return;

        // --- 1. Save Original State (Only if transitioning to fullscreen) ---
        if (fav != null && !fav.IsFullscreen)
        {
            // FIX: Instantiate a new WINDOWPLACEMENT struct.
            Win32.WINDOWPLACEMENT placement = new Win32.WINDOWPLACEMENT();

            // FIX: Set the length on the local copy (which is a variable).
            placement.length = Marshal.SizeOf(placement);

            // Use the local copy in the P/Invoke call.
            Win32.GetWindowPlacement(hWnd, ref placement);

            // FIX: Assign the modified local copy back to the property.
            fav.LastPlacement = placement;

            // Save original styles
            fav.LastStyle = Win32.GetWindowLong(hWnd, Win32.GWL_STYLE);
            fav.LastExStyle = Win32.GetWindowLong(hWnd, Win32.GWL_EXSTYLE);
        }

        // --- 2. Remove Window Chrome (Styles) ---
        int style = Win32.GetWindowLong(hWnd, Win32.GWL_STYLE);

        // Remove all borders, title bar, and sizing frame for true borderless
        style &= ~Win32.WS_CAPTION;
        style &= ~Win32.WS_SYSMENU;
        style &= ~Win32.WS_MINIMIZEBOX;
        style &= ~Win32.WS_MAXIMIZEBOX;
        style &= ~Win32.WS_BORDER;
        style &= ~Win32.WS_DLGFRAME;
        style &= ~Win32.WS_THICKFRAME; // Crucial for removing the sizing frame/edge

        Win32.SetWindowLong(hWnd, Win32.GWL_STYLE, style);

        // Remove extended styles for shadows/edges
        int exStyle = Win32.GetWindowLong(hWnd, Win32.GWL_EXSTYLE);
        exStyle &= ~Win32.WS_EX_CLIENTEDGE;
        exStyle &= ~Win32.WS_EX_WINDOWEDGE; // Use Win32.WS_EX_WINDOWEDGE if defined
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
                Win32.HWND_TOP, // Use HWND_TOP to bring it to the front
                x,
                y,
                width,
                height,
                SWP_FLAGS
            );

            if (fav != null)
            {
                fav.IsFullscreen = true;
            }
        }
    }

    private void RestoreWindowed(IntPtr hWnd, FavoriteAppInfo fav)
    {
        if (hWnd == IntPtr.Zero || fav == null || !fav.IsFullscreen) return;

        // --- 1. Restore Original Styles ---
        Win32.SetWindowLong(hWnd, Win32.GWL_STYLE, fav.LastStyle);
        Win32.SetWindowLong(hWnd, Win32.GWL_EXSTYLE, fav.LastExStyle);

        // --- 2. Restore Original Placement (Position, Size, and State) ---
        // This reliably restores the window to its previous windowed, minimized, or maximized state.
        Win32.WINDOWPLACEMENT placement = new Win32.WINDOWPLACEMENT();

        // FIX: Set the length on the local copy (which is a variable).
        placement.length = Marshal.SizeOf(placement);

        // Use the local copy in the P/Invoke call.
        Win32.GetWindowPlacement(hWnd, ref placement);
        placement.length = Marshal.SizeOf(placement);
        Win32.SetWindowPlacement(hWnd, ref placement);

        // Ensure the window is redrawn after style changes and placement restoration
        const uint SWP_FLAGS = Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_FRAMECHANGED;

        // SetWindowPos here forces a repaint (SWP_FRAMECHANGED) without changing size/position
        Win32.SetWindowPos(
            hWnd,
            IntPtr.Zero,
            0, 0, 0, 0,
            SWP_FLAGS
        );

        // If the window was minimized before fullscreen, we need to explicitly restore it
        if (fav.LastPlacement.showCmd == Win32.SW_SHOWMINIMIZED)
        {
            Win32.ShowWindow(hWnd, Win32.SW_RESTORE);
        }

        fav.IsFullscreen = false;
    }




    // ====================================================================
    // 3.4 Button and Context Menu Handlers
    // ====================================================================

    private void AddToFavorites(object sender, EventArgs e)
        {
            if (_openAppsListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select an application from the left list to add to favorites.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedItem = _openAppsListView.SelectedItems[0];
            var appInfo = selectedItem.Tag as WindowInfo;

            if (_favoriteApplications.Any(f => f.Title == appInfo.Title && f.ExeName == appInfo.ExeName))
            {
                MessageBox.Show($"{appInfo.Title} is already in Favorites.", "Already Favorited", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Default match is by title
            var newFav = new FavoriteAppInfo { Title = appInfo.Title, ExeName = appInfo.ExeName, MatchBy = "Title" };
            _favoriteApplications.Add(newFav);

            RefreshWindowListsAndApplyFavorites();
        }

        private void RemoveFromFavorites(object sender, EventArgs e)
        {
            if (_favoritesListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select an application from the Favorites list to remove.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedFavItem = _favoritesListView.SelectedItems[0];
            var favInfo = selectedFavItem.Tag as FavoriteAppInfo;

            // If the window is currently open and fullscreen, restore it before removing from favorites
            var targetWindow = _openApplications.FirstOrDefault(app => app.Title == favInfo.Title || app.ExeName == favInfo.ExeName);
            if (targetWindow != null && favInfo.IsFullscreen)
            {
                RestoreWindowed(targetWindow.Hwnd, favInfo);
            }

            _favoriteApplications.Remove(favInfo);
            RefreshWindowListsAndApplyFavorites();
        }

        private void FullscreenClicked(object sender, EventArgs e)
        {
            if (_openAppsListView.SelectedItems.Count == 0 && _favoritesListView.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select an application from either list.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            IntPtr hWnd = IntPtr.Zero;
            FavoriteAppInfo favInfo = null;

            if (_favoritesListView.SelectedItems.Count > 0)
            {
                favInfo = _favoritesListView.SelectedItems[0].Tag as FavoriteAppInfo;
                // Find the currently open window matching the favorite rule
                var targetWindow = _openApplications.FirstOrDefault(app =>
                    (favInfo.MatchBy == "Title" && app.Title == favInfo.Title) ||
                    (favInfo.MatchBy == "Exe" && app.ExeName == favInfo.ExeName)
                );
                hWnd = targetWindow?.Hwnd ?? IntPtr.Zero;
            }
            else if (_openAppsListView.SelectedItems.Count > 0)
            {
                var appInfo = _openAppsListView.SelectedItems[0].Tag as WindowInfo;
                hWnd = appInfo.Hwnd;
                // Since this is a manual fullscreen, we temporarily create a FavoriteAppInfo to track state for restoration
                favInfo = new FavoriteAppInfo { Title = appInfo.Title, ExeName = appInfo.ExeName, MatchBy = "Title" };
                // This temporary 'favInfo' is not added to the main list but allows the window to be restored later.
                // In a real app, this manual state would need to be tracked separately.
            }

            if (hWnd != IntPtr.Zero)
            {
                MakeBorderlessFullscreen(hWnd, favInfo);
                // after MakeBorderlessFullscreen(hWnd, favInfo);
                if (favInfo != null && hWnd != IntPtr.Zero)
                {
                    // MakeBorderlessFullscreen saves favInfo.LastRect and favInfo.LastStyle when favInfo != null
                    _manualFullscreenStates[hWnd] = favInfo;
                }
            }
            else
            {
                MessageBox.Show("Selected application window not found or is already in fullscreen mode.", "Window Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void WindowedClicked(object sender, EventArgs e)
        {
            // Try to restore from either list
            FavoriteAppInfo favInfo = null;
            IntPtr hWnd = IntPtr.Zero;

            // Case 1: Selected from Favorites
            if (_favoritesListView.SelectedItems.Count > 0)
            {
                favInfo = _favoritesListView.SelectedItems[0].Tag as FavoriteAppInfo;
                var targetWindow = _openApplications.FirstOrDefault(app =>
                    (favInfo.MatchBy == "Title" && app.Title == favInfo.Title) ||
                    (favInfo.MatchBy == "Exe" && app.ExeName == favInfo.ExeName)
                );
                hWnd = targetWindow?.Hwnd ?? IntPtr.Zero;

                if (hWnd != IntPtr.Zero)
                {
                    RestoreWindowed(hWnd, favInfo);
                    RefreshWindowListsAndApplyFavorites();
                    return;
                }
            }

            // Case 2: Selected from Open Apps (manual restore)
            if (_openAppsListView.SelectedItems.Count > 0)
            {
                var appInfo = _openAppsListView.SelectedItems[0].Tag as WindowInfo;

                if (hWnd != IntPtr.Zero)
                {
                    if (_manualFullscreenStates.TryGetValue(hWnd, out var storedFav))
                    {
                        RestoreWindowed(hWnd, storedFav);
                        _manualFullscreenStates.Remove(hWnd);
                        RefreshWindowListsAndApplyFavorites();
                        return;
                    }

                    // No stored state -> best-effort fallback: re-add standard styles and resize (not guaranteed to match original).
                    int style = Win32.GetWindowLong(hWnd, Win32.GWL_STYLE);
                    style |= Win32.WS_CAPTION | Win32.WS_SYSMENU | Win32.WS_MINIMIZEBOX | Win32.WS_MAXIMIZEBOX;
                    Win32.SetWindowLong(hWnd, Win32.GWL_STYLE, style);
                    // place somewhere reasonable
                    Win32.SetWindowPos(hWnd, Win32.HWND_TOP, 100, 100, 800, 600, Win32.SWP_FRAMECHANGED);
                    RefreshWindowListsAndApplyFavorites();
                    return;
                }
            }
        }

        private void OpenAppsListView_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && _openAppsListView.SelectedItems.Count > 0)
            {
                ShowContextMenu(_openAppsListView.SelectedItems[0].Tag as WindowInfo, _openAppsListView);
            }
        }

        private void FavoritesListView_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && _favoritesListView.SelectedItems.Count > 0)
            {
                ShowContextMenu(_favoritesListView.SelectedItems[0].Tag as FavoriteAppInfo, _favoritesListView);
            }
        }

        private void ShowContextMenu(object target, ListView sourceListView)
        {
            var menu = new ContextMenuStrip();
            ToolStripMenuItem matchByTitle = new ToolStripMenuItem("Match on Title");
            ToolStripMenuItem matchByExe = new ToolStripMenuItem("Match on EXE");

            // Determine if we are dealing with a new open app or an existing favorite
            string currentMatchBy = "Title";
            FavoriteAppInfo fav = target as FavoriteAppInfo;

            if (fav != null)
            {
                currentMatchBy = fav.MatchBy;
            }

            matchByTitle.Checked = currentMatchBy == "Title";
            matchByExe.Checked = currentMatchBy == "Exe";

            matchByTitle.Click += (s, e) => SetMatchBy(target, "Title");
            matchByExe.Click += (s, e) => SetMatchBy(target, "Exe");

            menu.Items.Add(matchByTitle);
            menu.Items.Add(matchByExe);

            menu.Show(sourceListView, new Point(Cursor.Position.X - sourceListView.Left, Cursor.Position.Y - sourceListView.Top));
        }

        private void SetMatchBy(object target, string matchType)
        {
            FavoriteAppInfo fav = target as FavoriteAppInfo;

            if (fav == null)
            {
                // If right-clicked on Open Apps list, we can only set the preference once it's a favorite.
                MessageBox.Show("Matching preference can only be set for items in the Favorites list.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            fav.MatchBy = matchType;
            // Uncheck other options in the menu by re-drawing
            RefreshWindowListsAndApplyFavorites();
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
