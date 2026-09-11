using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using Icons.Catalog;
using Icons.Logic;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// Recognising an icon that is already in an environment. The question these answer is the
    /// one the window asks the moment a table with an icon is chosen: which of ours is that?
    /// </summary>
    [TestFixture]
    public class IdentityTests
    {
        private IconCatalog _catalog;

        [OneTimeSetUp]
        public void LoadCatalog()
        {
            _catalog = IconCatalog.Load();

            _catalog.Index();
        }

        [OneTimeTearDown]
        public void DisposeCatalog()
        {
            _catalog?.Dispose();
        }

        [Test]
        public void Indexing_says_when_it_is_done()
        {
            Assert.That(_catalog.Indexed, Is.True);
        }

        /// <summary>
        /// The index is carried in the bundle rather than worked out here. Fingerprinting all
        /// 18,637 icons is half a second and about twenty megabytes of garbage, on every
        /// connection and in every XrmToolBox tab, for an answer that cannot change between
        /// builds - so tools\index-catalog.ps1 works it out once and puts it in the archive.
        ///
        /// The number is printed rather than asserted on: what matters is where the index came
        /// from, and a millisecond budget on a build agent is a flaky test waiting to happen.
        /// </summary>
        [Test]
        public void The_index_comes_out_of_the_bundle()
        {
            using (var fresh = IconCatalog.Load())
            {
                var clock = Stopwatch.StartNew();
                fresh.Index();
                clock.Stop();

                TestContext.Out.WriteLine("Indexing " + fresh.Icons.Count + " icons took "
                                          + clock.ElapsedMilliseconds + " ms from the bundle.");

                Assert.That(fresh.IndexOrigin, Is.EqualTo(IconIndexOrigin.Bundled),
                    "the bundle should carry " + IconCatalog.IndexEntryName
                    + "; run tools\\index-catalog.ps1");
            }
        }

        /// <summary>
        /// A bundle carrying no index this build can read is still a working library, at the
        /// price of the half second. This is what an older tool over a newer bundle sees, and
        /// what anybody who rebuilds the archive without reindexing it sees.
        /// </summary>
        [Test]
        public void A_bundle_with_no_index_works_it_out()
        {
            using (var stripped = new MemoryStream(BundleWithout(IconCatalog.IndexEntryName)))
            using (var catalog = IconCatalog.Load(stripped))
            {
                catalog.Index();

                Assert.That(catalog.IndexOrigin, Is.EqualTo(IconIndexOrigin.Computed));

                var icon = catalog.Icons.First(i => i.Family == IconFamily.Lucide && i.Name == "folder");
                var found = IconIdentifier.Identify(
                    SvgOutput.Build(catalog.ReadSvg(icon), IconColourMode.Theme, Color.Black, null, null),
                    catalog);

                Assert.That(found?.Entry, Is.SameAs(icon), "and it identifies by drawing all the same");
            }
        }

        /// <summary>
        /// An index written against some earlier bundle is refused outright rather than half
        /// believed. This is the way the file actually goes wrong - an archive rebuilt from
        /// newer upstream releases and not reindexed - and half of an index would mean icons
        /// that quietly stopped being recognised.
        /// </summary>
        [Test]
        public void An_index_that_does_not_fit_the_bundle_is_refused()
        {
            var bundle = BundleWithout(null, index =>
                index.Replace("lucide/regular/folder.svg", "lucide/regular/folder-that-went.svg"));

            using (var stream = new MemoryStream(bundle))
            using (var catalog = IconCatalog.Load(stream))
            {
                catalog.Index();

                Assert.That(catalog.IndexOrigin, Is.EqualTo(IconIndexOrigin.Computed),
                    "an index naming an icon the archive does not hold is not this archive's index");
            }
        }

        /// <summary>
        /// Two icons that are not the same file must never come out as the same drawing.
        ///
        /// This is the one that says the canonicaliser is not throwing away something that
        /// matters. It caught exactly that: fill was dropped everywhere, so Tabler's
        /// percentage-0 and percentage-100 - one circle, hollow and solid - were one drawing,
        /// and so were the five transform-point icons, which say which corner is meant by
        /// filling that one square.
        ///
        /// The other way round is fine and expected: 348 pairs of bundled icons really are the
        /// same file under two names, which is upstream keeping an old name working.
        /// </summary>
        [Test]
        public void Two_icons_that_differ_are_never_one_drawing()
        {
            var byDrawing = new Dictionary<string, List<IconEntry>>(StringComparer.Ordinal);
            var source = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var icon in _catalog.Icons)
            {
                var svg = _catalog.ReadSvg(icon);
                source[icon.EntryPath] = Regex.Replace(svg, @"\s+", " ").Trim();

                var print = IconFingerprint.Of(svg);
                if (print == null) continue;

                List<IconEntry> sharing;
                if (!byDrawing.TryGetValue(print, out sharing))
                    byDrawing[print] = sharing = new List<IconEntry>();

                sharing.Add(icon);
            }

            var flattened = new List<string>();

            foreach (var sharing in byDrawing.Values.Where(g => g.Count > 1))
            {
                var texts = sharing.Select(i => source[i.EntryPath]).Distinct(StringComparer.Ordinal).Count();
                if (texts > 1)
                    flattened.Add(string.Join(" = ", sharing.Select(i => i.EntryPath)));
            }

            Assert.That(flattened, Is.Empty,
                "these icons are different files that the fingerprint cannot tell apart, so "
                + "something the drawing depends on is being left out of it");
        }

        /// <summary>
        /// The bundled index against a fresh fingerprinting of the bundle, icon for icon.
        ///
        /// This is the drift detector. The index is written by index-catalog.ps1 through the
        /// same canonicaliser that reads it, so the two can only disagree if the archive was
        /// rebuilt without reindexing or the canonical form changed without its version - and
        /// either of those is an index that quietly recognises nothing. Here it has to answer
        /// with the right icon for every one of the 18,637, and with nothing for exactly those
        /// whose drawing another name shares.
        /// </summary>
        [Test]
        public void The_bundled_index_says_what_the_drawings_say()
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var prints = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var icon in _catalog.Icons)
            {
                var print = IconFingerprint.Of(_catalog.ReadSvg(icon));
                prints[icon.EntryPath] = print;
                if (print == null) continue;

                int seen;
                counts[print] = counts.TryGetValue(print, out seen) ? seen + 1 : 1;
            }

            var wrong = new List<string>();
            var shared = 0;

            foreach (var icon in _catalog.Icons)
            {
                var print = prints[icon.EntryPath];
                var answer = print == null ? null : _catalog.Drawn(print);

                if (print != null && counts[print] > 1)
                {
                    shared++;
                    if (answer != null) wrong.Add(icon.EntryPath + " is a shared drawing but was named " + answer.EntryPath);
                }
                else if (!ReferenceEquals(answer, icon))
                {
                    wrong.Add(icon.EntryPath + " answered with " + (answer == null ? "nothing" : answer.EntryPath));
                }
            }

            TestContext.Out.WriteLine(shared + " of " + _catalog.Icons.Count
                                      + " icons are a drawing more than one name shares.");

            Assert.That(_catalog.IndexOrigin, Is.EqualTo(IconIndexOrigin.Bundled),
                "this only tests the bundled index if the bundled index is what was used");
            Assert.That(wrong, Is.Empty,
                "the bundled index disagrees with the drawings; run tools\\index-catalog.ps1");
            // 685 on this bundle, measured. The bound is close to it on purpose: a fingerprint
            // is 96 bits of a digest rather than the whole of it, and the only way a truncation
            // that started colliding would show is here, as drawings that are not really the
            // same one going quiet. Upstream adding a handful of aliases moves this by a
            // handful; a truncation gone wrong moves it by hundreds.
            Assert.That(shared, Is.LessThan(750),
                "more icons share a drawing than this bundle has ever had - if upstream did not "
                + "add aliases, the fingerprint is colliding on drawings that differ");
        }

        /// <summary>
        /// The one that matters: every icon in the bundle, put through the rewrite that storing
        /// it applies, comes back as itself. This is what the window relies on when it says
        /// what a table is already carrying, and it is also where the collision count and the
        /// cost of building the index are measured.
        /// </summary>
        [Test]
        public void Every_stored_icon_is_recognised_as_the_one_it_was()
        {
            var wrong = new List<string>();
            var unknown = new List<string>();

            var clock = Stopwatch.StartNew();
            foreach (var icon in _catalog.Icons)
            {
                var stored = SvgOutput.Build(
                    _catalog.ReadSvg(icon), IconColourMode.Fixed, Color.FromArgb(0x24, 0x24, 0x24),
                    32, IconProvenance.For(icon));

                var identity = IconIdentifier.Identify(stored, _catalog);

                if (identity == null) unknown.Add(icon.EntryPath);
                else if (!ReferenceEquals(identity.Entry, icon)) wrong.Add(icon.EntryPath + " read as " + identity.Described);
            }
            clock.Stop();

            TestContext.Out.WriteLine("Identifying " + _catalog.Icons.Count + " stored icons took "
                                      + clock.ElapsedMilliseconds + " ms.");

            Assert.That(unknown, Is.Empty, "these stored icons were not recognised at all");
            Assert.That(wrong, Is.Empty, "these stored icons were recognised as something else");
        }

        /// <summary>
        /// The same, without the comment this tool writes - which is what an icon stored by a
        /// version of this tool older than provenance looks like, and what a file pasted in
        /// from the family's own site looks like. Here the drawing has to carry it alone.
        /// </summary>
        [Test]
        public void An_icon_with_no_comment_is_recognised_by_its_drawing()
        {
            var missed = new List<string>();
            var shared = new List<string>();

            foreach (var icon in Sample(400))
            {
                var stored = SvgOutput.Build(
                    _catalog.ReadSvg(icon), IconColourMode.Theme, Color.Black, null, null);

                var identity = IconIdentifier.Identify(stored, _catalog);

                // An icon two names share cannot be told apart by its drawing, and the index
                // holds neither. That is the rule working, not a miss.
                if (identity == null) { shared.Add(icon.EntryPath); continue; }

                if (!ReferenceEquals(identity.Entry, icon)) missed.Add(icon.EntryPath);
                else Assert.That(identity.Evidence, Is.EqualTo(IconEvidence.Drawing));
            }

            Assert.That(missed, Is.Empty, "these were recognised as the wrong icon");
            TestContext.Out.WriteLine(shared.Count + " of the sample are drawings more than one name shares.");
        }

        /// <summary>
        /// Tabler ships every icon with an invisible full-canvas rectangle that the bundle
        /// strips. A file pasted in straight from Tabler therefore has one shape more than the
        /// bundled copy, and has to be recognised anyway.
        /// </summary>
        [Test]
        public void A_tabler_icon_with_its_backdrop_still_on_is_recognised()
        {
            var icon = _catalog.Icons.First(i => i.Family == IconFamily.Tabler && i.Name == "folder");
            var bundled = _catalog.ReadSvg(icon);

            var asShipped = ReplaceFirst(bundled, "<path",
                "<path stroke=\"none\" d=\"M0 0h24v24H0z\" fill=\"none\"/><path");

            var identity = IconIdentifier.Identify(asShipped, _catalog);

            Assert.That(identity, Is.Not.Null);
            Assert.That(identity.Entry, Is.SameAs(icon));
        }

        /// <summary>
        /// The class Lucide and Tabler ship on their files, which the bundle strips. It only
        /// gets a say when the drawing itself did not match - an icon the family has redrawn
        /// since this bundle was built.
        /// </summary>
        [Test]
        public void A_class_names_an_icon_whose_drawing_has_moved_on()
        {
            var redrawn =
                "<svg xmlns=\"http://www.w3.org/2000/svg\" class=\"lucide lucide-folder\" viewBox=\"0 0 24 24\">" +
                "<path d=\"M1 1h2v2h-2z\"/></svg>";

            var identity = IconIdentifier.Identify(redrawn, _catalog);

            Assert.That(identity, Is.Not.Null);
            Assert.That(identity.Evidence, Is.EqualTo(IconEvidence.Marker));
            Assert.That(identity.Entry?.Name, Is.EqualTo("folder"));
            Assert.That(identity.Entry?.Family, Is.EqualTo(IconFamily.Lucide));
        }

        [Test]
        public void A_drawing_that_is_nobody_elses_is_recognised_as_nothing()
        {
            var mine =
                "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\">" +
                "<path d=\"M3 3 L21 21 L3 21 Z\"/></svg>";

            Assert.That(IconIdentifier.Identify(mine, _catalog), Is.Null);
        }

        [Test]
        public void Nothing_at_all_is_recognised_as_nothing()
        {
            Assert.That(IconIdentifier.Identify(null, _catalog), Is.Null);
            Assert.That(IconIdentifier.Identify("", _catalog), Is.Null);
            Assert.That(IconIdentifier.Identify("not xml at all", _catalog), Is.Null);
            Assert.That(IconIdentifier.Identify("<svg xmlns=\"http://www.w3.org/2000/svg\"/>", _catalog), Is.Null);
        }

        /// <summary>
        /// A provenance comment naming an icon this bundle no longer carries still says what it
        /// is. The window can name it even though it cannot go to it in the grid.
        /// </summary>
        [Test]
        public void An_icon_this_bundle_no_longer_has_is_still_named()
        {
            var gone = new IconProvenance
            {
                Family = "Lucide",
                Weight = "regular",
                Icon = "icon_that_lucide_withdrew"
            };

            var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><!--"
                      + Provenance.Comment(gone) + "--><path d=\"M3 3 L21 21 L3 21 Z\"/></svg>";

            var identity = IconIdentifier.Identify(svg, _catalog);

            Assert.That(identity, Is.Not.Null);
            Assert.That(identity.Entry, Is.Null);
            Assert.That(identity.Described, Is.EqualTo("Icon that lucide withdrew (Lucide regular)"));
        }

        /// <summary>Every nth icon, spread across the families rather than the first N.</summary>
        private IEnumerable<IconEntry> Sample(int wanted)
        {
            var step = Math.Max(1, _catalog.Icons.Count / wanted);
            return _catalog.Icons.Where((i, index) => index % step == 0);
        }

        /// <summary>
        /// The bundled archive again, with one entry left out and optionally one rewritten on
        /// the way through. There is no second library to point a build at, so a test about a
        /// bundle that is wrong has to make one.
        /// </summary>
        private static byte[] BundleWithout(string leaveOut, Func<string, string> rewriteIndex = null)
        {
            var source = typeof(IconCatalog).Assembly.GetManifestResourceStream("Icons.Catalog.icons.zip");
            Assert.That(source, Is.Not.Null, "the library should be embedded in the assembly");

            var made = new MemoryStream();

            using (source)
            using (var reading = new ZipArchive(source, ZipArchiveMode.Read))
            using (var writing = new ZipArchive(made, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var entry in reading.Entries)
                {
                    if (entry.FullName == leaveOut) continue;

                    var copy = writing.CreateEntry(entry.FullName);
                    using (var from = entry.Open())
                    using (var to = copy.Open())
                    {
                        if (rewriteIndex != null && entry.FullName == IconCatalog.IndexEntryName)
                        {
                            using (var reader = new StreamReader(from, Encoding.UTF8))
                            using (var writer = new StreamWriter(to, new UTF8Encoding(false)))
                                writer.Write(rewriteIndex(reader.ReadToEnd()));
                        }
                        else
                        {
                            from.CopyTo(to);
                        }
                    }
                }
            }

            return made.ToArray();
        }

        private static string ReplaceFirst(string text, string find, string replacement)
        {
            var at = text.IndexOf(find, StringComparison.Ordinal);
            return at < 0 ? text : text.Substring(0, at) + replacement + text.Substring(at + find.Length);
        }
    }
}
