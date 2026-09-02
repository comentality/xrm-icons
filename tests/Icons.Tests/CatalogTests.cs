using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using Icons.Catalog;
using Icons.Logic;
using NUnit.Framework;

namespace Icons.Tests
{
    [TestFixture]
    public class CatalogTests
    {
        private IconCatalog _catalog;

        [OneTimeSetUp]
        public void LoadCatalog()
        {
            _catalog = IconCatalog.Load();
        }

        [OneTimeTearDown]
        public void DisposeCatalog()
        {
            _catalog?.Dispose();
        }

        [Test]
        public void The_library_is_compiled_into_the_assembly()
        {
            Assert.That(_catalog.Icons.Count, Is.GreaterThan(4000),
                "the bundled library should hold thousands of icons; run tools\\build-catalog.ps1");
        }

        [Test]
        public void Both_weights_are_present()
        {
            Assert.That(_catalog.Icons.Any(i => i.Style == IconStyle.Regular), Is.True);
            Assert.That(_catalog.Icons.Any(i => i.Style == IconStyle.Filled), Is.True);
        }

        [Test]
        public void The_library_says_where_it_came_from()
        {
            Assert.That(_catalog.About, Does.Contain("MIT"));
            Assert.That(_catalog.About, Does.Contain("Fluent"));
        }

        [Test]
        public void Names_are_readable_and_searchable()
        {
            var person = _catalog.Icons.First(i => i.Family == IconFamily.Fluent && i.Name == "person" && i.Style == IconStyle.Regular);

            Assert.That(person.DisplayName, Is.EqualTo("Person"));
            Assert.That(person.EntryPath, Is.EqualTo("fluent/regular/person.svg"));
            Assert.That(person.Words, Is.EqualTo(new[] { "person" }));
        }

        [Test]
        public void Multi_word_names_split_into_words()
        {
            var icon = new IconEntry("building_bank_toolbox", IconStyle.Filled);

            Assert.That(icon.DisplayName, Is.EqualTo("Building bank toolbox"));
            Assert.That(icon.Words, Is.EqualTo(new[] { "building", "bank", "toolbox" }));
            Assert.That(icon.EntryPath, Is.EqualTo("fluent/filled/building_bank_toolbox.svg"));
        }

        [Test]
        public void An_icon_reads_back_as_an_svg()
        {
            var icon = _catalog.Icons.First(i => i.Name == "person");
            var svg = _catalog.ReadSvg(icon);

            Assert.That(svg, Does.StartWith("<svg"));
            Assert.That(svg, Does.Contain("viewBox"));
        }

        /// <summary>
        /// The one that matters. The path parser was written from the specification rather than
        /// taken from a library, so the only honest evidence it is right is that every icon the
        /// tool can offer actually draws - and draws something, not an empty square.
        /// </summary>
        [Test]
        public void Every_icon_in_the_library_parses_and_draws()
        {
            var failures = new List<string>();
            var blank = new List<string>();

            foreach (var icon in _catalog.Icons)
            {
                try
                {
                    var image = SvgImage.Parse(_catalog.ReadSvg(icon));

                    if (image.Unsupported.Count > 0)
                    {
                        failures.Add(icon.EntryPath + ": unsupported " + string.Join(", ", image.Unsupported));
                        continue;
                    }

                    using (var bitmap = image.Render(RenderSize, Color.Black))
                    {
                        if (InkPixels(bitmap) < MinimumInk)
                            blank.Add(icon.EntryPath);
                    }
                }
                catch (Exception ex)
                {
                    failures.Add(icon.EntryPath + ": " + ex.GetType().Name + " " + ex.Message);
                }
            }

            Assert.That(failures, Is.Empty, "icons that would not draw:\n" + string.Join("\n", failures.Take(20)));
            Assert.That(blank, Is.Empty, "icons that drew nothing:\n" + string.Join("\n", blank.Take(20)));
        }

        [Test]
        public void Every_icon_survives_the_rewrite_it_is_stored_with()
        {
            var failures = new List<string>();

            foreach (var icon in _catalog.Icons)
            {
                try
                {
                    var stored = SvgOutput.Build(_catalog.ReadSvg(icon), IconColourMode.Theme, Color.Black);

                    // What comes out has to be an SVG that still draws, or the table gets an
                    // icon that renders as nothing in the app and nowhere else complains.
                    var image = SvgImage.Parse(stored);
                    using (var bitmap = image.Render(RenderSize, Color.Black))
                    {
                        if (InkPixels(bitmap) < MinimumInk) failures.Add(icon.EntryPath + ": blank after rewrite");
                    }
                }
                catch (Exception ex)
                {
                    failures.Add(icon.EntryPath + ": " + ex.GetType().Name + " " + ex.Message);
                }
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(20)));
        }

        [Test]
        public void The_handful_of_icons_that_paint_themselves_are_reported_not_hidden()
        {
            // A few icons - the pride flags - carry their own colours on purpose. They are
            // legitimate choices, so the tool warns rather than dropping them, and this pins
            // that the detection actually finds them.
            var flag = _catalog.Icons.FirstOrDefault(i => i.Name.StartsWith("flag_pride") && i.Style == IconStyle.Filled);
            Assume.That(flag, Is.Not.Null, "the library no longer carries a multicoloured icon to check");

            var warnings = SvgOutput.Inspect(_catalog.ReadSvg(flag));

            Assert.That(warnings.Any(w => w.Contains("own colour")), Is.True,
                "an icon with baked colours should warn that recolouring will not take");
        }

        private const int RenderSize = 64;

        /// <summary>
        /// The least ink a real icon can leave. Not a fraction of the canvas: cellular_data_5
        /// is one 1.5-by-3 dash in a 24-unit box, which is under one percent of it and entirely
        /// correct. Eight pixels at this size is far below that icon's thirty-odd and far above
        /// the zero that a path which failed to draw would leave.
        /// </summary>
        private const int MinimumInk = 8;

        /// <summary>How many pixels got painted. Guards against a silently empty render.</summary>
        private static int InkPixels(Bitmap bitmap)
        {
            var data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                var bytes = new byte[data.Stride * data.Height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

                var painted = 0;
                for (var i = 3; i < bytes.Length; i += 4)
                    if (bytes[i] > 32) painted++;

                return painted;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }
}
