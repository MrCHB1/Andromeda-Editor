using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Andromeda_Editor.Navigation
{
    public struct GlobalNavigation
    {
        public static double TickPosition = 0;
        public static double PlayheadTickPosition = 0;
        public static int CurrTrack = 0;
    }

    public struct PianoRollNavigation
    {
        // Number of ticks on-screen
        public static double XZoom = 7680;
        public static double YZoom = 1;
    }

    public struct TrackListNavigation
    {
        // XZoom is the number of bars on-screen
        public static double XZoom = 3840 * 64;
        public static double YZoom = 32;
        public static double TrackOffset = 0.0;
    }
}
