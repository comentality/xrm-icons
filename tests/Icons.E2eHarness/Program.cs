using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
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
    /// It creates a table of its own, drives the same control the other harnesses drive through
    /// the whole workflow - load, search, pick, apply, re-apply, the classic PNGs - and then asks
    /// the org what actually got written. Everything it creates it deletes on the way out;
    /// --keep leaves it there to look at.
    ///
    /// The sign in is borrowed, not performed: the token cache is a copy of the one the xtb.ps1
    /// sandbox saved, so this can run with nobody at the keyboard. No cache, no run.
    /// </summary>
    internal static class Program
    {
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
            var client = new CrmServiceClient(
                "AuthType=OAuth;Url=" + url
                + ";Username=" + username
                + ";AppId=51f81489-12ee-4a9e-aaae-a2591f45987d"
                + ";RedirectUri=app://58145B91-0C36-4500-8554-080854F2AC97"
                + ";TokenCacheStorePath=" + cache
                + ";LoginPrompt=Never;RequireNewInstance=True");
            if (!client.IsReady)
            {
                Console.Error.WriteLine("The borrowed sign in did not take: " + client.LastCrmError);
                return 3;
            }

            var org = new Org(client);
            org.EnsureTable();

            // What the run learns as it goes: the name the tool suggested is the key to
            // everything the org is asked afterwards, and to what cleanup deletes.
            string vectorName = null;
            Guid vectorId = Guid.Empty;
            string firstContent = null;
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
                }
            };

            Directory.CreateDirectory(root);

            List<string> failures;
            try
            {
                failures = Driver.Play(client, steps, Path.Combine(root, "run"));
            }
            finally
            {
                if (keep)
                {
                    Console.WriteLine("        kept: " + Org.TableLogicalName + " and "
                                      + (vectorName ?? "no web resources yet"));
                }
                else
                {
                    Console.WriteLine("        cleaning up after the run");
                    var names = new[] { vectorName, raster("small"), raster("medium"), raster("large") };
                    foreach (var problem in new Org(client).Cleanup(names))
                    {
                        Console.WriteLine("        " + problem);
                    }
                }
            }

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
