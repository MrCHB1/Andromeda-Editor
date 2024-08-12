using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Andromeda_Editor.Editor.Settings
{
    public struct RenderSettings
    {
        public const int BarBufferLength = 1 << 12;
        public static int RenderThreads = 16;
        public static bool MultiThreadedRendering = false;
    }
}
