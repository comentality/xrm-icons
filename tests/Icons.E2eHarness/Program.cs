using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Icons.Logic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Tooling.Connector;

namespace Icons.E2eHarness
{
    /// <summary>
    /// The tool, driven for real, against a real Dataverse. Run it through e2e.ps1 rather than
    /// directly.
    ///
    ///   Icons.E2eHarness.exe &lt;org url&gt; &lt;username&gt; &lt;token cache&gt; &lt;output folder&gt; [--keep]
    ///
    /// It creates a table of its own, gives it an icon in a folder of its own so there is a
    /// convention to be read, drives the same control the other harnesses drive through the whole
    /// workflow - load, search, pick, apply, re-apply, the classic PNGs, a path typed by hand -
    /// and then asks the org what actually got written. Everything it creates it deletes on the
    /// way out; --keep leaves it there to look at.
    ///
    /// The sign in is borrowed, not performed: the token cache is a copy of the one the xtb.ps1
    /// sandbox saved, so this can run with nobody at the keyboard. No cache, no run.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// The borrowed sign in, as a connection. Made more than once: the bench needs a client
        /// that has not spoken to the org yet, because the first call pays for the token and the
        /// handshake and that is part of what it is measuring.
        /// </summary>
        internal static CrmServiceClient Connect(string url, string username, string cache)
        {
            return new CrmServiceClient(
                "AuthType=OAuth;Url=" + url
                + ";Username=" + username
                + ";AppId=51f81489-12ee-4a9e-aaae-a2591f45987d"
                + ";RedirectUri=app://58145B91-0C36-4500-8554-080854F2AC97"
                + ";TokenCacheStorePath=" + cache
                + ";LoginPrompt=Never;RequireNewInstance=True");
        }

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 4)
            {
                Console.Error.WriteLine(
                    "usage: Icons.E2eHarness.exe <org url> <username> <token cache> <output folder> [--keep]");
                return 2;
            }

            var url = args[0];
            var username = args[1];
            var cache = args[2];
            var root = Path.GetFullPath(args[3]);
            var keep = args.Contains("--keep");

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Console.WriteLine("        connecting to " + url + " as " + username);
            var client = Connect(url, username, cache);
            if (!client.IsReady)
            {
                Console.Error.WriteLine("The borrowed sign in did not take: " + client.LastCrmError);
                return 3;
            }

            // A different question, asked of the same org through the same borrowed sign in:
            // what XrmToolBox's metadata cache would be worth to this tool. No window is opened
            // for it, so it answers before the run that needs one.
            if (args.Contains("--bench-cache"))
            {
                return CacheBench.Run(client, url, username, cache, root);
            }

            // Another question of the same org: what an app's navigation is made of, and how
            // much of it could be drawn. Reads only, so it never reaches the write run below.
            if (args.Contains("--sitemap"))
            {
                return SiteMapProbe.Run(client, root);
            }

            var org = new Org(client);
            org.EnsureTable();

            // The fixture table arrives with an icon already on it, in a folder of its own that
            // this tool would never have guessed. Everything about the suggestion is judged
            // against that: an org that already keeps its icons somewhere has an answer, and the
            // tool is supposed to read it rather than impose one.
            var solution = org.PickSolution();
            var seedName = org.SeedIcon(solution);
            Console.WriteLine("        seeded " + seedName + ", through " + solution.UniqueName);

            // The tool chooses a solution only when the environment names one, so the run names
            // one - the same way a person does, through the maker portal's preferred solution.
            // Whatever was set before goes back at the end: it is the signed-in user's setting.
            var preferredBefore = org.PreferredSolution();
            org.SetPreferredSolution(solution.Id);
            Console.WriteLine("        set the preferred solution to " + solution.UniqueName
                              + " (it was " + (preferredBefore == Guid.Empty ? "unset" : preferredBefore.ToString()) + ")");

            // What the run learns as it goes: the name the tool suggested is the key to
            // everything the org is asked afterwards, and to what cleanup deletes.
            string vectorName = null;
            Guid vectorId = Guid.Empty;
            string firstContent = null;
            string typedName = null;
            Func<string, string> raster = slot =>
            {
                if (vectorName == null) return null;
                var stem = vectorName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                    ? vectorName.Substring(0, vectorName.Length - 4)
                    : vectorName;
                return stem + "_" + slot + ".png";
            };

            var steps = new List<Step>
            {
                new Step
                {
                    Label = "press Load",
                    Do = r => r.Probe.PressLoad(),
                    Until = r => !r.Probe.LoadingTables && !r.Probe.LoadingSolutions
                                 && r.Probe.Tables.Items.Count > 0,
                    TimeoutMs = 180000
                },
                new Step
                {
                    Label = "search for calendar",
                    Do = r => r.Probe.Field<TextBox>("_searchBox").Text = "calendar",
                    Until = r => r.Probe.Grid.VirtualListSize > 0,
                    TimeoutMs = 10000
                },
                new Step { Label = "pick the first icon", Do = r => r.Probe.PickFirstIcon() },
                new Step
                {
                    Label = "pick the fixture table",
                    Do = r => r.Probe.PickTable(Org.TableLogicalName),
                    Until = r => !string.IsNullOrWhiteSpace(r.Probe.Name.Text),
                    TimeoutMs = 10000
                },
                new Step
                {
                    Label = "the environment's preferred solution is the one chosen",
                    Do = r =>
                    {
                        r.Check(r.Probe.SelectedSolution == solution.UniqueName,
                            "the solution this user prefers should be the chosen one, and the box holds \""
                            + (r.Probe.SelectedSolution ?? "nothing") + "\"");
                        r.Check(r.Probe.Solutions.SelectedIndex == 0,
                            "and it should be the row the list opens on, not one further down; it is row "
                            + r.Probe.Solutions.SelectedIndex);
                    }
                },
                new Step
                {
                    Label = "the suggestion follows the icon the table already has",
                    Do = r => r.Check(r.Probe.Name.Text == seedName,
                        "the table already carries " + seedName
                        + ", which is the name a second icon for it belongs under, and the box offers \""
                        + r.Probe.Name.Text + "\"")
                },
                new Step
                {
                    Label = "a solution is required",
                    Do = r =>
                    {
                        // Nothing else here has changed: an icon and a table are chosen and the
                        // name is good. Only the solution goes away, and it alone has to be
                        // enough to stop the write - the default solution is where a web resource
                        // goes when nobody chooses, and it is not a choice anybody makes on purpose.
                        var chosen = r.Probe.Solutions.SelectedIndex;
                        r.Probe.Solutions.SelectedIndex = -1;
                        r.Check(!r.Probe.Apply.Enabled,
                            "with no solution chosen there is nowhere to file the web resource, so Apply must be dead");

                        r.Probe.Solutions.SelectedIndex = chosen;
                        r.Check(r.Probe.Apply.Enabled,
                            "and live again the moment a solution is chosen back");
                        r.Check(r.Probe.Name.Text == seedName,
                            "the name must survive the solution going and coming back, and the box holds \""
                            + r.Probe.Name.Text + "\"");
                    }
                },
                new Step
                {
                    Label = "note the suggested name",
                    Do = r =>
                    {
                        vectorName = r.Probe.Name.Text;
                        Console.WriteLine("        the tool suggested " + vectorName);
                    }
                },
                new Step
                {
                    Label = "apply",
                    Do = r => r.Probe.PressApply(),
                    Until = r => !r.Probe.Applying && r.Probe.Apply.Enabled,
                    TimeoutMs = 240000
                },
                new Step
                {
                    Label = "the org has the icon",
                    Do = r =>
                    {
                        r.Check(r.Probe.Trouble == null,
                            "nothing should be wrong after a clean apply, and the trouble line says \""
                            + (r.Probe.Trouble ?? "") + "\"");
                        r.Check(r.Dialogs.Count(d => d == "Set table icon") == 1,
                            "one confirm should have been asked, and the dialogs were: "
                            + string.Join(" | ", r.Dialogs));

                        var found = org.WebResourcesNamed(vectorName);
                        r.Check(found.Count == 1,
                            "exactly one web resource should carry the name, and there are " + found.Count);
                        if (found.Count == 1)
                        {
                            vectorId = found[0].Id;
                            firstContent = found[0].GetAttributeValue<string>("content");
                            r.Check(found[0].GetAttributeValue<OptionSetValue>("webresourcetype").Value == 11,
                                "it should be an SVG web resource (type 11)");
                            var svg = Encoding.UTF8.GetString(Convert.FromBase64String(firstContent ?? ""));
                            r.Check(svg.Contains("currentColor"),
                                "the SVG should follow the theme by default, and does not mention currentColor");

                            // The platform's answer, not this tool's: a stored icon carries a
                            // comment, and the attribution page is rebuilt from those comments.
                            // An SVG the platform stripped or refused over it would leave every
                            // icon in the org uncreditable, and nothing local would notice.
                            r.Check(svg.Contains(Provenance.Marker),
                                "the stored SVG should still carry its provenance comment, and it does not");
                        }

                        var pageName = AttributionPage.NameFor(vectorName);
                        var page = org.WebResourcesNamed(pageName);
                        r.Check(page.Count == 1,
                            "an attribution page should have been written beside the icon as " + pageName
                            + ", and there are " + page.Count);
                        if (page.Count == 1)
                        {
                            r.Check(page[0].GetAttributeValue<OptionSetValue>("webresourcetype").Value == 1,
                                "the page should be an HTML web resource (type 1)");

                            var html = Encoding.UTF8.GetString(
                                Convert.FromBase64String(page[0].GetAttributeValue<string>("content") ?? ""));
                            r.Check(html.Contains(vectorName),
                                "the page should credit the icon that was just written, and does not name it");
                        }

                        var table = org.TableIcons();
                        r.Check(table.IconVectorName == vectorName,
                            "the table should point at it, and IconVectorName is \""
                            + (table.IconVectorName ?? "nothing") + "\"");
                    }
                },
                new Step
                {
                    Label = "search for settings",
                    Do = r => r.Probe.Field<TextBox>("_searchBox").Text = "settings",
                    Until = r => r.Probe.Grid.VirtualListSize > 0,
                    TimeoutMs = 10000
                },
                new Step { Label = "pick a different icon", Do = r => r.Probe.PickFirstIcon() },
                new Step
                {
                    Label = "apply again",
                    Do = r => r.Probe.PressApply(),
                    Until = r => !r.Probe.Applying && r.Probe.Apply.Enabled,
                    TimeoutMs = 240000
                },
                new Step
                {
                    Label = "same web resource, new drawing",
                    Do = r =>
                    {
                        var found = org.WebResourcesNamed(vectorName);
                        r.Check(found.Count == 1,
                            "a second apply must update, not duplicate, and the name is now carried by "
                            + found.Count + " web resources");
                        if (found.Count == 1)
                        {
                            r.Check(found[0].Id == vectorId,
                                "the web resource should keep its identity across applies");
                            r.Check(found[0].GetAttributeValue<string>("content") != firstContent,
                                "and its drawing should have changed");
                        }
                    }
                },
                new Step
                {
                    Label = "ask for the classic PNGs too",
                    Do = r => r.Probe.Field<CheckBox>("_rasterBox").Checked = true
                },
                new Step
                {
                    Label = "apply with rasters",
                    Do = r => r.Probe.PressApply(),
                    Until = r => !r.Probe.Applying && r.Probe.Apply.Enabled,
                    TimeoutMs = 300000
                },
                new Step
                {
                    Label = "the classic slots are filled",
                    Do = r =>
                    {
                        var table = org.TableIcons();
                        r.Check(table.IconSmallName == raster("small"),
                            "IconSmallName should be " + raster("small") + " and is \""
                            + (table.IconSmallName ?? "nothing") + "\"");
                        r.Check(table.IconMediumName == raster("medium"),
                            "IconMediumName should be " + raster("medium") + " and is \""
                            + (table.IconMediumName ?? "nothing") + "\"");
                        r.Check(table.IconLargeName == raster("large"),
                            "IconLargeName should be " + raster("large") + " and is \""
                            + (table.IconLargeName ?? "nothing") + "\"");

                        foreach (var slot in new[] { "small", "medium", "large" })
                        {
                            var found = org.WebResourcesNamed(raster(slot));
                            r.Check(found.Count == 1,
                                "exactly one web resource should carry " + raster(slot)
                                + ", and there are " + found.Count);
                            if (found.Count != 1) continue;

                            r.Check(found[0].GetAttributeValue<OptionSetValue>("webresourcetype").Value == 5,
                                raster(slot) + " should be a PNG web resource (type 5)");
                            var bytes = Convert.FromBase64String(
                                found[0].GetAttributeValue<string>("content") ?? "");
                            r.Check(bytes.Length > 8 && bytes[1] == 'P' && bytes[2] == 'N' && bytes[3] == 'G',
                                raster(slot) + " should actually hold a PNG");
                        }
                    }
                },
                new Step
                {
                    Label = "type a path of your own",
                    Do = r =>
                    {
                        // The guess is a suggestion, not a decision. Whatever the tool worked out,
                        // a typed name has to be the one that is written - and it has to be
                        // written under the prefix of the solution the write is going through, or
                        // the platform refuses the create.
                        r.Probe.Raster.Checked = false;
                        typedName = r.Probe.SelectedPrefix + "_/e2etyped/icon.svg";
                        r.Probe.Name.Text = typedName;

                        r.Check(r.Probe.Apply.Enabled,
                            "a valid typed name should leave Apply live");
                    }
                },
                new Step
                {
                    Label = "apply to the typed path",
                    Do = r => r.Probe.PressApply(),
                    Until = r => !r.Probe.Applying && r.Probe.Apply.Enabled,
                    TimeoutMs = 240000
                },
                new Step
                {
                    Label = "the icon went where it was typed",
                    Do = r =>
                    {
                        r.Check(r.Probe.Trouble == null,
                            "nothing should be wrong after applying to a typed path, and the trouble line says \""
                            + (r.Probe.Trouble ?? "") + "\"");

                        var found = org.WebResourcesNamed(typedName);
                        r.Check(found.Count == 1,
                            "the typed name should be carried by exactly one web resource, and it is carried by "
                            + found.Count);

                        var table = org.TableIcons();
                        r.Check(table.IconVectorName == typedName,
                            "the table should point at the typed name, and IconVectorName is \""
                            + (table.IconVectorName ?? "nothing") + "\"");

                        if (found.Count != 1) return;

                        // The reason a solution is required at all: a web resource that only ever
                        // reached the default solution is one nothing you ship can see.
                        var holders = org.SolutionsHolding(found[0].Id);
                        r.Check(holders.Any(h => string.Equals(h, r.Probe.SelectedSolution,
                                    StringComparison.OrdinalIgnoreCase)),
                            "the new web resource should be in the chosen solution "
                            + r.Probe.SelectedSolution + ", and it is in: "
                            + (holders.Count == 0 ? "nothing" : string.Join(", ", holders)));
                    }
                },
                new Step
                {
                    Label = "take the last write back",
                    Do = r => r.Probe.PressUndo(),
                    Until = r => !r.Probe.Applying && r.Probe.Apply.Enabled,
                    TimeoutMs = 240000
                },
                new Step
                {
                    Label = "the table points back at what it had before",
                    Do = r =>
                    {
                        r.Check(r.Probe.Trouble == null,
                            "nothing should be wrong after a clean undo, and the trouble line says \""
                            + (r.Probe.Trouble ?? "") + "\"");
                        r.Check(r.Dialogs.Count(d => d == "Undo the last change") == 1,
                            "one confirm should have been asked, and the dialogs were: "
                            + string.Join(" | ", r.Dialogs));

                        var table = org.TableIcons();
                        r.Check(table.IconVectorName == vectorName,
                            "the table should point at " + vectorName
                            + " again - what it held before the typed write - and IconVectorName is \""
                            + (table.IconVectorName ?? "nothing") + "\"");

                        r.Check(org.WebResourcesNamed(typedName).Count == 1,
                            "and the web resource it stopped pointing at should still be there: "
                            + "nothing here deletes one");

                        r.Check(r.Probe.Redo.Enabled, "with the change offered back the other way");
                    }
                },
                new Step
                {
                    Label = "do it again",
                    Do = r => r.Probe.PressRedo(),
                    Until = r => !r.Probe.Applying && r.Probe.Apply.Enabled,
                    TimeoutMs = 240000
                },
                new Step
                {
                    Label = "the typed icon is on again",
                    Do = r =>
                    {
                        var table = org.TableIcons();
                        r.Check(table.IconVectorName == typedName,
                            "a redo should put the typed name back on, and IconVectorName is \""
                            + (table.IconVectorName ?? "nothing") + "\"");
                        r.Check(org.WebResourcesNamed(typedName).Count == 1,
                            "with the web resource updated rather than duplicated");
                        r.Check(r.Probe.Undo.Enabled, "and the two buttons swapped back over");
                    }
                },
                new Step
                {
                    Label = "an icon can be taken off a table entirely",
                    Do = r =>
                    {
                        // The one path the window cannot reach here: undo puts back whatever the
                        // table held, and this fixture always held something. A table that had no
                        // icon when it was applied to has to end up with none again.
                        //
                        // Which is not the property being set to null. Null is read as "this one
                        // is not part of the update" and the icon stays exactly where it was;
                        // an empty string is what clears it, and an empty string is what Revert
                        // writes for nothing. That is the platform's answer rather than this
                        // tool's, so it is asked of the platform, once, directly, on every run -
                        // the day it changes, this line is the one that says so.
                        IconApplier.Revert(client, new IconRevertRequest
                        {
                            TableLogicalName = Org.TableLogicalName,
                            VectorName = null
                        });

                        var table = org.TableIcons();
                        r.Check(string.IsNullOrEmpty(table.IconVectorName),
                            "writing null should leave the table with no vector icon, and IconVectorName is \""
                            + (table.IconVectorName ?? "nothing") + "\"");
                    }
                }
            };

            Directory.CreateDirectory(root);

            List<string> failures;

            // Anything the run changed about the person signed in, rather than about its own
            // fixtures. It is reported with the findings, because a run that quietly leaves
            // somebody's environment set to what it wanted is worse than a run that fails.
            string leftBehind = null;

            try
            {
                failures = Driver.Play(client, steps, Path.Combine(root, "run"));
            }
            finally
            {
                // Before anything else, and whether the run is keeping its fixtures or not: the
                // preferred solution belongs to the person signed in, not to this run.
                if (preferredBefore != Guid.Empty)
                {
                    try
                    {
                        var after = new Org(client);
                        after.SetPreferredSolution(preferredBefore);

                        // Asked rather than assumed: the whole point of putting it back is that it
                        // is actually back.
                        var now = after.PreferredSolution();
                        if (now == preferredBefore)
                        {
                            Console.WriteLine("        put the preferred solution back to " + preferredBefore);
                        }
                        else
                        {
                            leftBehind = "the preferred solution was left as " + now + ", not the "
                                         + preferredBefore + " it was before this run";
                        }
                    }
                    catch (Exception ex)
                    {
                        leftBehind = "could not put the preferred solution back to "
                                     + preferredBefore + ": " + ex.Message;
                    }

                    if (leftBehind != null) Console.WriteLine("        " + leftBehind);
                }

                if (keep)
                {
                    Console.WriteLine("        kept: " + Org.TableLogicalName + " and "
                                      + string.Join(", ", new[] { seedName, vectorName, typedName }
                                          .Where(n => n != null)));
                }
                else
                {
                    Console.WriteLine("        cleaning up after the run");
                    var names = new[]
                    {
                        seedName, vectorName, typedName,
                        raster("small"), raster("medium"), raster("large"),
                        // One per folder the run wrote into: the typed path is somewhere else,
                        // and it got its own page rather than a mention on this one.
                        vectorName == null ? null : AttributionPage.NameFor(vectorName),
                        typedName == null ? null : AttributionPage.NameFor(typedName)
                    };
                    foreach (var problem in new Org(client).Cleanup(names))
                    {
                        Console.WriteLine("        " + problem);
                    }
                }
            }

            // Counted as a finding: it is one, and it is about the environment rather than the
            // tool, which is exactly the kind that goes unnoticed otherwise.
            if (leftBehind != null) failures.Add(leftBehind);

            var report = new List<string>();
            foreach (var failure in failures)
            {
                Console.WriteLine("          " + failure);
                report.Add("          " + failure);
            }

            var verdict = failures.Count == 0
                ? "e2e clean: the tool wrote, updated and rastered a real icon on a real org."
                : failures.Count + " finding" + (failures.Count == 1 ? "" : "s") + ".";
            Console.WriteLine(verdict + "  Shots and report in " + root);
            report.Add(verdict);
            File.WriteAllLines(Path.Combine(root, "report.txt"), report);

            return failures.Count;
        }
    }
}
