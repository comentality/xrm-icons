using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Tooling.Connector;

namespace Icons.E2eHarness
{
    /// <summary>
    /// What XrmToolBox's own metadata cache would be worth to this tool, measured against a real
    /// environment rather than argued about.
    ///
    /// The tool reads every table once per load, through RetrieveAllEntities. XrmToolBox already
    /// holds the same metadata per connection - ConnectionDetail.MetadataCache, kept current
    /// through a version stamp - so the question is whether reading that is faster than asking
    /// the org, and whether it tells the truth about a table somebody made a minute ago.
    ///
    /// Nothing here touches the cache of a real connection: the bench uses connection ids of its
    /// own, so it writes its own files beside them, and it says at the end whether anything it
    /// did not create changed while it ran.
    /// </summary>
    internal static class CacheBench
    {
        private sealed class Row
        {
            public string Label;
            public readonly List<long> Ms = new List<long>();
            public int Count;
            public string Note = "";
        }

        private static readonly List<Row> Rows = new List<Row>();

        /// <summary>
        /// Everywhere a cache file could land. The folder is the library's own business and it
        /// is not the same one in XrmToolBox as it is out here: inside, it is under the roaming
        /// profile beside the connections; out here it followed this exe into its build output.
        /// Both are watched, so "the bench wrote a file" and "the bench touched a real
        /// connection's file" are both answerable rather than assumed.
        /// </summary>
        private static IEnumerable<string> MetadataFolders
        {
            get
            {
                yield return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MscrmTools", "XrmToolBox", "Metadata");

                var appBase = AppDomain.CurrentDomain.BaseDirectory;
                yield return Path.Combine(appBase, "Metadata");
                yield return Path.GetFullPath(Path.Combine(appBase, "..", "Metadata"));
            }
        }

        public static int Run(CrmServiceClient client, string url, string username, string tokenCache, string root)
        {
            var findings = new List<string>();
            var before = Snapshot();
            var ours = new List<Guid>();
            var org = new Org(client);
            string seedName = null;

            Say("watching for cache files in: " + string.Join(", ", MetadataFolders.Distinct()));
            Say("they hold " + before.Count + " file(s) before this run, none of them this bench's");

            try
            {
                // The fixture table has to be absent before the run, or "a table you made a
                // minute ago" is a table that was already in the cache.
                if (org.TableExists())
                {
                    Say("removing a leftover " + Org.TableLogicalName + " so the new-table timing is honest");
                    foreach (var problem in org.Cleanup(new string[0])) Say(problem);
                }

                // ===== what the tool does today =====

                Measure("RetrieveAllEntities (what Load tables costs now)", 3, () => AllEntities(client).Length);

                // ===== the cache, cold =====

                var coldId = Guid.NewGuid();
                ours.Add(coldId);
                var cold = Detail(client, coldId, url, username);

                Say("filling a cache from nothing - a full metadata download, this takes a while");
                var coldRow = Measure("MetadataCache, cold (first ever load)", 1, () =>
                {
                    Update(cold, true);
                    return cold.MetadataCache == null ? 0 : cold.MetadataCache.Length;
                });
                if (coldRow.Count == 0)
                {
                    findings.Add("the cold cache came back empty, so nothing below is worth reading");
                    return Report(findings, root);
                }

                // ===== the cache, warm, in the same process =====

                Measure("MetadataCache, warm in memory (version stamp check)", 3, () =>
                {
                    Update(cold, false);
                    return cold.MetadataCache.Length;
                });

                // What the tool would actually have to do with it: everything the load reads,
                // shaped the way the window needs it. On a lazy structure this is where the
                // cost would land rather than on the call above.
                Measure("...and turning it into the tool's table list", 3, () => Tables(cold.MetadataCache).Count);

                // ===== the cache, warm, from disk =====
                //
                // A fresh ConnectionDetail on the same connection id is XrmToolBox started again:
                // nothing in memory, a cache file on disk, and a stamp to check it against.

                // Asked here rather than the moment the fill returned: the file is written
                // through a temporary one, and looking for it too early says nothing was
                // written when a second later there is 8 MB of it.
                coldRow.Note = FileNote(coldId);

                Measure("MetadataCache from its file (new instance, XrmToolBox starting)", 2, () =>
                {
                    var warm = Detail(client, coldId, url, username);
                    Update(warm, false);
                    return warm.MetadataCache == null ? 0 : Tables(warm.MetadataCache).Count;
                });

                // ===== jumped straight in =====
                //
                // Both paths on a client that has not spoken to the org yet, which is where a
                // load on connect would really happen: token, TLS and the first call included.

                Measure("RetrieveAllEntities on a cold client", 1, () =>
                {
                    using (var fresh = Program.Connect(url, username, tokenCache))
                        return AllEntities(fresh).Length;
                });

                Measure("MetadataCache from disk on a cold client", 1, () =>
                {
                    using (var fresh = Program.Connect(url, username, tokenCache))
                    {
                        var warm = Detail(fresh, coldId, url, username);
                        Update(warm, false);
                        return warm.MetadataCache == null ? 0 : Tables(warm.MetadataCache).Count;
                    }
                });

                // ===== a table made a minute ago =====

                Say("creating " + Org.TableLogicalName + " (a real table takes a while)");
                var made = Stopwatch.StartNew();
                org.EnsureTable();
                made.Stop();
                Say("the org took " + made.ElapsedMilliseconds + "ms to create and publish it");

                // On the instance that has been held all along, which is what XrmToolBox keeps
                // for a connection: the stamp says the org has moved, and this is what catching
                // up costs. Not a fresh instance - that one has no memory of the org and would
                // be measuring the startup load again.
                var afterNewTable = Measure("MetadataCache delta on the held instance (new table)", 1, () =>
                {
                    Update(cold, false);
                    return cold.MetadataCache == null ? 0 : cold.MetadataCache.Length;
                });
                var cacheSawIt = cold.MetadataCache != null
                                 && cold.MetadataCache.Any(e => e.LogicalName == Org.TableLogicalName);
                afterNewTable.Note = cacheSawIt ? "the new table is in it" : "THE NEW TABLE IS NOT IN IT";
                if (!cacheSawIt)
                    findings.Add("a table created and published a moment ago is missing from the delta-updated cache");

                var direct = Measure("RetrieveAllEntities at the same moment", 1, () =>
                    AllEntities(client).Length);
                var orgSawIt = AllEntities(client).Any(e => e.LogicalName == Org.TableLogicalName);
                direct.Note = orgSawIt ? "the new table is in it" : "THE NEW TABLE IS NOT IN IT EITHER";
                if (!orgSawIt)
                    findings.Add("the direct call cannot see the new table either, so this is the org, not the cache");

                // ===== an icon written a minute ago =====

                var solution = org.PickSolution();
                seedName = org.SeedIcon(solution);
                Say("set IconVectorName to " + seedName + " and published it");

                var afterIcon = Measure("MetadataCache delta on the held instance (icon written)", 1, () =>
                {
                    Update(cold, false);
                    return cold.MetadataCache == null ? 0 : cold.MetadataCache.Length;
                });
                var table = cold.MetadataCache?.FirstOrDefault(e => e.LogicalName == Org.TableLogicalName);
                var iconSeen = table != null && table.IconVectorName == seedName;
                afterIcon.Note = iconSeen
                    ? "the icon is on it"
                    : "the icon reads as \"" + (table == null ? "no such table" : table.IconVectorName ?? "null") + "\"";
                if (!iconSeen)
                    findings.Add("the cache does not carry an icon written a moment ago, so a tool "
                                 + "reading it would offer to set one that is already set");
            }
            catch (Exception ex)
            {
                findings.Add("the bench stopped early: " + ex.GetBaseException());
            }
            finally
            {
                Say("cleaning up");
                foreach (var problem in org.Cleanup(new[] { seedName })) Say(problem);

                foreach (var id in ours)
                {
                    foreach (var file in CacheFiles(id + "*").ToList())
                    {
                        try { File.Delete(file); }
                        catch (Exception ex) { findings.Add("could not delete the bench's own cache file " + file + ": " + ex.Message); }
                    }
                }

                foreach (var changed in Changed(before)) findings.Add(changed);
            }

            return Report(findings, root);
        }

        // ===== the two ways of asking =====

        private static EntityMetadata[] AllEntities(Microsoft.Xrm.Sdk.IOrganizationService service)
        {
            var response = (RetrieveAllEntitiesResponse)service.Execute(new RetrieveAllEntitiesRequest
            {
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            });
            return response.EntityMetadata;
        }

        /// <summary>Everything the tool's load does with what comes back, so the cost of it counts.</summary>
        private static List<string> Tables(IEnumerable<EntityMetadata> metadata)
        {
            return metadata
                .Where(e => e.IsIntersect != true && e.IsPrivate != true)
                .Select(e => (e.DisplayName?.UserLocalizedLabel?.Label ?? e.SchemaName ?? e.LogicalName)
                             + "|" + e.IconVectorName + "|" + (e.IsCustomizable == null || e.IsCustomizable.Value))
                .OrderBy(t => t, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static ConnectionDetail Detail(CrmServiceClient client, Guid id, string url, string username)
        {
            return new ConnectionDetail
            {
                ConnectionId = id,
                ConnectionName = "Icons cache bench",
                ServiceClient = client,
                UserName = username,
                OriginalUrl = url,
                WebApplicationUrl = url,
                OrganizationServiceUrl = url.TrimEnd('/') + "/XRMServices/2011/Organization.svc",
                Organization = client.ConnectedOrgUniqueName,
                OrganizationUrlName = client.ConnectedOrgUniqueName,
                OrganizationFriendlyName = client.ConnectedOrgFriendlyName,
                OrganizationVersion = client.ConnectedOrgVersion?.ToString()
            };
        }

        /// <summary>
        /// Run on a thread of its own: the cache loader is async, and this is called from the
        /// harness's UI thread with no message loop to pump a continuation back onto.
        /// </summary>
        private static void Update(ConnectionDetail detail, bool flush)
        {
            Task.Run(async () => await detail.UpdateMetadataCache(flush)).GetAwaiter().GetResult();
        }

        // ===== measuring =====

        private static Row Measure(string label, int times, Func<int> once)
        {
            var row = new Row { Label = label };
            for (var i = 0; i < times; i++)
            {
                var clock = Stopwatch.StartNew();
                row.Count = once();
                clock.Stop();
                row.Ms.Add(clock.ElapsedMilliseconds);
            }

            Rows.Add(row);
            Say(label + ": " + string.Join(", ", row.Ms.Select(m => m + "ms")) + "  (" + row.Count + ")");
            return row;
        }

        // ===== what was on disk, and what is now =====

        private static Dictionary<string, string> Snapshot()
        {
            var seen = new Dictionary<string, string>();
            foreach (var file in CacheFiles("*"))
            {
                var info = new FileInfo(file);
                seen[info.FullName] = info.Length + "@" + info.LastWriteTimeUtc.Ticks;
            }
            return seen;
        }

        private static IEnumerable<string> CacheFiles(string pattern)
        {
            foreach (var folder in MetadataFolders.Distinct())
            {
                if (!Directory.Exists(folder)) continue;

                foreach (var file in Directory.GetFiles(folder, pattern)) yield return file;
            }
        }

        private static IEnumerable<string> Changed(Dictionary<string, string> before)
        {
            var after = Snapshot();

            foreach (var was in before)
            {
                string now;
                if (!after.TryGetValue(was.Key, out now))
                    yield return "the bench removed a cache file it did not create: " + was.Key;
                else if (now != was.Value)
                    yield return "the bench changed a cache file it did not create: " + was.Key;
            }

            foreach (var left in after.Keys.Where(k => !before.ContainsKey(k)))
                yield return "the bench left a cache file behind: " + left;
        }

        private static string FileNote(Guid id)
        {
            var file = CacheFiles(id + "*").FirstOrDefault();
            if (file == null) return "nothing was written to disk";

            return (new FileInfo(file).Length / 1024) + " KB written to " + Path.GetDirectoryName(file);
        }

        // ===== saying what happened =====

        private static void Say(string line)
        {
            Console.WriteLine("        " + line);
        }

        private static int Report(List<string> findings, string root)
        {
            var report = new List<string>
            {
                "",
                string.Format("{0,-58} {1,-26} {2,7}  {3}", "what was asked", "how long", "count", "note")
            };

            foreach (var row in Rows)
            {
                report.Add(string.Format("{0,-58} {1,-26} {2,7}  {3}",
                    row.Label,
                    string.Join(" ", row.Ms.Select(m => m + "ms")),
                    row.Count,
                    row.Note));
            }

            report.Add("");
            foreach (var finding in findings) report.Add("  " + finding);
            report.Add(findings.Count == 0
                ? "cache bench clean: nothing it did not create was touched."
                : findings.Count + " finding" + (findings.Count == 1 ? "" : "s") + ".");

            foreach (var line in report) Console.WriteLine(line);
            Directory.CreateDirectory(root);
            File.WriteAllLines(Path.Combine(root, "bench-cache.txt"), report);

            return findings.Count;
        }
    }
}
