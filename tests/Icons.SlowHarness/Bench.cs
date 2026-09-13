using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using McTools.Xrm.Connection;
using ThreadExceptionEventHandler = System.Threading.ThreadExceptionEventHandler;

namespace Icons.SlowHarness
{
    /// <summary>One thing that happens, at a moment, to a tool somebody is using.</summary>
    public class Beat
    {
        public int At;
        public string Label;
        public Action<Run> Do;
    }

    /// <summary>
    /// A tool in a window, an environment that answers slowly, and a list of things that go
    /// wrong. Handed to every beat of a scenario.
    /// </summary>
    public class Run
    {
        public Form Form;
        public IconsControl Control;
        public Probe Probe;
        public SlowService Service;
        public string ShotDir;

        /// <summary>Everything the run has to answer for, findings and crashes alike.</summary>
        public readonly List<string> Failures = new List<string>();

        /// <summary>Captions of every dialog the tool put up, in order, answered as they appeared.</summary>
        public readonly List<string> Dialogs = new List<string>();

        public Stopwatch Clock = new Stopwatch();

        public void Check(bool ok, string what)
        {
            if (!ok) Failures.Add(Clock.ElapsedMilliseconds + "ms  " + what);
        }

        /// <summary>
        /// Hands the tool a different environment, the way XrmToolBox does when the connection
        /// in the toolbar changes.
        /// </summary>
        public void SwitchTo(SlowService service)
        {
            // The empty action name, not null: with anything else the base class goes looking
            // for a method of that name to run against the new connection.
            SwitchTo(service, "");
        }

        /// <summary>
        /// The same, for a connection made because somebody pressed a button while there was no
        /// connection to press it against. XrmToolBox carries the method's name on the update and
        /// the base class runs it - so this is how that arrival is driven here.
        /// </summary>
        public void SwitchTo(SlowService service, string actionName)
        {
            Control.UpdateConnection(service, new ConnectionDetail(), actionName, null);
            Service = service;
        }
    }

    public class Scenario
    {
        public string Name;

        /// <summary>One line, printed beside the result: which finding this is about.</summary>
        public string Why;

        /// <summary>How the environment behaves, set before anything is pressed.</summary>
        public Action<SlowService> Wire;

        /// <summary>
        /// A host to stand up before the tool is handed its connection, for the one thing this
        /// tool asks for over HTTP rather than through the SDK. What it returns is where the org
        /// says it lives, and it is taken down with the window.
        /// </summary>
        public Func<DeadHost> Host;

        public readonly List<Beat> Beats = new List<Beat>();

        public Scenario At(int ms, string label, Action<Run> what)
        {
            Beats.Add(new Beat { At = ms, Label = label, Do = what });
            return this;
        }

        /// <summary>
        /// Runs the whole scenario in a window of its own, and returns what went wrong.
        ///
        /// Everything happens on the UI thread with a live message loop, because that is where
        /// the tool lives: WorkAsync marshals its callback back here, and a scenario that drove
        /// the control from anywhere else would be testing something nobody does.
        /// </summary>
        public List<string> Play(string root)
        {
            var run = new Run { ShotDir = Path.Combine(root, Name) };

            if (Directory.Exists(run.ShotDir)) Directory.Delete(run.ShotDir, true);
            Directory.CreateDirectory(run.ShotDir);

            run.Service = SlowService.Sampled();
            if (Wire != null) Wire(run.Service);

            // Before the connection rather than with it: the address is read off the detail the
            // moment a load starts, so the host has to be listening by then.
            var host = Host == null ? null : Host();

            run.Control = new IconsControl { Dock = DockStyle.Fill };
            Connect(run.Control, run.Service, host == null ? null : host.Url);
            run.Probe = new Probe(run.Control);

            run.Form = new QuietForm
            {
                Text = "Icons — " + Name,
                ClientSize = new Size(1180, 760)
            };
            run.Form.Controls.Add(run.Control);

            ThreadExceptionEventHandler onThreadException = (s, e) =>
                run.Failures.Add(run.Clock.ElapsedMilliseconds + "ms  the tool threw: " + Head(e.Exception));
            Application.ThreadException += onThreadException;

            var sweeper = new Dialogs(run);

            run.Form.Shown += (s, e) =>
            {
                run.Clock.Start();
                sweeper.Start();

                var queue = new Queue<Beat>(Beats.OrderBy(b => b.At));
                var shot = 0;
                var pump = new Timer { Interval = 25 };
                pump.Tick += (s2, e2) =>
                {
                    while (queue.Count > 0 && run.Clock.ElapsedMilliseconds >= queue.Peek().At)
                    {
                        var beat = queue.Dequeue();
                        try
                        {
                            beat.Do(run);
                        }
                        catch (Exception ex)
                        {
                            run.Failures.Add(run.Clock.ElapsedMilliseconds + "ms  \"" + beat.Label
                                             + "\" threw: " + Head(ex));
                        }

                        // A shot per beat, so a failure is something to look at rather than only
                        // a line to read. Named in order, because the order is the story.
                        try
                        {
                            Capture.Of(run.Form, Path.Combine(run.ShotDir,
                                (++shot).ToString("00") + "-" + Slug(beat.Label) + ".png"));
                        }
                        catch (Exception)
                        {
                            // A shot that cannot be taken is not worth failing a scenario over.
                        }
                    }

                    if (queue.Count > 0) return;

                    pump.Stop();
                    sweeper.Stop();
                    run.Form.Close();
                };
                pump.Start();
            };

            try
            {
                Application.Run(run.Form);
            }
            finally
            {
                Application.ThreadException -= onThreadException;
                sweeper.Stop();
                if (!run.Control.IsDisposed) run.Control.Dispose();
                run.Form.Dispose();
                if (host != null) host.Dispose();
            }

            return run.Failures;
        }

        /// <summary>
        /// Hands the control its first connection, the way XrmToolBox does. The setter is
        /// protected, because outside a host nothing has any business setting one - so a harness
        /// standing in for the host reaches it the way it reaches everything else here.
        /// </summary>
        public static void Connect(IconsControl control, Microsoft.Xrm.Sdk.IOrganizationService service)
        {
            Connect(control, service, null);
        }

        /// <summary>
        /// The same, for a harness that is talking to a real environment and so can say where it
        /// is. Without a detail the tool cannot ask the org what it already draws, and the whole
        /// stock-icon read is skipped - which is correct behaviour, and not what an e2e against
        /// a real org is there to prove.
        /// </summary>
        public static void Connect(IconsControl control, Microsoft.Xrm.Sdk.IOrganizationService service,
                                   string orgUrl)
        {
            const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                                     | BindingFlags.DeclaredOnly;

            if (!string.IsNullOrEmpty(orgUrl)) Detail(control, orgUrl);

            for (var type = control.GetType(); type != null; type = type.BaseType)
            {
                var property = type.GetProperty("Service", any);
                var setter = property == null ? null : property.GetSetMethod(true);
                if (setter != null)
                {
                    setter.Invoke(control, new object[] { service });
                    return;
                }

                var backing = type.GetField("<Service>k__BackingField", any);
                if (backing != null)
                {
                    backing.SetValue(control, service);
                    return;
                }
            }

            throw new MissingMemberException("PluginControlBase", "Service");
        }

        /// <summary>
        /// The connection detail XrmToolBox would have filled in, with the one field the tool
        /// reads off it: where the org lives. Set the same way the service is, and for the same
        /// reason - the setter belongs to the host.
        /// </summary>
        private static void Detail(IconsControl control, string orgUrl)
        {
            const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                                     | BindingFlags.DeclaredOnly;

            var detail = new ConnectionDetail
            {
                WebApplicationUrl = orgUrl,
                OriginalUrl = orgUrl,
                OrganizationServiceUrl = orgUrl.TrimEnd('/') + "/XRMServices/2011/Organization.svc"
            };

            for (var type = control.GetType(); type != null; type = type.BaseType)
            {
                var property = type.GetProperty("ConnectionDetail", any);
                var setter = property?.GetSetMethod(true);
                if (setter != null)
                {
                    setter.Invoke(control, new object[] { detail });
                    return;
                }

                var backing = type.GetField("<ConnectionDetail>k__BackingField", any);
                if (backing != null)
                {
                    backing.SetValue(control, detail);
                    return;
                }
            }

            throw new MissingMemberException("PluginControlBase", "ConnectionDetail");
        }

        private static string Head(Exception ex)
        {
            var line = ex.GetBaseException().ToString();
            var stop = line.IndexOf('\n');
            return (stop < 0 ? line : line.Substring(0, stop)).Trim();
        }

        private static string Slug(string label)
        {
            var sb = new StringBuilder();
            foreach (var c in label ?? "beat")
            {
                sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
            }

            return sb.ToString().Trim('-');
        }
    }

    /// <summary>
    /// Answers whatever modal the tool puts up, and writes down that it did.
    ///
    /// Nothing here is a workaround for the tool being awkward: a confirm before a write and a
    /// message box after one are exactly right, and a run with nobody at the keyboard has to
    /// answer them or hang. The captions are worth keeping either way - "one confirm, one
    /// report" is exactly the assertion a double press is caught by.
    ///
    /// The confirm is answered OK, everything else closed: WM_CLOSE on an OKCancel box is its
    /// Cancel button, which would silently call off every apply the suite tries to make.
    /// </summary>
    public class Dialogs
    {
        private const int WM_CLOSE = 0x0010;
        private const int BM_CLICK = 0x00F5;
        private const int IDOK = 1;

        [DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(uint threadId, EnumThreadDelegate callback, IntPtr param);

        private delegate bool EnumThreadDelegate(IntPtr hwnd, IntPtr param);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern IntPtr PostMessage(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDlgItem(IntPtr hwnd, int id);

        private readonly Run _run;
        private readonly Timer _timer;

        public Dialogs(Run run)
        {
            _run = run;
            // Faster than a person, slower than the message loop. A modal dialog runs its own
            // loop and pumps WM_TIMER, which is the whole reason this works from inside one.
            _timer = new Timer { Interval = 120 };
            _timer.Tick += (s, e) => Sweep();
        }

        public void Start() { _timer.Start(); }

        public void Stop() { _timer.Stop(); _timer.Dispose(); }

        /// <summary>
        /// The dialogs a run is supposed to say yes to. Everything else is closed, and WM_CLOSE
        /// on an OKCancel box is its Cancel button - so a confirm missing from this list is a
        /// write the suite quietly calls off and then wonders why nothing happened.
        /// </summary>
        private static bool Confirms(string caption)
        {
            return caption == "Set table icon"
                || caption == "Undo the last change"
                || caption == "Do it again";
        }

        private void Sweep()
        {
            var mine = _run.Form.IsDisposed ? IntPtr.Zero : _run.Form.Handle;
            EnumThreadWindows(GetCurrentThreadId(), (hwnd, param) =>
            {
                if (hwnd == mine || !IsWindowVisible(hwnd)) return true;

                var caption = new StringBuilder(260);
                GetWindowText(hwnd, caption, caption.Capacity);
                var text = caption.ToString();
                if (text.Length == 0) return true;

                _run.Dialogs.Add(text);

                var ok = Confirms(text) ? GetDlgItem(hwnd, IDOK) : IntPtr.Zero;
                if (ok != IntPtr.Zero) PostMessage(ok, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                else PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                return true;
            }, IntPtr.Zero);
        }
    }

    /// <summary>
    /// The window a scenario plays out in. These are real windows on a real desktop, but nothing
    /// here needs them to be seen: <see cref="Capture"/> asks the window for its own pixels and
    /// the gestures are performed on the controls rather than typed at them. So they open past
    /// the right edge of every monitor and never take the keyboard, and the machine stays usable
    /// while a suite runs. Set ICONS_HARNESS_ONSCREEN=1 to watch a scenario play out instead.
    /// </summary>
    public class QuietForm : Form
    {
        public static bool OnScreen
        {
            get { return Environment.GetEnvironmentVariable("ICONS_HARNESS_ONSCREEN") == "1"; }
        }

        protected override bool ShowWithoutActivation
        {
            get { return !OnScreen; }
        }

        public QuietForm()
        {
            StartPosition = FormStartPosition.Manual;

            if (OnScreen)
            {
                Location = new Point(0, 0);
                TopMost = true;
            }
            else
            {
                var desktop = SystemInformation.VirtualScreen;
                Location = new Point(desktop.Right + 64, desktop.Top + 64);
                ShowInTaskbar = false;
            }
        }
    }

    /// <summary>A window's own pixels, on disk.</summary>
    public static class Capture
    {
        private const uint PW_RENDERFULLCONTENT = 0x00000002;

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        /// <summary>
        /// Asks the window for its own pixels rather than reading them off the screen: scraping
        /// the screen fails outright on a locked workstation, and off screen there is nothing at
        /// those coordinates to grab.
        /// </summary>
        public static void Of(Form form, string outPath)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using (var bmp = new Bitmap(form.Bounds.Width, form.Bounds.Height))
            {
                var printed = false;
                using (var g = Graphics.FromImage(bmp))
                {
                    var hdc = g.GetHdc();
                    try { printed = PrintWindow(form.Handle, hdc, PW_RENDERFULLCONTENT); }
                    finally { g.ReleaseHdc(hdc); }
                }

                if (!printed)
                    using (var g = Graphics.FromImage(bmp))
                        g.CopyFromScreen(form.Bounds.Location, Point.Empty, form.Bounds.Size);

                bmp.Save(outPath, ImageFormat.Png);
            }
        }
    }
}
