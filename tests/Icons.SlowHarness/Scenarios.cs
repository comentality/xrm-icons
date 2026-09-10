using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Icons.SlowHarness
{
    /// <summary>
    /// One scenario per thing a slow link does to this tool. Each is a sequence of ordinary
    /// gestures at ordinary intervals, against an environment that takes seconds rather than
    /// milliseconds, and a handful of questions asked of the window in between.
    ///
    /// None of them is about what lands in Dataverse - the applier tests cover that. They are
    /// about the window while the answer is still on its way: which buttons a person can still
    /// press, what the status line is claiming, and whether the answer that lands last belongs
    /// to the question asked last.
    /// </summary>
    public static class Scenarios
    {
        /// <summary>
        /// A link where one kind of question is slow and the rest are merely remote. Latency is
        /// put on one label at a time so a scenario's clock is readable: a fetch takes about
        /// what its number says.
        /// </summary>
        private static Func<Call, int> Slow(string label, params int[] perCall)
        {
            return call =>
            {
                if (call.Label != label) return 20;
                return perCall[Math.Min(call.Nth - 1, perCall.Length - 1)];
            };
        }

        public static List<Scenario> All()
        {
            return new List<Scenario>
            {
                CloseDuringLoad(),
                LoadTwice(),
                LoadFails(),
                AppsFailLast(),
                SolutionsFailOnReload(),
                SwitchMidLoad(),
                StaleAnswerDiscarded(),
                ApplyGuarded(),
                CancelApply(),
                ApplyFails(),
                SwitchMidApply(),
                UndoPutsItBack(),
                UndoGuarded(),
                UndoForgottenOnSwitch(),
                UnsetOnlyFilter(),
                TableSeedsTheSearch(),
                FilterOfOnePicksIt(),
                ConnectingLoadsItself(),
                QueuedLoadSurvivesTheConnection(),
                RasterHandover(),
                PrefixMismatchWarned(),
                CatchAllNotPreselected(),
                PreferredSolutionWins(),
            };
        }

        /// <summary>
        /// An environment that knows which solution this user works in, because they told the
        /// maker portal. GetPreferredSolution is the only source here that is a stated preference
        /// rather than an inference, so it beats the sort order - the solution it names goes to
        /// the top even though it sorts last.
        /// </summary>
        private static Scenario PreferredSolutionWins()
        {
            return new Scenario
            {
                Name = "preferred-solution-wins",
                Why = "a solution the user has actually chosen outranks anything a sort order can infer",
                Wire = s =>
                {
                    s.Solutions.Clear();
                    s.AddSolution("Adatum", "Adatum Customisations", "ada");
                    s.AddSolution("Default", "Default Solution", "new");
                    var zephyr = s.AddSolution("Zephyr", "Zephyr Customisations", "zep");

                    s.PreferredSolution = zephyr;
                    s.Latency = Slow("solutions", 300);
                }
            }
                .At(200, "press Load", r => r.Probe.PressLoad())
                .At(1400, "the stated preference is the top row", r =>
                {
                    r.Check(r.Probe.SelectedSolution == "Zephyr",
                        "the environment's own answer should be the one chosen, and the box holds \""
                        + (r.Probe.SelectedSolution ?? "nothing") + "\"");
                    r.Check(r.Probe.Solutions.SelectedIndex == 0,
                        "and it should be the top row, not a selection made further down; it is row "
                        + r.Probe.Solutions.SelectedIndex);
                    r.Check(r.Probe.SolutionNames().Length == 3,
                        "with everything else still offered under it, and the list reads: "
                        + string.Join(", ", r.Probe.SolutionNames()));
                    r.Check(r.Service.Log("preferred-solution").Count == 1,
                        "asked once per load, and it was asked "
                        + r.Service.Log("preferred-solution").Count + " times");
                });
        }

        /// <summary>
        /// A name typed under one publisher's prefix, being written through a solution published
        /// by another. Dataverse refuses to create a web resource that is not named for the
        /// publisher of the solution it goes into - but it will happily update one that already
        /// exists, wherever it lives, and only the org knows which of the two this is. So the
        /// window says what it knows and leaves the button alone: a warning here, not a refusal.
        /// </summary>
        private static Scenario PrefixMismatchWarned()
        {
            return OpenOnATable(new Scenario
            {
                Name = "prefix-mismatch-warned",
                Why = "a name under another publisher's prefix is worth saying, and not worth blocking",
                Wire = s => s.Latency = Slow("tables", 300)
            })
                .At(2000, "type a name under another prefix", r =>
                {
                    r.Probe.Name.Text = "zzz_/icons/account.svg";

                    r.Check((r.Probe.Warning ?? "").IndexOf("zzz_", StringComparison.Ordinal) >= 0,
                        "the window should say which prefix the name carries, and the warning line says \""
                        + (r.Probe.Warning ?? "nothing") + "\"");
                    r.Check((r.Probe.Warning ?? "").IndexOf("con_", StringComparison.Ordinal) >= 0,
                        "and which one the chosen solution publishes, and it says \""
                        + (r.Probe.Warning ?? "nothing") + "\"");
                    r.Check(r.Probe.Apply.Enabled,
                        "but Apply must stay live: the name may belong to a web resource that already exists");
                })
                .At(2400, "put it back under the solution's prefix", r =>
                {
                    r.Probe.Name.Text = "con_/icons/account.svg";

                    r.Check(r.Probe.Warning == null,
                        "with the prefixes agreeing there is nothing left to warn about, and the line says \""
                        + (r.Probe.Warning ?? "nothing") + "\"");
                    r.Check(r.Probe.Apply.Enabled, "and Apply is still live");
                });
        }

        /// <summary>
        /// An environment where nobody has set a preferred solution, which is what the platform
        /// reports by naming the CDS default solution - the same catch-all the Default solution
        /// is, under a unique name generated per environment.
        ///
        /// That answer means "nobody said", and nobody said is not permission to pick. So nothing
        /// is chosen, Apply stays dead, and the window says whose turn it is. Choosing the
        /// alphabetically first solution instead would be a guess, and choosing the catch-all it
        /// names would put icons where nothing this environment ships can see them.
        /// </summary>
        private static Scenario CatchAllNotPreselected()
        {
            var cdsDefault = new Guid("00000001-0000-0000-0001-00000000009b");

            return new Scenario
            {
                Name = "no-preference-nothing-chosen",
                Why = "with no preferred solution set, nothing is chosen for you and the catch-alls still sink",
                Wire = s =>
                {
                    s.Solutions.Clear();
                    s.AddSolution("Cr89693", "Common Data Services Default Solution", "cr23c", cdsDefault);
                    s.AddSolution("Default", "Default Solution", "new");
                    s.AddSolution("Contoso", "Contoso Customisations", "con");

                    // What the platform answers when nobody has chosen: the catch-all itself.
                    s.PreferredSolution = cdsDefault;
                    s.Latency = Slow("solutions", 300);
                }
            }
                .At(200, "press Load", r => r.Probe.PressLoad())
                .At(1400, "nothing is chosen, and the catch-alls are last", r =>
                {
                    r.Check(r.Probe.Solutions.Items.Count == 3,
                        "all three should still be offered, and there are " + r.Probe.Solutions.Items.Count);
                    r.Check(r.Probe.SelectedSolution == null,
                        "nothing should be chosen without a stated preference, and the box holds \""
                        + (r.Probe.SelectedSolution ?? "nothing") + "\"");
                    r.Check(r.Probe.SolutionNames().FirstOrDefault() != null
                            && r.Probe.SolutionNames()[0].Contains("Contoso"),
                        "the real solution should still be the top row to choose; the list reads: "
                        + string.Join(", ", r.Probe.SolutionNames()));
                    r.Check((r.Probe.Warning ?? "").IndexOf("no preferred solution",
                                StringComparison.OrdinalIgnoreCase) >= 0,
                        "and the window should say whose turn it is, but the warning line says \""
                        + (r.Probe.Warning ?? "nothing") + "\"");
                })
                .At(1700, "with an icon and a table, Apply is still dead", r =>
                {
                    r.Probe.PickFirstIcon();
                    r.Probe.PickTable("contoso_account");

                    r.Check(!r.Probe.Apply.Enabled,
                        "everything but the solution is chosen, and that alone must keep Apply dead");
                });
        }

        /// <summary>
        /// The three gestures every apply scenario opens with: tables in, an icon chosen, a
        /// table chosen. The load is fast because these scenarios are about the apply - but the
        /// table pick still waits over a second for it, because on a machine busy running the
        /// rest of the suite even a fast worker can be slow to get out of bed, and a flaky
        /// setup beat reads exactly like a finding.
        /// </summary>
        private static Scenario OpenOnATable(Scenario scenario)
        {
            return scenario
                .At(200, "press Load", r => r.Probe.PressLoad())
                .At(700, "pick an icon", r => r.Probe.PickFirstIcon())
                .At(1600, "pick a table", r => r.Probe.PickTable("contoso_account"));
        }

        /// <summary>
        /// Somebody gives up and closes the tab while the environment is still thinking. The
        /// answer arrives afterwards and is handed to a control that no longer exists. On a
        /// fast link the window for this is milliseconds wide; this is what makes it seconds.
        /// </summary>
        private static Scenario CloseDuringLoad()
        {
            return new Scenario
            {
                Name = "close-during-load",
                Why = "closing the tab mid-fetch must not throw",
                Wire = s => s.Latency = Slow("tables", 2500)
            }
                .At(200, "press Load", r => r.Probe.PressLoad())
                .At(1000, "close the tab", r =>
                {
                    r.Form.Controls.Remove(r.Control);
                    r.Control.Dispose();
                })
                .At(4200, "let the answer land on nobody", r =>
                {
                    r.Check(r.Service.Log("tables").Count == 1,
                        "the load should have gone out exactly once");
                    // The dead control chains a solutions load after the tables land. A control
                    // that was disposed must start nothing: not throwing is not enough.
                    r.Check(r.Service.Log("solutions").Count == 0,
                        "nothing may be asked on behalf of a control that no longer exists, and the org saw: "
                        + string.Join(", ", r.Service.Log().Select(c => c.Label)));
                });
        }

        /// <summary>
        /// Load tables pressed again because the first press did not appear to do anything. It
        /// is the most natural thing in the world on a slow link, and it would cost a second
        /// full metadata query - the most expensive call the tool makes.
        /// </summary>
        private static Scenario LoadTwice()
        {
            return new Scenario
            {
                Name = "load-twice",
                Why = "Load must be dead while it is loading",
                Wire = s => s.Latency = Slow("tables", 2500)
            }
                .At(200, "press Load", r => r.Probe.PressLoad())
                .At(800, "press it again", r =>
                {
                    r.Check(!r.Probe.Load.Enabled, "Load should be dead while it is loading");
                    r.Check(r.Probe.Status.IndexOf("Loading tables", StringComparison.OrdinalIgnoreCase) >= 0,
                        "the status line should own up to the fetch, and says \"" + r.Probe.Status + "\"");
                    r.Probe.PressLoad();
                })
                .At(1600, "and again", r => r.Probe.PressLoad())
                .At(5000, "count the round trips", r =>
                {
                    r.Check(r.Service.Log("tables").Count == 1,
                        "three presses, one query, and there were " + r.Service.Log("tables").Count);
                    r.Check(r.Probe.Load.Enabled, "and Load should be live again afterwards");
                    // Two of the three: the frozen one is locked against customisation and is
                    // hidden until somebody asks for it.
                    r.Check(r.Probe.Tables.Items.Count == 2,
                        "the tables that can take an icon should be in the list, and there are "
                        + r.Probe.Tables.Items.Count);
                });
        }

        /// <summary>
        /// The link gives up rather than answering. A timeout is what a slow network does when
        /// it is slow enough, and it ends in a dialog somebody reads once and dismisses. What
        /// matters afterwards is that the tool is not bricked and the window still says why the
        /// list is empty - an environment that could not be reached must not read as an
        /// environment with no tables.
        /// </summary>
        private static Scenario LoadFails()
        {
            return new Scenario
            {
                Name = "load-fails",
                Why = "a load that fails must give the tool back, not brick it",
                Wire = s =>
                {
                    s.Latency = Slow("tables", 1200, 200);
                    s.Fails = call => call.Label == "tables" && call.Nth == 1
                        ? new TimeoutException("The request channel timed out while waiting for a reply.")
                        : null;
                }
            }
                .At(200, "press Load", r => r.Probe.PressLoad())
                .At(2500, "the tool is still a tool", r =>
                {
                    r.Check(r.Probe.Load.Enabled, "Load must come back, or there is no way on from here");
                    r.Check((r.Probe.Trouble ?? "").IndexOf("Could not load tables", StringComparison.OrdinalIgnoreCase) >= 0,
                        "the trouble line should carry the failure once the dialog is gone, and says \""
                        + (r.Probe.Trouble ?? "nothing") + "\"");
                    r.Check(r.Dialogs.Any(d => d.IndexOf("Could not load tables", StringComparison.OrdinalIgnoreCase) >= 0),
                        "and a dialog should have said so first: " + string.Join(" | ", r.Dialogs));
                    r.Check(r.Probe.Tables.Items.Count == 0, "with nothing left pretending to be a table list");
                })
                .At(2700, "press Load again", r => r.Probe.PressLoad())
                .At(4200, "and this time it lands", r =>
                {
                    r.Check(r.Probe.Tables.Items.Count == 2, "the second load should fill the list");
                    r.Check(r.Probe.Trouble == null, "with the failure gone from the window");
                });
        }

        /// <summary>
        /// The app list is the last link of the load chain, read for the preview that is on
        /// screen from the moment the tool opens. It is the least of the three: an environment
        /// whose apps will not come back is still one whose tables and solutions did, and the
        /// window has to stay every bit as usable - with the preview saying what it fell back
        /// to rather than showing an invented navigation as though it were the org's.
        /// </summary>
        private static Scenario AppsFailLast()
        {
            return new Scenario
            {
                Name = "apps-fail-last",
                Why = "an app list that will not come back must cost the preview only",
                Wire = s =>
                {
                    s.Latency = Slow("apps", 800);
                    s.Fails = call => call.Label == "apps"
                        ? new TimeoutException("The request channel timed out while waiting for a reply.")
                        : null;
                }
            }
                .At(200, "press Load", r => r.Probe.PressLoad())
                .At(2500, "the tables and solutions are in, the apps are not", r =>
                {
                    r.Check(r.Probe.Tables.Items.Count > 0,
                        "the tables landed before the apps were asked for, and the list holds "
                        + r.Probe.Tables.Items.Count);
                    r.Check(r.Probe.Solutions.Items.Count > 0,
                        "and so did the solutions, and the box holds " + r.Probe.Solutions.Items.Count);
                    r.Check(r.Probe.Load.Enabled, "Load must be live again");
                    r.Check(r.Probe.Trouble == null,
                        "and nothing is wrong with this window, though it says \"" + r.Probe.Trouble + "\"");
                    r.Check(r.Probe.ContextNote.IndexOf("stand-in", StringComparison.OrdinalIgnoreCase) >= 0,
                        "the preview must own up to what it is drawing, and says \"" + r.Probe.ContextNote + "\"");

                    // Last of the three, so the two that matter are on screen while this one is
                    // still out rather than behind it.
                    var order = r.Service.Log().Select(c => c.Label).ToList();
                    r.Check(order.IndexOf("apps") > order.IndexOf("solutions"),
                        "the apps must be asked for after the solutions, and the org saw: "
                        + string.Join(", ", order));
                });
        }

        /// <summary>
        /// The solutions query fails on a reload, after a first load that worked. A new web
        /// resource has to be filed somewhere, and the only somewhere left is the default
        /// solution - so this stops an apply rather than falling back to it. The box must not
        /// quietly keep offering the solution it loaded last time either: that one would be
        /// written into on the strength of an answer this load never gave.
        /// </summary>
        private static Scenario SolutionsFailOnReload()
        {
            return new Scenario
            {
                Name = "solutions-fail-on-reload",
                Why = "with no solution loaded there is nowhere to write, and Apply must say so by being dead",
                Wire = s =>
                {
                    s.Latency = Slow("solutions", 200);
                    s.Fails = call => call.Label == "solutions" && call.Nth == 2
                        ? new TimeoutException("The request channel timed out while waiting for a reply.")
                        : null;
                }
            }
                .At(200, "press Load", r => r.Probe.PressLoad())
                .At(1200, "the first load lands whole", r =>
                {
                    r.Check(r.Probe.Solutions.Items.Count == 2,
                        "both solutions should be in the box, and there are " + r.Probe.Solutions.Items.Count);
                    r.Check(r.Probe.Solutions.SelectedItem != null
                            && r.Probe.Solutions.SelectedItem.ToString().Contains("Contoso"),
                        "with the real solution preferred over Default");
                })
                .At(1400, "choose an icon and a table", r =>
                {
                    r.Probe.PickFirstIcon();
                    r.Probe.PickTable("contoso_account");
                    r.Check(r.Probe.Apply.Enabled,
                        "everything an apply needs is chosen, so Apply should be live");
                })
                .At(1700, "press Load again", r => r.Probe.PressLoad())
                .At(3100, "the reload loses the solutions", r =>
                {
                    r.Check((r.Probe.Trouble ?? "").IndexOf("needs one", StringComparison.OrdinalIgnoreCase) >= 0,
                        "the trouble line should say an icon needs a solution to go in, and says \""
                        + (r.Probe.Trouble ?? "nothing") + "\"");
                    r.Check(r.Probe.Solutions.SelectedItem == null,
                        "nothing may stay selected in the solution box, or the write goes somewhere this load never confirmed - it holds \""
                        + (r.Probe.Solutions.SelectedItem == null ? "nothing" : r.Probe.Solutions.SelectedItem.ToString()) + "\"");
                    r.Check(!r.Probe.Apply.Enabled,
                        "and Apply must be dead with nowhere to put the web resource");
                });
        }

        /// <summary>
        /// The connection changes while a load is still out, and the new org's load starts
        /// before the old org's answer lands. The old answer must not be believed - and must
        /// not release the flag the new fetch is holding, or the Load button comes back to
        /// life with a query still on the wire and the status line stops owning up to it.
        /// </summary>
        private static Scenario SwitchMidLoad()
        {
            return new Scenario
            {
                Name = "switch-mid-load",
                Why = "the old org's answer must not unlock the new org's fetch",
                Wire = s => s.Latency = Slow("tables", 2600)
            }
                .At(200, "press Load", r => r.Probe.PressLoad())
                .At(800, "switch to another org", r =>
                {
                    var fabrikam = SlowService.Sampled("Fabrikam");
                    fabrikam.Latency = Slow("tables", 2600);
                    r.SwitchTo(fabrikam);
                })
                .At(1000, "press Load there", r => r.Probe.PressLoad())
                .At(3200, "the old answer lands while the new is still out", r =>
                {
                    r.Check(r.Probe.LoadingTables,
                        "the tool should still know a fetch is out, and thinks nothing is");
                    r.Check(!r.Probe.Load.Enabled,
                        "Load should still be dead with a query on the wire");
                    r.Check(r.Probe.Status.IndexOf("Loading tables", StringComparison.OrdinalIgnoreCase) >= 0,
                        "the status line should still own up to the fetch, and says \"" + r.Probe.Status + "\"");
                })
                .At(4400, "the new org's answer is the one believed", r =>
                {
                    r.Check(r.Probe.TableNames().Any(t => t.Contains("fabrikam_account")),
                        "the new org's tables should be in the list");
                    r.Check(!r.Probe.TableNames().Any(t => t.Contains("contoso_account")),
                        "and the old org's must not be");
                    r.Check(r.Probe.Load.Enabled, "with Load live again once everything has landed");
                });
        }

        /// <summary>
        /// The other ordering of switch-mid-load: the new org answers quickly and the old org's
        /// answer lands last. Whichever lands last is what a tool without a stale-result guard
        /// believes - and here that is a list of tables from an org the window is no longer
        /// connected to, offered for writing to the org it is.
        /// </summary>
        private static Scenario StaleAnswerDiscarded()
        {
            return new Scenario
            {
                Name = "stale-answer-discarded",
                Why = "the answer that lands last must not be the question asked first",
                Wire = s => s.Latency = Slow("tables", 2600)
            }
                .At(200, "press Load", r => r.Probe.PressLoad())
                .At(800, "switch to another org", r => r.SwitchTo(SlowService.Sampled("Fabrikam")))
                .At(1000, "press Load there", r => r.Probe.PressLoad())
                .At(4400, "the old org's answer landed last, and on nobody", r =>
                {
                    r.Check(r.Probe.TableNames().Any(t => t.Contains("fabrikam_account")),
                        "the new org's tables should be in the list");
                    r.Check(!r.Probe.TableNames().Any(t => t.Contains("contoso_account")),
                        "and the old org's must not be, however late their answer landed");
                    r.Check(r.Probe.Load.Enabled, "with Load live again");
                });
        }

        /// <summary>
        /// Apply pressed again while the apply it started is still running. The second press
        /// must be impossible: two writers over the same web resource and the same table is how
        /// half of one apply interleaves with half of another.
        /// </summary>
        private static Scenario ApplyGuarded()
        {
            return OpenOnATable(new Scenario
            {
                Name = "apply-guarded",
                Why = "Apply must be dead while it is applying, and Cancel alive",
                Wire = s => s.Latency = Slow("webresource-find", 2500)
            })
                .At(1900, "press Apply and confirm", r => r.Probe.PressApply())
                .At(3000, "while the write is out", r =>
                {
                    r.Check(!r.Probe.Apply.Enabled, "Apply should be dead while it is applying");
                    r.Check(r.Probe.Cancel.Enabled, "and Cancel should be live, or the wait cannot be escaped");
                    r.Check(!r.Probe.Load.Enabled, "and Load should be dead: a reload mid-write believes half a truth");
                    r.Probe.PressApply();
                })
                .At(8200, "count what happened", r =>
                {
                    // Two lookups for one write: the name the drawing goes under, and the folder
                    // the attribution page is rebuilt from. A second press must add neither.
                    r.Check(r.Service.Log("webresource-find").Count == 2,
                        "two presses, one write, and the name was looked up "
                        + r.Service.Log("webresource-find").Count + " times");
                    r.Check(r.Service.Log("publish").Count == 1,
                        "one publish, and there were " + r.Service.Log("publish").Count);
                    r.Check(r.Dialogs.Count(d => d == "Set table icon") == 1,
                        "one confirm, and there were: " + string.Join(" | ", r.Dialogs));
                    r.Check(r.Dialogs.Count == 1,
                        "and the confirm should be the only modal in the whole run, and there were: "
                        + string.Join(" | ", r.Dialogs));
                    r.Check(r.Probe.Apply.Enabled, "and Apply should be live again afterwards");
                    r.Check(r.Probe.Status.IndexOf("Set ", StringComparison.Ordinal) >= 0,
                        "with the outcome on the status line, which says \"" + r.Probe.Status + "\"");
                    r.Check(r.Probe.StatusDetail.IndexOf("Published", StringComparison.Ordinal) >= 0,
                        "and every step of it still readable rather than dismissed, and the tooltip says \""
                        + r.Probe.StatusDetail.Replace("\n", " | ") + "\"");
                });
        }

        /// <summary>
        /// Cancel, mid-apply. The call already on the wire cannot be recalled, but on a slow
        /// link the calls behind it are most of the wait, and those must never be made. What is
        /// left on the org - a web resource written, a table not yet pointed at it - is exactly
        /// what the trouble line has to own up to.
        /// </summary>
        private static Scenario CancelApply()
        {
            return OpenOnATable(new Scenario
            {
                Name = "cancel-apply",
                Why = "cancelling must stop the round trips not yet made, and say what it left behind",
                Wire = s => s.Latency = call =>
                    call.Label == "webresource-find" ? 400 :
                    call.Label == "webresource-create" ? 2500 : 20
            })
                .At(1900, "press Apply and confirm", r => r.Probe.PressApply())
                .At(3000, "give up", r =>
                {
                    r.Check(r.Probe.Cancel.Enabled, "Cancel should be live while the write is out");
                    r.Probe.PressCancel();
                })
                .At(6000, "nothing more went out", r =>
                {
                    r.Check(r.Service.Log("table-read").Count == 0 && r.Service.Log("publish").Count == 0,
                        "cancelling should stop the round trips that had not happened yet, and the org saw: "
                        + string.Join(", ", r.Service.Log().Select(c => c.Label)));
                    r.Check(!r.Probe.Applying, "the tool should know it is idle again");
                    r.Check((r.Probe.Trouble ?? "").IndexOf("Cancelled", StringComparison.OrdinalIgnoreCase) >= 0,
                        "and the trouble line should own up to the half-done change, and says \""
                        + (r.Probe.Trouble ?? "nothing") + "\"");
                    r.Check(r.Probe.Apply.Enabled && r.Probe.Load.Enabled,
                        "with the tool given back");
                });
        }

        /// <summary>
        /// The write fails half way - the table update times out after the web resource is
        /// already stored. The same shape as load-fails and the same stakes: Apply is held down
        /// for the duration of its own write, so a failure that did not release it would be an
        /// Apply button dead for the rest of the session.
        /// </summary>
        private static Scenario ApplyFails()
        {
            return OpenOnATable(new Scenario
            {
                Name = "apply-fails",
                Why = "a write that fails must give the tool back, and keep saying so",
                Wire = s =>
                {
                    s.Latency = Slow("table-update", 800);
                    s.Fails = call => call.Label == "table-update"
                        ? new TimeoutException("The request channel timed out while waiting for a reply.")
                        : null;
                }
            })
                .At(1900, "press Apply and confirm", r => r.Probe.PressApply())
                .At(4300, "read what is left", r =>
                {
                    r.Check(!r.Probe.Applying, "a failed write must release the tool, and it still thinks it is applying");
                    r.Check(r.Probe.Apply.Enabled, "with Apply live again");
                    r.Check((r.Probe.Trouble ?? "").IndexOf("Could not set the icon", StringComparison.OrdinalIgnoreCase) >= 0,
                        "the trouble line should carry the failure once the dialog is gone, and says \""
                        + (r.Probe.Trouble ?? "nothing") + "\"");
                    r.Check(r.Dialogs.Any(d => d.IndexOf("Could not set the icon", StringComparison.OrdinalIgnoreCase) >= 0),
                        "and a dialog should have said so first: " + string.Join(" | ", r.Dialogs));
                    r.Check(r.Service.Log("publish").Count == 0,
                        "nothing may be published after a failed update");
                });
        }

        /// <summary>
        /// The connection changes while an apply is still writing to the old org. The calls not
        /// yet made must not be made - they would keep changing an environment the window no
        /// longer shows - and the window has to say that the old org may have been half-changed,
        /// because the discarded callback never will.
        /// </summary>
        private static Scenario SwitchMidApply()
        {
            return OpenOnATable(new Scenario
            {
                Name = "switch-mid-apply",
                Why = "switching orgs mid-apply must stop the write and own up to it",
                Wire = s => s.Latency = Slow("webresource-create", 2500)
            })
                .At(1900, "press Apply and confirm", r => r.Probe.PressApply())
                .At(3000, "switch to another org", r =>
                {
                    var old = r.Service;
                    r.SwitchTo(SlowService.Sampled("Fabrikam"));
                    r.Check((r.Probe.Trouble ?? "").IndexOf("connection changed", StringComparison.OrdinalIgnoreCase) >= 0,
                        "the window should say the old org may be half-changed, and says \""
                        + (r.Probe.Trouble ?? "nothing") + "\"");
                    r.Service = old;
                })
                .At(6000, "the old org is left alone", r =>
                {
                    r.Check(r.Service.Log("table-update").Count == 0 && r.Service.Log("publish").Count == 0,
                        "the calls not yet made must not be made, and the old org saw: "
                        + string.Join(", ", r.Service.Log().Select(c => c.Label)));
                    // The report is a status line rather than a dialog now, so this is asked of
                    // the line: an abandoned write must leave nothing behind claiming it landed.
                    r.Check(r.Probe.Status.IndexOf("Set ", StringComparison.Ordinal) < 0,
                        "nothing may report a write that was abandoned, and the status line says \""
                        + r.Probe.Status + "\"");
                    r.Check(r.Probe.StatusDetail.Length == 0,
                        "nor may its steps be left readable, and the tooltip says \""
                        + r.Probe.StatusDetail.Replace("\n", " | ") + "\"");
                });
        }


        /// <summary>
        /// Ctrl+Z, after a write has landed. Undo is not a keystroke against a document held in
        /// memory here - it is a second write and a second publish - so what this asks is that
        /// it goes out at all, that it goes out once, and that the window lands back on the table
        /// it just put back rather than on whatever the apply moved it to.
        /// </summary>
        private static Scenario UndoPutsItBack()
        {
            return OpenOnATable(new Scenario
            {
                Name = "undo-puts-it-back",
                Why = "an applied icon must be takeable back, and the window must land on the table it took it off",
                Wire = s => s.Latency = Slow("publish", 300)
            })
                .At(1900, "press Apply and confirm", r => r.Probe.PressApply())
                .At(3200, "the write landed and there is something to take back", r =>
                {
                    r.Check(!r.Probe.Applying, "the apply should be finished by now");
                    r.Check(r.Probe.Undo.Enabled, "and Undo should be offering to take it back");
                    r.Check(!r.Probe.Redo.Enabled, "with nothing to do again yet");
                    r.Check(r.Probe.Undo.ToolTipText.IndexOf("Account", StringComparison.OrdinalIgnoreCase) >= 0,
                        "and the tooltip should name the table it would put back, and says \""
                        + r.Probe.Undo.ToolTipText + "\"");
                })
                .At(3400, "press Ctrl+Z", r =>
                {
                    r.Check(r.Probe.SendKey(Keys.Control | Keys.Z),
                        "the window should take Ctrl+Z for itself rather than pass it on");
                })
                .At(5200, "the table is back the way it was", r =>
                {
                    r.Check(!r.Probe.Applying, "the undo should be finished");
                    r.Check(r.Service.Log("table-update").Count == 2,
                        "the table should have been written twice - set, then put back - and it was written "
                        + r.Service.Log("table-update").Count + " times");
                    r.Check(r.Service.Log("publish").Count == 2,
                        "with a publish behind each, and there were " + r.Service.Log("publish").Count);
                    r.Check(r.Dialogs.Count(d => d == "Undo the last change") == 1,
                        "one confirm before it went out, and the dialogs were: " + string.Join(" | ", r.Dialogs));
                    r.Check((r.Probe.Tables.SelectedItem ?? "").ToString().Contains("contoso_account"),
                        "and the window should land on the table it just put back, not on the next one waiting; it is on \""
                        + (r.Probe.Tables.SelectedItem ?? "nothing") + "\"");
                    r.Check(!r.Probe.Undo.Enabled, "with nothing left to take back");
                    r.Check(r.Probe.Redo.Enabled, "and the change offered back the other way");
                })
                .At(5400, "press Ctrl+Y", r => r.Probe.SendKey(Keys.Control | Keys.Y))
                .At(7200, "it is on again", r =>
                {
                    r.Check(r.Service.Log("table-update").Count == 3,
                        "doing it again is a third write, and there were "
                        + r.Service.Log("table-update").Count);
                    r.Check(r.Probe.Undo.Enabled && !r.Probe.Redo.Enabled,
                        "and the two buttons should have changed places");
                    r.Check(r.Probe.Trouble == null,
                        "with nothing wrong, and the trouble line says \"" + (r.Probe.Trouble ?? "") + "\"");
                });
        }

        /// <summary>
        /// The same rule the apply lives under, because it is the same kind of thing: one write
        /// out at a time. Two reverts over one table is how half of one interleaves with half of
        /// another - and Ctrl+Z is cheaper to press twice than a button is.
        /// </summary>
        private static Scenario UndoGuarded()
        {
            return OpenOnATable(new Scenario
            {
                Name = "undo-guarded",
                Why = "undo must be dead until there is something to take back, and while it is taking it",
                Wire = s => s.Latency = Slow("table-read", 20, 2500)
            })
                .At(1700, "before anything has been written", r =>
                {
                    r.Check(!r.Probe.Undo.Enabled, "there is nothing to take back yet");
                    r.Check(!r.Probe.Redo.Enabled, "and nothing to do again");
                    r.Check(!r.Probe.SendKey(Keys.Control | Keys.Z),
                        "and Ctrl+Z should be left to whatever else wants it");
                })
                .At(1900, "press Apply and confirm", r => r.Probe.PressApply())
                .At(3400, "press Undo", r => r.Probe.PressUndo())
                .At(4200, "while the revert is out", r =>
                {
                    r.Check(r.Probe.Applying, "the revert should still be on the wire");
                    r.Check(!r.Probe.Undo.Enabled, "Undo should be dead while it is undoing");
                    r.Check(!r.Probe.Redo.Enabled, "and Redo with it");
                    r.Check(!r.Probe.Apply.Enabled, "and Apply, which would be a second writer over the same table");
                    r.Check(!r.Probe.Load.Enabled, "and Load, which mid-write believes half a truth");
                    r.Check(r.Probe.Cancel.Enabled, "with Cancel live, or the wait cannot be escaped");
                    r.Probe.PressUndo();
                    r.Probe.SendKey(Keys.Control | Keys.Z);
                })
                .At(7500, "count what happened", r =>
                {
                    r.Check(r.Service.Log("table-update").Count == 2,
                        "three presses, one revert, and the table was written "
                        + r.Service.Log("table-update").Count + " times");
                    r.Check(r.Dialogs.Count(d => d == "Undo the last change") == 1,
                        "one confirm asked, and the dialogs were: " + string.Join(" | ", r.Dialogs));
                    r.Check(r.Probe.Apply.Enabled && r.Probe.Load.Enabled,
                        "with the tool given back afterwards");
                });
        }

        /// <summary>
        /// An undo names a web resource and a table in the org it was written against. Another
        /// org has its own contoso_account, and putting this one's icon back on that one would be
        /// a write nobody asked for into an environment nobody was looking at.
        /// </summary>
        private static Scenario UndoForgottenOnSwitch()
        {
            return OpenOnATable(new Scenario
            {
                Name = "undo-forgotten-on-switch",
                Why = "what can be taken back belongs to one org, and must not survive the connection changing",
                Wire = s => s.Latency = Slow("publish", 200)
            })
                .At(1900, "press Apply and confirm", r => r.Probe.PressApply())
                .At(3200, "there is something to take back", r =>
                    r.Check(r.Probe.Undo.Enabled, "the write landed, so Undo should be offering it"))
                .At(3600, "switch to another org", r =>
                {
                    var old = r.Service;
                    r.SwitchTo(SlowService.Sampled("Fabrikam"));
                    r.Service = old;
                })
                .At(5000, "the offer is gone with it", r =>
                {
                    r.Check(!r.Probe.Undo.Enabled,
                        "the change belongs to the org that is no longer connected, and Undo is still offering it");
                    r.Check(!r.Probe.Redo.Enabled, "and Redo with it");
                    r.Check(r.Service.Log("table-update").Count == 1,
                        "with nothing written to the old org after the switch, and it was written "
                        + r.Service.Log("table-update").Count + " times");
                });
        }

        /// <summary>
        /// "Only tables with no icon yet" is a working list, and the point of a working list is
        /// that it gets shorter. A table that already carries an icon is never in it, and one
        /// that has just been given an icon leaves it the moment the write lands - not at the
        /// next reload, which on a slow link is the difference between a list you can work down
        /// and one you have to keep re-reading.
        /// </summary>
        private static Scenario UnsetOnlyFilter()
        {
            return new Scenario
            {
                Name = "unset-only-filter",
                Why = "the unset-only list must leave out what has an icon, and drop what just got one",
                Wire = s =>
                {
                    s.Tables.Add(SlowService.Table("contoso_settled", "Contoso Settled",
                        icon: "new_/icons/settled.svg"));
                    s.Latency = Slow("webresource-find", 600);
                }
            }
                .At(200, "press Load", r => r.Probe.PressLoad())
                .At(1800, "narrow to the tables with no icon", r =>
                {
                    r.Check(r.Probe.UnsetOnly.Checked,
                        "the window should have opened on the tables with no icon yet");
                    r.Check(!r.Probe.TableNames().Any(n => n.Contains("(contoso_settled)")),
                        "so a table that already has one should not be listed, and the list holds: "
                        + string.Join(", ", r.Probe.TableNames()));

                    // Everything, and then narrowed again, because the box unticks as well as
                    // ticks and the list has to come back whole.
                    r.Probe.UnsetOnly.Checked = false;

                    r.Check(r.Probe.TableNames().Any(n => n.Contains("(contoso_settled)")),
                        "unticking should give back every table, and holds: "
                        + string.Join(", ", r.Probe.TableNames()));

                    r.Probe.UnsetOnly.Checked = true;

                    // The locked table has no icon and never will, so it is in the unset-only
                    // list honestly - and hidden, because nothing can be done with it there.
                    // Unticking brings it back, and the rest of this scenario is about what the
                    // advance does when it meets one.
                    r.Check(!r.Probe.TableNames().Any(n => n.Contains("(contoso_frozen)")),
                        "a table nobody can customise should be hidden by default, and the list holds: "
                        + string.Join(", ", r.Probe.TableNames()));

                    r.Probe.HideLocked.Checked = false;

                    r.Check(r.Probe.TableNames().Any(n => n.Contains("(contoso_frozen)")),
                        "unticking should give it back, and the list holds: "
                        + string.Join(", ", r.Probe.TableNames()));

                    r.Check(!r.Probe.TableNames().Any(n => n.Contains("(contoso_settled)")),
                        "a table that already has an icon has no business in the unset list, which holds: "
                        + string.Join(", ", r.Probe.TableNames()));
                    r.Check(r.Probe.TableNames().Any(n => n.Contains("(contoso_account)")),
                        "and one still without an icon has to stay in it, and the list holds: "
                        + string.Join(", ", r.Probe.TableNames()));
                })
                .At(2300, "pick an icon and a table", r =>
                {
                    r.Probe.PickFirstIcon();
                    r.Probe.PickTable("contoso_account");
                })
                .At(2800, "press Apply and confirm", r => r.Probe.PressApply())
                .At(3100, "while the write is out", r =>
                    r.Check(r.Probe.TableNames().Any(n => n.Contains("(contoso_account)")),
                        "nothing has landed yet, so the table must still be listed, and the list holds: "
                        + string.Join(", ", r.Probe.TableNames())))
                .At(5200, "the table it was given to leaves the list", r =>
                {
                    r.Check(r.Probe.Status.IndexOf("Set ", StringComparison.Ordinal) >= 0,
                        "the write should have landed, and the status line says \"" + r.Probe.Status + "\"");
                    r.Check(!r.Probe.TableNames().Any(n => n.Contains("(contoso_account)")),
                        "the table just given an icon should have left the unset list, which holds: "
                        + string.Join(", ", r.Probe.TableNames()));
                    r.Check(r.Probe.TableNames().Any(n => n.Contains("(contoso_case)")),
                        "the tables still waiting for one must stay, and the list holds: "
                        + string.Join(", ", r.Probe.TableNames()));
                    // The list got shorter, so the row that took the applied table's place is
                    // the next one waiting for an icon. Landing on it is what makes a working
                    // list something you can work down rather than re-pick your way through.
                    var now = r.Probe.Tables.SelectedItem;
                    r.Check(now != null && now.ToString().Contains("(contoso_case)"),
                        "the next table without an icon should be waiting selected, and the box holds \""
                        + (now == null ? "nothing" : now.ToString()) + "\"");
                    r.Check(r.Probe.Name.Text.IndexOf("contoso_case", StringComparison.Ordinal) >= 0,
                        "with a name suggested for that one rather than the last one, and the box holds \""
                        + r.Probe.Name.Text + "\"");
                })
                .At(5600, "give the next one an icon too", r => r.Probe.PressApply())
                .At(8000, "and stop at the one that cannot take an icon", r =>
                {
                    // What is left in the list is locked against customisation. It is in the
                    // unset-only list honestly - it has no icon and never will - but landing on
                    // it would cost the click the advance is there to save.
                    r.Check(r.Probe.TableNames().Any(n => n.Contains("(contoso_frozen)")),
                        "the locked table should still be listed, and the list holds: "
                        + string.Join(", ", r.Probe.TableNames()));
                    r.Check(r.Probe.Tables.SelectedItem == null,
                        "but nothing may land on it, and the box holds \""
                        + r.Probe.Tables.SelectedItem + "\"");
                    r.Check(!r.Probe.Apply.Enabled,
                        "so with nothing left to apply to, Apply has to be dead again");
                });
        }

        /// <summary>
        /// A connection arriving. Nobody opens this tool to look at an empty window, and reading
        /// the tables is half a second, so the arrival is the cue rather than a click - and it is
        /// one load, not one per way in.
        /// </summary>
        private static Scenario ConnectingLoadsItself()
        {
            return new Scenario
            {
                Name = "connecting-loads-itself",
                Why = "a connection should put the tables on screen without being asked twice",
                Wire = s => s.Latency = Slow("tables", 600)
            }
                .At(200, "the connection arrives", r =>
                {
                    r.SwitchTo(r.Service);

                    r.Check(r.Probe.LoadingTables,
                        "the arrival should have started the load itself, and the tool thinks nothing is out");
                    r.Check(!r.Probe.Load.Enabled,
                        "with Load dead while it is out, like any other load");
                })
                .At(1800, "and the window is useful without a click", r =>
                {
                    r.Check(r.Probe.TableNames().Any(n => n.Contains("(contoso_account)")),
                        "the tables should be there for the taking, and the list holds: "
                        + string.Join(", ", r.Probe.TableNames()));
                    r.Check(r.Probe.Solutions.Items.Count > 0,
                        "and the solutions with them, and there are " + r.Probe.Solutions.Items.Count);
                    r.Check(r.Service.Log("tables").Count == 1,
                        "asked once, and the org was asked " + r.Service.Log("tables").Count + " times");
                    r.Check(r.Probe.Load.Enabled, "with Load live again for a reload");
                });
        }

        /// <summary>
        /// Load pressed with no connection yet. XrmToolBox asks for one, and hands it back with
        /// the name of the method that wanted it - which the base class runs as its last act. The
        /// window's own reset for a new org has to happen before all that, or it throws away the
        /// load the connection was made for and leaves an empty window and a live button.
        /// </summary>
        private static Scenario QueuedLoadSurvivesTheConnection()
        {
            return new Scenario
            {
                Name = "queued-load-survives-connecting",
                Why = "the load a connection was made for must not be discarded by arriving",
                Wire = s => s.Latency = Slow("tables", 600)
            }
                .At(200, "the connection arrives carrying the load that asked for it", r =>
                {
                    r.SwitchTo(r.Service, "LoadTables");

                    r.Check(r.Probe.LoadingTables,
                        "the load the connection was made for should still be out, and the tool thinks nothing is");
                })
                .At(1800, "and it is the answer the window shows", r =>
                {
                    r.Check(r.Probe.TableNames().Any(n => n.Contains("(contoso_account)")),
                        "its answer should be on screen, and the list holds: "
                        + string.Join(", ", r.Probe.TableNames()));
                    r.Check(r.Service.Log("tables").Count == 1,
                        "once, not once for the button and once for the arrival: the org was asked "
                        + r.Service.Log("tables").Count + " times");
                });
        }

        /// <summary>
        /// A table filter typed down to one table. There is nothing left to choose between, and
        /// making somebody open the list to click the only row in it is a click for nothing.
        /// </summary>
        private static Scenario FilterOfOnePicksIt()
        {
            return new Scenario
            {
                Name = "filter-of-one-picks-it",
                Why = "a filter that leaves one table has already chosen it",
                Wire = s => s.Latency = Slow("tables", 300)
            }
                .At(200, "press Load", r => r.Probe.PressLoad())
                .At(1400, "type a filter that leaves several", r =>
                {
                    r.Probe.TableFilter.Text = "contoso";

                    r.Check(r.Probe.Tables.Items.Count > 1,
                        "several tables should still be listed, and there are " + r.Probe.Tables.Items.Count);
                    r.Check(r.Probe.Tables.SelectedItem == null,
                        "and with a choice still to make, nothing may be chosen for you: the box holds \""
                        + r.Probe.Tables.SelectedItem + "\"");
                })
                .At(1600, "type it down to one", r =>
                {
                    r.Probe.TableFilter.Text = "case";

                    r.Check(r.Probe.Tables.Items.Count == 1,
                        "one table should be left, and there are " + r.Probe.Tables.Items.Count);
                    var chosen = r.Probe.Tables.SelectedItem;
                    r.Check(chosen != null && chosen.ToString().Contains("(contoso_case)"),
                        "and it should be the chosen one, rather than a list of one to open: the box holds \""
                        + (chosen == null ? "nothing" : chosen.ToString()) + "\"");
                });
        }

        /// <summary>
        /// Picking a table asks the library the question the user was about to type. The search
        /// this tool wrote is its own to replace when the table changes; a search somebody typed
        /// is not, and neither is an untouched box with an icon already picked out of it.
        /// </summary>
        private static Scenario TableSeedsTheSearch()
        {
            return new Scenario
            {
                Name = "table-seeds-the-search",
                Why = "picking a table should narrow the library to what that table is about",
                Wire = s =>
                {
                    // A name no icon in the library says anything about. Made up rather than
                    // borrowed from the sample org, so the scenario does not quietly stop
                    // testing anything the day Fluent draws a frozen or a settled.
                    s.Tables.Add(SlowService.Table("contoso_zzzz", "Contoso Zzzz"));
                    s.Latency = Slow("tables", 300);
                }
            }
                .At(200, "press Load", r => r.Probe.PressLoad())
                .At(1400, "pick a table, having typed nothing", r =>
                {
                    r.Probe.PickTable("contoso_account");

                    r.Check(r.Probe.Search.Text == "account",
                        "the table's own word should be in the search box, and it holds \""
                        + r.Probe.Search.Text + "\"");
                    r.Check(r.Probe.Grid.VirtualListSize > 0,
                        "with something in the grid, and there are " + r.Probe.Grid.VirtualListSize);
                    r.Check(r.Probe.Status.IndexOf(" of ", StringComparison.Ordinal) >= 0,
                        "and the status line should own up to a narrowed library, saying \""
                        + r.Probe.Status + "\"");
                })
                .At(1500, "press Enter in the search box", r =>
                {
                    r.Probe.PressEnterInSearch();

                    r.Check(r.Probe.Grid.SelectedIndices.Count == 1,
                        "Enter should land on the first result rather than beeping, and "
                        + r.Probe.Grid.SelectedIndices.Count + " cells are selected");
                })
                .At(1600, "pick another one", r =>
                {
                    r.Probe.PickTable("contoso_case");

                    r.Check(r.Probe.Search.Text == "case",
                        "the seed follows the table it was made for, and the box holds \""
                        + r.Probe.Search.Text + "\"");
                })
                .At(1800, "type a search of your own, then change table again", r =>
                {
                    r.Probe.Search.Text = "star";
                    r.Probe.PickTable("contoso_account");

                    r.Check(r.Probe.Search.Text == "star",
                        "a search somebody typed is theirs to keep, and the box holds \""
                        + r.Probe.Search.Text + "\"");
                })
                .At(2000, "browse the whole library from a table the library knows nothing about", r =>
                {
                    // A table that seeds nothing leaves an empty seed behind. An empty box is
                    // then indistinguishable from one nobody has touched - so it is only ever
                    // this tool's to write while no icon has been chosen out of it.
                    r.Probe.Search.Text = "";
                    r.Probe.PickTable("contoso_zzzz");

                    r.Check(r.Probe.Search.Text.Length == 0,
                        "a table the library cannot answer should seed nothing, and the box holds \""
                        + r.Probe.Search.Text + "\"");

                    r.Probe.PickFirstIcon();
                    r.Probe.PickTable("contoso_account");

                    r.Check(r.Probe.Search.Text.Length == 0,
                        "and an icon picked out of the whole library must not be re-ranked away by "
                        + "the next table: the box holds \"" + r.Probe.Search.Text + "\"");
                    r.Check(r.Probe.Grid.SelectedIndices.Count == 1,
                        "with that icon still chosen, and " + r.Probe.Grid.SelectedIndices.Count
                        + " cells are selected");
                });
        }

        /// <summary>
        /// A custom table that arrived with both a vector icon and classic ones, set by
        /// somebody else long before this tool existed. Asking for the PNGs takes those three
        /// slots over - which is the one thing the window has to say out loud before it happens,
        /// because what they point at now is left behind rather than replaced.
        ///
        /// The wording is pinned by the unit tests. What is pinned here is the wiring: that the
        /// load reads the classic slots at all, that the confirm builds with them without
        /// falling over, and that a run with four web resources instead of one still lands.
        /// </summary>
        private static Scenario RasterHandover()
        {
            return new Scenario
            {
                Name = "raster-handover",
                Why = "asking for the PNGs on a table whose classic slots point elsewhere must go through",
                Wire = s =>
                {
                    s.Tables.Add(SlowService.Table("contoso_legacy", "Contoso Legacy",
                        icon: "abc_/images/thing.svg",
                        small: "abc_/images/thing16.png",
                        medium: "abc_/images/thing32.png"));
                    s.Latency = Slow("webresource-find", 300);
                }
            }
                .At(200, "press Load", r => r.Probe.PressLoad())
                .At(1600, "pick an icon, the old table, and the PNGs", r =>
                {
                    r.Probe.PickFirstIcon();

                    // This table already has an icon, which is the point of it - so it is not in
                    // the list the window opens on, and seeing it takes unticking the box.
                    r.Probe.UnsetOnly.Checked = false;

                    r.Probe.PickTable("contoso_legacy");
                    r.Probe.Raster.Checked = true;
                })
                .At(2100, "press Apply and confirm", r => r.Probe.PressApply())
                .At(7000, "count what was written", r =>
                {
                    r.Check(r.Service.Log("webresource-find").Count == 5,
                        "the vector, the three PNGs and the attribution page's folder should each "
                        + "be looked up once, and there were "
                        + r.Service.Log("webresource-find").Count);
                    r.Check(r.Service.Log("publish").Count == 1,
                        "one publish, and there were " + r.Service.Log("publish").Count);
                    r.Check(r.Dialogs.Count(d => d == "Set table icon") == 1,
                        "one confirm, and there were: " + string.Join(" | ", r.Dialogs));
                    r.Check(r.Probe.Status.IndexOf("Set ", StringComparison.Ordinal) >= 0,
                        "and the write should have landed, but the status line says \""
                        + r.Probe.Status + "\"");
                });
        }
    }
}
