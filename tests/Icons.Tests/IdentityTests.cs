using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
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

            var clock = Stopwatch.StartNew();
            _catalog.Index();
            clock.Stop();

            TestContext.Out.WriteLine("Fingerprinting " + _catalog.Icons.Count + " icons took "
                                      + clock.ElapsedMilliseconds + " ms.");
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
        /// What building the index costs, on a catalogue nobody has indexed yet. It runs on a
        /// worker while the window is already usable, so this is not a budget - but it is the
        /// number that decides whether it can stay on a worker or has to be built at bundle
        /// time, and it should not drift without somebody noticing.
        /// </summary>
        [Test]
        public void Indexing_the_whole_library_is_worth_doing_at_run_time()
        {
            using (var fresh = IconCatalog.Load())
            {
                var clock = Stopwatch.StartNew();
                fresh.Index();
                clock.Stop();

                TestContext.Out.WriteLine("Fingerprinting " + fresh.Icons.Count + " icons took "
                                          + clock.ElapsedMilliseconds + " ms.");

                Assert.That(fresh.Indexed, Is.True);
                Assert.That(clock.ElapsedMilliseconds, Is.LessThan(30000),
                    "indexing has got slow enough to be worth moving to bundle time");
            }
        }

        /// <summary>
        /// How many of the bundled icons are a drawing that another name shares. Those cannot
        /// be recognised from the drawing alone, which is the rule working rather than a gap -
        /// but if the number ever climbs, the rule is costing more than it is worth.
        /// </summary>
        [Test]
        public void Few_drawings_are_shared_by_two_names()
        {
            var shared = 0;
            var wrong = new List<string>();

            foreach (var icon in _catalog.Icons)
            {
                var found = _catalog.Drawn(IconFingerprint.Of(_catalog.ReadSvg(icon)));

                if (found == null) shared++;
                else if (!ReferenceEquals(found, icon)) wrong.Add(icon.EntryPath + " read as " + found.EntryPath);
            }

            TestContext.Out.WriteLine(shared + " of " + _catalog.Icons.Count
                                      + " icons are a drawing more than one name shares.");

            // The index answering with the wrong icon is the failure that matters, and this is
            // the only place every one of the bundle's drawings is looked up: the icons stored
            // with a provenance comment never reach the drawing at all.
            Assert.That(wrong, Is.Empty, "the drawing index gave the wrong icon for these");

            Assert.That(shared, Is.LessThan(_catalog.Icons.Count / 10),
                "too many icons share a drawing for silence to be the right answer");
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

        private static string ReplaceFirst(string text, string find, string replacement)
        {
            var at = text.IndexOf(find, StringComparison.Ordinal);
            return at < 0 ? text : text.Substring(0, at) + replacement + text.Substring(at + find.Length);
        }
    }
}
