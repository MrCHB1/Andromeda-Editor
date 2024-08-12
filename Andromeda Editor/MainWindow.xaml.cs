using Andromeda_Editor.Editor;
using Andromeda_Editor.MIDI;
using Andromeda_Editor.Rendering;
using Andromeda_Editor.Navigation;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Andromeda_Editor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Low-level Chrome window code

        private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case 0x0024:
                    WmGetMinMaxInfo(hwnd, lParam);
                    handled = true;
                    break;
            }
            return (IntPtr)0;
        }

        private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
            int MONITOR_DEFAULTTONEAREST = 0x00000002;
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                MONITORINFO monitorInfo = new MONITORINFO();
                GetMonitorInfo(monitor, monitorInfo);
                RECT rcWorkArea = monitorInfo.rcWork;
                RECT rcMonitorArea = monitorInfo.rcMonitor;
                if (_fullscreen)
                    rcWorkArea = rcMonitorArea;
                mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
                mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
                mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
                mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
            }
            Marshal.StructureToPtr(mmi, lParam, true);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            /// <summary>x coordinate of point.</summary>
            public int x;
            /// <summary>y coordinate of point.</summary>
            public int y;
            /// <summary>Construct a point of coordinates (x,y).</summary>
            public POINT(int x, int y)
            {
                this.x = x;
                this.y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            public RECT rcMonitor = new RECT();
            public RECT rcWork = new RECT();
            public int dwFlags = 0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
            public static readonly RECT Empty = new RECT();
            public int Width { get { return Math.Abs(right - left); } }
            public int Height { get { return bottom - top; } }
            public RECT(int left, int top, int right, int bottom)
            {
                this.left = left;
                this.top = top;
                this.right = right;
                this.bottom = bottom;
            }
            public RECT(RECT rcSrc)
            {
                left = rcSrc.left;
                top = rcSrc.top;
                right = rcSrc.right;
                bottom = rcSrc.bottom;
            }
            public bool IsEmpty { get { return left >= right || top >= bottom; } }
            public override string ToString()
            {
                if (this == Empty) { return "RECT {Empty}"; }
                return "RECT { left : " + left + " / top : " + top + " / right : " + right + " / bottom : " + bottom + " }";
            }
            public override bool Equals(object obj)
            {
                if (!(obj is Rect)) { return false; }
                return (this == (RECT)obj);
            }
            /// <summary>Return the HashCode for this struct (not garanteed to be unique)</summary>
            public override int GetHashCode() => left.GetHashCode() + top.GetHashCode() + right.GetHashCode() + bottom.GetHashCode();
            /// <summary> Determine if 2 RECT are equal (deep compare)</summary>
            public static bool operator ==(RECT rect1, RECT rect2) { return (rect1.left == rect2.left && rect1.top == rect2.top && rect1.right == rect2.right && rect1.bottom == rect2.bottom); }
            /// <summary> Determine if 2 RECT are different(deep compare)</summary>
            public static bool operator !=(RECT rect1, RECT rect2) { return !(rect1 == rect2); }
        }

        [DllImport("user32")]
        internal static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

        [DllImport("User32")]
        internal static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        static System.Windows.WindowState _cacheWindowState;
        static bool _fullscreen = false;
        static bool Fullscreen { get => _fullscreen; }

        #endregion

        public Visibility ChromeVisibility
        {
            get { return (Visibility)GetValue(ChromeVisibilityProperty); } 
            set { SetValue(ChromeVisibilityProperty, value); }
        }

        public static readonly DependencyProperty ChromeVisibilityProperty =
            DependencyProperty.Register("ChromeVisibility", typeof(Visibility), typeof(MainWindow), new PropertyMetadata(Visibility.Visible));

        D3D11 d3d;
        Scene scene;

        public MainWindow()
        {
            InitializeComponent();
            SourceInitialized += (s, e) =>
            {
                IntPtr handle = (new WindowInteropHelper(this)).Handle;
                HwndSource.FromHwnd(handle).AddHook(new HwndSourceHook(WindowProc));

            };

            TrackBars.UpdateBarTicks();

            d3d = new D3D11();
            scene = new Scene() { Renderer = d3d };
            D3DRenderer.Renderer = scene;

            // Initialize the Bars' tick positions
        }

        #region MIDI-related functions
        
        // Triggered when the user decides to click Tools > Analysis > Quick MIDI File Analysis
        void MIAnalyzeMIDI_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog MIDIFileDialog = new OpenFileDialog();
            MIDIFileDialog.Filter = "MIDI File|*.mid";
            if ((bool)MIDIFileDialog.ShowDialog()) 
            {
                AnalyzeMIDIFile(MIDIFileDialog.FileName);
            }
        }

        // Analyzes a MIDI File
        void AnalyzeMIDIFile(string path)
        {
            Stopwatch analyzeStopwatch = new Stopwatch();

            // Since we're only analyzing the MIDI, we set analysisMode (the 2nd argument) to true to reduce RAM usage and parse at lightning speeds.
            MIDIParser midiParser = new MIDIParser(path, true);

            analyzeStopwatch.Start();
            midiParser.ParseMIDI();
            analyzeStopwatch.Stop();

            MessageBox.Show(

$@"Note Count: {midiParser.NoteCount}
Event Count: {midiParser.EventCount}
PPQ: {midiParser.PPQ}
Track Count (header): {midiParser.TrackCount}
True Track Count: {midiParser.TrueTrackCount}
Length in ticks: {midiParser.LengthTicks}",
            $"Analysis Complete (Took {analyzeStopwatch.ElapsedMilliseconds}ms)");

            // Cleanup
            midiParser.Dispose();
            GC.Collect();
        }

        #endregion

        #region Window functions
        private void CloseWindow(object sender, MouseButtonEventArgs e)
        {
            d3d.Dispose();
            Close();
        }

        private void MinimizeWindow(object sender, MouseButtonEventArgs e)
        {
            try
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
            }
            catch { }
            WindowState = WindowState.Minimized;
        }
        #endregion

        #region Unused
        private void Window_PreviewDragEnter(object sender, DragEventArgs e)
        {

        }

        private void Window_PreviewDragLeave(object sender, DragEventArgs e)
        {

        }
        #endregion

        private void RendererScroll(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftShift))
            {
                GlobalNavigation.TickPosition += e.Delta * 4.0;
                GlobalNavigation.TickPosition = Math.Max(0, GlobalNavigation.TickPosition);
            }
            else
            {
                TrackListNavigation.TrackOffset += (float)e.Delta / 500.0f;
                TrackListNavigation.TrackOffset = Math.Min(0, TrackListNavigation.TrackOffset); // prevent overscrolling
            }
        }

        private void RendererClick(object sender, MouseButtonEventArgs e)
        {
            
        }
    }
}
