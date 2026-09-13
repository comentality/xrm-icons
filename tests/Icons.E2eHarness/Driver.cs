using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Icons.SlowHarness;
using Microsoft.Xrm.Sdk;
using ThreadExceptionEventHandler = System.Threading.ThreadExceptionEventHandler;

namespace Icons.E2eHarness
{
    /// <summary>
    /// One gesture against the real org, and what has to be true before the next one is made.
    /// Unlike the slow harness there is no clock to choreograph against: a real org answers
    /// when it answers, so a step waits on its condition rather than on a timestamp.
    /// </summary>
    public class Step
    {
        public string Label;

        /// <summary>Null for a step that only asserts: see <see cref="Until"/>.</summary>
        public Action<Run> Do;

        /// <summary>Null means the step is done the moment it ran.</summary>
        public Func<Run, bool> Until;

        public int TimeoutMs = 60000;
    }

    /// <summary>
    /// The slow harness's bench, re-pointed at a real environment: the same control in the same
    /// window with the same dialog sweeper, driven by steps that wait for the org instead of
    /// beats that assume its timing. A step that throws or times out stops the run - the steps
    /// after it would be gestures against a window in an unknown state.
    /// </summary>
    public static class Driver
    {
        public static List<string> Play(IOrganizationService service, List<Step> steps, string shotDir,
                                        string orgUrl)
        {
            var run = new Run { ShotDir = shotDir };

            if (Directory.Exists(shotDir)) Directory.Delete(shotDir, true);
            Directory.CreateDirectory(shotDir);

            run.Control = new IconsControl { Dock = DockStyle.Fill };
            Scenario.Connect(run.Control, service, orgUrl);
            run.Probe = new Probe(run.Control);

            run.Form = new QuietForm
            {
                Text = "Icons — e2e",
                ClientSize = new Size(1180, 760)
            };
            run.Form.Controls.Add(run.Control);

            ThreadExceptionEventHandler onThreadException = (s, e) =>
                run.Failures.Add(run.Clock.ElapsedMilliseconds + "ms  the tool threw: "
                                 + e.Exception.GetBaseException());
            Application.ThreadException += onThreadException;

            var sweeper = new Dialogs(run);

            run.Form.Shown += (s, e) =>
            {
                run.Clock.Start();
                sweeper.Start();

                var queue = new Queue<Step>(steps);
                Step current = null;
                long startedAt = 0;
                var shot = 0;

                var pump = new Timer { Interval = 50 };
                var inTick = false;
                pump.Tick += (s2, e2) =>
                {
                    // A step that opens a modal - the confirm before an apply - pumps this very
                    // timer from inside its own Do. That re-entrant tick would read the window
                    // before the gesture has landed and call the step done on what it saw.
                    if (inTick) return;
                    inTick = true;
                    try
                    {
                        Tick();
                    }
                    finally
                    {
                        inTick = false;
                    }
                };

                void Tick()
                {
                    if (current == null)
                    {
                        if (queue.Count == 0)
                        {
                            pump.Stop();
                            sweeper.Stop();
                            run.Form.Close();
                            return;
                        }

                        current = queue.Dequeue();
                        startedAt = run.Clock.ElapsedMilliseconds;
                        try
                        {
                            // A step with nothing to do is an assertion: it does not touch the
                            // window, it only says what must be true of it before the run may go
                            // on. Its Until is the whole step.
                            if (current.Do != null) current.Do(run);
                        }
                        catch (Exception ex)
                        {
                            run.Failures.Add(run.Clock.ElapsedMilliseconds + "ms  \"" + current.Label
                                             + "\" threw: " + Head(ex));
                            Shot(run, ++shot, current.Label);
                            pump.Stop();
                            sweeper.Stop();
                            run.Form.Close();
                            return;
                        }
                    }

                    bool satisfied;
                    try
                    {
                        satisfied = current.Until == null || current.Until(run);
                    }
                    catch (Exception ex)
                    {
                        // A condition that throws is a finding, not a wait: report it as the
                        // step's failure rather than spinning until the timeout dresses it up
                        // as slowness.
                        run.Failures.Add(run.Clock.ElapsedMilliseconds + "ms  the wait after \""
                                         + current.Label + "\" threw: " + Head(ex));
                        Shot(run, ++shot, current.Label);
                        pump.Stop();
                        sweeper.Stop();
                        run.Form.Close();
                        return;
                    }

                    if (satisfied)
                    {
                        Shot(run, ++shot, current.Label);
                        current = null;
                        return;
                    }

                    if (run.Clock.ElapsedMilliseconds - startedAt > current.TimeoutMs)
                    {
                        run.Failures.Add(run.Clock.ElapsedMilliseconds + "ms  \"" + current.Label
                                         + "\" was still waiting after " + current.TimeoutMs
                                         + "ms; the status line says \"" + run.Probe.Status + "\"");
                        Shot(run, ++shot, current.Label);
                        pump.Stop();
                        sweeper.Stop();
                        run.Form.Close();
                    }
                }

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
            }

            return run.Failures;
        }

        private static void Shot(Run run, int number, string label)
        {
            try
            {
                Capture.Of(run.Form, Path.Combine(run.ShotDir, number.ToString("00") + "-" + Slug(label) + ".png"));
            }
            catch (Exception)
            {
                // A shot that cannot be taken is not worth failing a run over.
            }
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
            foreach (var c in label ?? "step")
            {
                sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
            }

            return sb.ToString().Trim('-');
        }
    }
}
