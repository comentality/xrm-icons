using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DpiSpike
{
    /// <summary>
    /// Walks the windows of a running process and says what DPI awareness each one was created
    /// with. For checking a change to XrmToolBox from outside it, without a debugger and without
    /// adding a line of logging to the thing being tested: run it, open a tool, and see whether
    /// the tool's window really is the odd one out.
    ///
    ///     WindowProbe.exe XrmToolBox
    ///
    /// Reads only. It touches nothing in the process it is looking at.
    /// </summary>
    internal static class WindowProbe
    {
        private delegate bool EnumProc(IntPtr window, IntPtr data);

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr data);
        [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr data);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int count);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
        [DllImport("user32.dll")] private static extern bool AreDpiAwarenessContextsEqual(IntPtr a, IntPtr b);
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("shcore.dll")] private static extern int GetProcessDpiAwareness(IntPtr process, out int awareness);

        private static readonly IntPtr Unaware = new IntPtr(-1);
        private static readonly IntPtr SystemAware = new IntPtr(-2);
        private static readonly IntPtr PerMonitor = new IntPtr(-3);
        private static readonly IntPtr PerMonitorV2 = new IntPtr(-4);
        private static readonly IntPtr UnawareGdiScaled = new IntPtr(-5);

        private static int Main(string[] args)
        {
            var wanted = args.Length > 0 ? args[0] : "XrmToolBox";
            var processes = Process.GetProcessesByName(wanted.Replace(".exe", ""));

            if (processes.Length == 0)
            {
                Console.WriteLine("Nothing called " + wanted + " is running.");
                return 1;
            }

            foreach (var process in processes)
            {
                int awareness;
                GetProcessDpiAwareness(process.Handle, out awareness);

                Console.WriteLine(process.ProcessName + " (" + process.Id + ") - the process is " +
                                  ProcessAwareness(awareness));
                Console.WriteLine();

                foreach (var window in TopLevelOf(process.Id))
                {
                    Say(window, 0);

                    // One level in is enough to tell a tool's window from the shell around it.
                    foreach (var child in ChildrenOf(window))
                    {
                        Say(child, 1);
                    }
                }

                Console.WriteLine();
            }

            return 0;
        }

        private static void Say(IntPtr window, int depth)
        {
            var title = Text(window);
            var name = Class(window);

            Console.WriteLine(
                new string(' ', depth * 2) +
                Name(GetWindowDpiAwarenessContext(window)).PadRight(22 - depth * 2) + "  " +
                GetDpiForWindow(window).ToString().PadLeft(3) + " dpi  " +
                name + (title.Length > 0 ? "  \"" + title + "\"" : ""));
        }

        private static IEnumerable<IntPtr> TopLevelOf(int process)
        {
            var found = new List<IntPtr>();

            EnumWindows((window, data) =>
            {
                uint owner;
                GetWindowThreadProcessId(window, out owner);

                if (owner == process && IsWindowVisible(window)) found.Add(window);
                return true;
            }, IntPtr.Zero);

            return found;
        }

        private static IEnumerable<IntPtr> ChildrenOf(IntPtr parent)
        {
            var found = new List<IntPtr>();

            EnumChildWindows(parent, (window, data) =>
            {
                // Only the ones with something to say - a WinForms window tree is hundreds deep
                // in panels, and what matters here is the forms.
                if (IsWindowVisible(window) && Text(window).Length > 0) found.Add(window);
                return true;
            }, IntPtr.Zero);

            return found;
        }

        private static string Text(IntPtr window)
        {
            var text = new StringBuilder(256);
            GetWindowText(window, text, text.Capacity);
            return text.ToString();
        }

        private static string Class(IntPtr window)
        {
            var name = new StringBuilder(256);
            GetClassName(window, name, name.Capacity);
            return name.ToString();
        }

        private static string ProcessAwareness(int awareness)
        {
            switch (awareness)
            {
                case 0: return "UNAWARE";
                case 1: return "SYSTEM_AWARE";
                case 2: return "PER_MONITOR_AWARE";
                default: return "awareness " + awareness;
            }
        }

        private static string Name(IntPtr context)
        {
            if (AreDpiAwarenessContextsEqual(context, UnawareGdiScaled)) return "UNAWARE_GDISCALED";
            if (AreDpiAwarenessContextsEqual(context, Unaware)) return "UNAWARE";
            if (AreDpiAwarenessContextsEqual(context, SystemAware)) return "SYSTEM_AWARE";
            if (AreDpiAwarenessContextsEqual(context, PerMonitorV2)) return "PER_MONITOR_V2";
            if (AreDpiAwarenessContextsEqual(context, PerMonitor)) return "PER_MONITOR";

            return "unnamed";
        }
    }
}
