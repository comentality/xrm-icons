using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DpiSpike
{
    /// <summary>
    /// Asks Windows whether XrmToolBox could become DPI-aware without every tool in it having to
    /// become DPI-aware on the same day.
    ///
    /// The shape being tested is the one XrmToolBox already has: a per-monitor-v2 process whose
    /// main window is an MDI container, with each tool a child form of its own - see NewForm,
    /// PluginForm and the DockPanel between them. If a child form can be created DPI-unaware
    /// inside that aware process, then a host that scales properly can go on handing every
    /// existing tool the 96 dpi it was written against, and a tool opts in when it is ready.
    /// If it cannot, "nothing changes for tools that do not opt in" is not a promise anybody can
    /// make, and MscrmTools/XrmToolBox#1260 is as big as it looks.
    ///
    /// Two halves. The structural half runs anywhere, including a 96-dpi machine: it asks each
    /// window what awareness it was created with, which is a fact about the window and not about
    /// the screen. The visual half needs a monitor at 150% and a pair of eyes - the host should
    /// be crisp and the unaware child should be a bitmap stretch of a 96-dpi drawing, and
    /// neither should be the other.
    /// </summary>
    internal static class Program
    {
        // DPI_AWARENESS_CONTEXT values are sentinel handles, not indexes.
        private static readonly IntPtr Unaware = new IntPtr(-1);
        private static readonly IntPtr SystemAware = new IntPtr(-2);
        private static readonly IntPtr PerMonitor = new IntPtr(-3);
        private static readonly IntPtr PerMonitorV2 = new IntPtr(-4);

        /// <summary>
        /// Unaware, but drawn by GDI at the real resolution before being handed back - so text in
        /// a window created with this is sharp where plain UNAWARE is a stretched bitmap. 1809 and
        /// later. It is the interesting one for XrmToolBox: a tool that has opted into nothing
        /// would come out better than it is today rather than merely no worse.
        /// </summary>
        private static readonly IntPtr UnawareGdiScaled = new IntPtr(-5);

        private const int HostingInvalid = -1;
        private const int HostingDefault = 0;
        private const int HostingMixed = 1;

        [DllImport("user32.dll")] private static extern int SetThreadDpiHostingBehavior(int behaviour);
        [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr GetThreadDpiAwarenessContext();
        [DllImport("user32.dll")] private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] private static extern bool AreDpiAwarenessContextsEqual(IntPtr a, IntPtr b);
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var headless = Array.IndexOf(args, "-headless") >= 0;

            // The control case: everything the same except that the host never asks for mixed
            // hosting. What the children come out as then is what says whether the asking is
            // what did it.
            var plainHosting = Array.IndexOf(args, "-default-hosting") >= 0;

            Console.WriteLine("Process was created with: " + Name(GetThreadDpiAwarenessContext()));
            Console.WriteLine();

            // Mixed hosting is a property of the parent, read when the parent's window is made -
            // so it goes on before the host form exists, not around the child. A host that does
            // not ask for it gets children of its own awareness whatever the thread says at the
            // moment they are created, which is the whole question this file is about.
            if (plainHosting)
            {
                Console.WriteLine("Hosting left at DEFAULT, on purpose.");
            }
            else
            {
                // The call answers with the behaviour the thread had before, so DEFAULT is a
                // success and INVALID is the failure. A Windows without the API at all does not
                // return anything - the call does not bind, and that is caught here rather than
                // taking the spike down.
                try
                {
                    var was = SetThreadDpiHostingBehavior(HostingMixed);
                    Console.WriteLine(was == HostingInvalid
                        ? "SetThreadDpiHostingBehavior(MIXED) was refused."
                        : "SetThreadDpiHostingBehavior(MIXED) accepted, replacing " +
                          (was == HostingDefault ? "DEFAULT" : "MIXED") + ".");
                }
                catch (EntryPointNotFoundException)
                {
                    Console.WriteLine("SetThreadDpiHostingBehavior is not on this Windows - it needs 1803.");
                }
            }

            Console.WriteLine();

            using (var host = new Host())
            {
                host.Show();

                // Created with nothing said about it, so it is the process's own awareness. This
                // is a tool that has opted in.
                var aware = host.OpenChild("Opted in - per-monitor v2", IntPtr.Zero);

                // Created under a context of its own. This is every tool that has not.
                var unaware = host.OpenChild("Not opted in - unaware, GDI-scaled", UnawareGdiScaled);

                // And the same thing without GDI scaling, which is what a tool gets today: a
                // bitmap of a 96-dpi window, stretched.
                var stretched = host.OpenChild("Not opted in - plain unaware", Unaware);

                Console.WriteLine("Window                              awareness                     dpi  font");
                Console.WriteLine("----------------------------------  ---------------------------  ----  ----");
                Say("host (the MDI container)", host);
                Say("child, opted in", aware);
                Say("child, unaware + GDI scaled", unaware);
                Say("child, plain unaware", stretched);
                Console.WriteLine();

                // The tool is only one thing if what it makes later comes out the way it did.
                Console.WriteLine("A control added to each child after the context was put back:");
                Console.WriteLine("  in the opted-in child:     " + Name(aware.LateContext));
                Console.WriteLine("  in the GDI-scaled child:   " + Name(unaware.LateContext));
                Console.WriteLine("  in the plain unaware child: " + Name(stretched.LateContext));

                var inherited = Same(unaware.LateContext, Context(unaware)) &&
                                Same(stretched.LateContext, Context(stretched));

                Console.WriteLine(inherited
                    ? "  -> a child window takes its parent's awareness, so one call at the tool's own"
                    + Environment.NewLine +
                      "     window covers everything the tool goes on to make."
                    : "  -> a child window does NOT take its parent's awareness, so the context has to be"
                    + Environment.NewLine +
                      "     held for as long as the tool is making windows, not just around its first.");
                Console.WriteLine();

                var mixed = !Same(Context(host), Context(unaware));
                Console.WriteLine(mixed
                    ? "MIXED AWARENESS HOLDS: the child is not the awareness the process is."
                    : "NO MIXED AWARENESS: the child came out the awareness of the process, so a tool " +
                      "cannot be left at 96 dpi inside an aware XrmToolBox.");

                if (headless)
                {
                    Console.WriteLine();
                    Console.WriteLine("Headless, so nothing was looked at. The rest of this needs a 150% monitor:");
                    Console.WriteLine("  - the host's text should be sharp and its 96-pixel square should measure 144;");
                    Console.WriteLine("  - the unaware child's square should measure 96 on the screen, stretched;");
                    Console.WriteLine("  - GDI-scaled should be the same size as plain unaware and sharper.");
                    Console.WriteLine("Drag a child between monitors of different scales and watch the log in each");
                    Console.WriteLine("window: an unaware child that is resized by WinForms on a DPI change is being");
                    Console.WriteLine("scaled twice, and that is the thing that would break the promise.");
                    return mixed ? 0 : 1;
                }

                Application.Run(host);
                return mixed ? 0 : 1;
            }
        }

        private static void Say(string what, Form window)
        {
            Console.WriteLine(what.PadRight(34) + "  " +
                              Name(Context(window)).PadRight(27) + "  " +
                              GetDpiForWindow(window.Handle).ToString().PadLeft(4) + "  " +
                              window.Font.Height.ToString().PadLeft(4));
        }

        private static IntPtr Context(Form window)
        {
            return GetWindowDpiAwarenessContext(window.Handle);
        }

        private static bool Same(IntPtr a, IntPtr b)
        {
            return AreDpiAwarenessContextsEqual(a, b);
        }

        /// <summary>
        /// The context by the name it is written under. Asked context by context rather than
        /// through GetAwarenessFromDpiAwarenessContext alone, which cannot tell v1 from v2 and
        /// calls both of them per-monitor.
        /// </summary>
        private static string Name(IntPtr context)
        {
            if (Same(context, UnawareGdiScaled)) return "UNAWARE_GDISCALED";
            if (Same(context, Unaware)) return "UNAWARE";
            if (Same(context, SystemAware)) return "SYSTEM_AWARE";
            if (Same(context, PerMonitorV2)) return "PER_MONITOR_AWARE_V2";
            if (Same(context, PerMonitor)) return "PER_MONITOR_AWARE";

            return "unnamed (awareness " + GetAwarenessFromDpiAwarenessContext(context) + ")";
        }

        /// <summary>The main window, standing in for NewForm: an MDI container with tools in it.</summary>
        private sealed class Host : Form
        {
            internal Host()
            {
                Text = "DPI spike - the host";
                IsMdiContainer = true;
                Width = 1100;
                Height = 760;
                StartPosition = FormStartPosition.CenterScreen;
            }

            /// <summary>
            /// A tool, opened the way XrmToolBox opens one: built with no parent, then shown into
            /// the container, which is the moment its window is made. The context is set around
            /// that moment and put back after, so it belongs to this window and not to the
            /// program.
            /// </summary>
            private int opened;

            internal Child OpenChild(string title, IntPtr context)
            {
                // Side by side, because the whole point is comparing one against the next: at
                // 150% the three red squares should not be the same size on the screen.
                var child = new Child(title)
                {
                    MdiParent = this,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(opened++ * 360 + 6, 6),
                    Size = new Size(350, 430)
                };

                if (context == IntPtr.Zero)
                {
                    child.Show();
                    child.AddLateControl();
                    return child;
                }

                var previous = SetThreadDpiAwarenessContext(context);
                try
                {
                    child.Show();
                }
                finally
                {
                    SetThreadDpiAwarenessContext(previous);
                }

                // Deliberately after the context has been put back, because that is when most of
                // a tool's windows are really made: a dialog it opens, a ListView that recreates
                // its handle when a property changes, anything added once the tool is running.
                // If those come out aware inside an unaware tool, setting the context around one
                // Show is not enough and the whole tool is not one thing.
                child.AddLateControl();

                return child;
            }
        }

        /// <summary>
        /// A tool. Says what it was created as and what it is being drawn at, and draws one
        /// square of a size written down in pixels - which is what every layout in an XrmToolBox
        /// tool is made of, and so the thing to look at on a 150% screen.
        /// </summary>
        private sealed class Child : Form
        {
            private readonly Label _facts;
            private readonly ListBox _log;

            internal Child(string title)
            {
                Text = title;
                Width = 460;
                Height = 420;

                _facts = new Label { Dock = DockStyle.Top, Height = 96, Padding = new Padding(8) };

                var square = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 96,
                    BackColor = Color.FromArgb(0xC0, 0x39, 0x2B),
                    Margin = new Padding(8)
                };

                _log = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };

                var pop = new Button
                {
                    Dock = DockStyle.Bottom,
                    Height = 28,
                    Text = "Float this one out (what dragging a tool off the dock does)"
                };
                pop.Click += (s, e) => Float();

                Controls.Add(_log);
                Controls.Add(square);
                Controls.Add(_facts);
                Controls.Add(pop);
            }

            /// <summary>
            /// The window of a control made after the tool was opened, and the awareness it came
            /// out with. See where it is called from.
            /// </summary>
            internal IntPtr LateContext { get; private set; }

            protected override void OnShown(EventArgs e)
            {
                base.OnShown(e);
                Refresh_();
            }

            internal void AddLateControl()
            {
                var late = new Panel { Dock = DockStyle.Bottom, Height = 24, BackColor = Color.Gainsboro };
                Controls.Add(late);
                late.CreateControl();

                LateContext = GetWindowDpiAwarenessContext(late.Handle);
            }

            protected override void OnDpiChangedAfterParent(EventArgs e)
            {
                base.OnDpiChangedAfterParent(e);
                Note("DpiChangedAfterParent - now " + DeviceDpi + " dpi, font " + Font.Height);
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                Note("size " + Width + "x" + Height + ", font " + Font.Height + ", DeviceDpi " + DeviceDpi);
            }

            /// <summary>
            /// The window is made again somewhere else, which is what XrmToolBox does when a tool
            /// is dragged off the dock into a CustomFloatWindow - and a context that was only set
            /// around the first Show does not survive it. Whether it does is the second thing
            /// this spike is for.
            /// </summary>
            private void Float()
            {
                var was = Name(GetWindowDpiAwarenessContext(Handle));

                MdiParent = null;
                Show();

                var now = Name(GetWindowDpiAwarenessContext(Handle));

                Note("floated out: was " + was + ", now " + now +
                     (was == now ? " - kept" : " - LOST, so the context has to be set again on every handle"));
                Refresh_();
            }

            private void Refresh_()
            {
                _facts.Text =
                    "Created as: " + Name(GetWindowDpiAwarenessContext(Handle)) + Environment.NewLine +
                    "GetDpiForWindow: " + GetDpiForWindow(Handle) + "   DeviceDpi: " + DeviceDpi + Environment.NewLine +
                    "Font: " + Font.Name + " " + Font.Size + "pt, " + Font.Height + " pixels high" + Environment.NewLine +
                    "The red square is 96 pixels as this window counts them.";
            }

            private void Note(string line)
            {
                if (_log == null || _log.IsDisposed) return;

                _log.Items.Add(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + line);
                _log.TopIndex = _log.Items.Count - 1;
            }
        }
    }
}
