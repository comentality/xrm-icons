using System;
using System.Drawing;
using System.Linq;
using System.Xml.Linq;
using Icons.Catalog;
using Icons.Logic;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// The licence table and ABOUT.txt state the same facts to two different audiences - one to
    /// the page written into a customer's environment, one to whoever opens the About box - and
    /// the whole point of the page is that those facts are right. So they are checked against
    /// each other rather than trusted to stay in step by hand.
    /// </summary>
    [TestFixture]
    public class IconLicenceTests
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
        public void Every_family_is_credited_the_way_the_bundle_says()
        {
            foreach (var licence in IconLicence.All)
            {
                Assert.That(_catalog.About, Does.Contain(licence.SetName),
                    "the set's name, as ABOUT.txt writes it");
                Assert.That(_catalog.About, Does.Contain(licence.Licence),
                    licence.SetName + "'s licence");
                Assert.That(_catalog.About, Does.Contain(licence.SourceUrl),
                    licence.SetName + "'s source");

                // Lucide's notice names two copyright holders on two lines; the page says them
                // in one sentence. Each half still has to be a thing ABOUT.txt actually says.
                foreach (var holder in licence.Holder.Split(new[] { ", and " }, StringSplitOptions.None))
                    Assert.That(_catalog.About, Does.Contain(holder), licence.SetName + "'s copyright holder");
            }
        }

        [Test]
        public void Only_the_sets_whose_drawings_were_changed_say_so()
        {
            // Choosing which of Fluent's optical sizes to keep, and dropping the weight out of
            // a Phosphor file name, are not changes to a drawing. Removing an attribute is.
            Assert.That(IconLicence.For(IconFamily.Fluent).BundleChange, Is.Null);
            Assert.That(IconLicence.For(IconFamily.Phosphor).BundleChange, Is.Null);
            Assert.That(IconLicence.For(IconFamily.Lucide).BundleChange, Does.Contain("class attribute"));
            Assert.That(IconLicence.For(IconFamily.Tabler).BundleChange, Does.Contain("class attribute"));

            Assert.That(Paragraph("Lucide"), Does.Contain("class attribute"),
                "ABOUT.txt is where that wording comes from");
            Assert.That(Paragraph("Phosphor Icons"), Does.Not.Contain("class attribute"));
        }

        [Test]
        public void Exactly_the_CC_BY_sets_ask_for_a_credit_line()
        {
            // Named rather than counted. The other four ask only that their notice travel with
            // the software, and the day one of them starts asking for more is the day somebody
            // has to go and read what the tool promises in docs/clean-room.md and the README -
            // both of which say an obligation lands on the attribution page rather than on the
            // customer, and both of which were written when the answer to this was none.
            var asking = IconLicence.All.Where(l => l.RequiresNotice).Select(l => l.Family).ToList();

            Assert.That(asking, Is.EquivalentTo(new[] { IconFamily.FontAwesome, IconFamily.Solar }));

            // The two are not the same obligation by coincidence: it is the licence that asks,
            // so a set under it asks and a set not under it does not.
            foreach (var licence in IconLicence.All)
            {
                Assert.That(licence.RequiresNotice, Is.EqualTo(licence.Licence == "CC-BY-4.0"),
                            licence.SetName + " is " + licence.Licence);
            }
        }

        /// <summary>What ABOUT.txt says about one set, up to where it starts on the next.</summary>
        private string Paragraph(string setName)
        {
            var start = _catalog.About.IndexOf(setName, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "ABOUT.txt should mention " + setName);

            var next = IconLicence.All
                .Select(l => _catalog.About.IndexOf(l.SetName, StringComparison.Ordinal))
                .Where(i => i > start)
                .DefaultIfEmpty(_catalog.About.Length)
                .Min();

            return _catalog.About.Substring(start, next - start);
        }
    }

    /// <summary>
    /// The comment inside a stored icon is the only thing that survives the session that wrote
    /// it, so what matters is that it goes in, comes back out saying the same thing, and cannot
    /// be turned into an SVG the platform refuses.
    /// </summary>
    [TestFixture]
    public class ProvenanceTests
    {
        private const string Filled =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"#000\">" +
            "<path d=\"M2 2H22V22H2Z\"/></svg>";

        private const string Stroked =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"#000\">" +
            "<path d=\"M2 2H22V22H2Z\"/></svg>";

        private static IconProvenance Lucide()
        {
            return IconProvenance.For(new IconEntry(IconFamily.Lucide, "folder", "regular", IconStyle.Regular));
        }

        [Test]
        public void A_stored_icon_says_where_it_came_from()
        {
            var svg = SvgOutput.Build(Stroked, IconColourMode.Theme, Color.Black, null, Lucide());
            var read = Provenance.Find(svg);

            Assert.That(read, Is.Not.Null, "the comment should have been written");
            Assert.That(read.Family, Is.EqualTo("Lucide"));
            Assert.That(read.Weight, Is.EqualTo("regular"));
            Assert.That(read.Icon, Is.EqualTo("folder"));
            Assert.That(read.Set, Is.EqualTo("Lucide"));
            Assert.That(read.Holder, Is.EqualTo("Lucide Icons and Contributors, and Cole Bemis"));
            Assert.That(read.Licence, Is.EqualTo("ISC"));
            Assert.That(read.Source, Is.EqualTo("https://lucide.dev"));
        }

        [Test]
        public void The_comment_rides_inside_the_drawing_rather_than_above_it()
        {
            // So that anything taking the element on its own - a preview, a copy into another
            // file - takes the credit with it.
            var svg = SvgOutput.Build(Filled, IconColourMode.Theme, Color.Black, null, Lucide());
            var root = XDocument.Parse(svg).Root;

            Assert.That(root.FirstNode, Is.TypeOf<XComment>());
        }

        [Test]
        public void What_was_done_to_the_drawing_is_listed_in_the_order_it_happened()
        {
            var svg = SvgOutput.Build(Stroked, IconColourMode.Theme, Color.Black, 32, Lucide());
            var read = Provenance.Find(svg);

            Assert.That(read.Changes, Is.EqualTo(new[]
            {
                "the class attribute was removed from every element",
                "the stroke was set to currentColor",
                "the declared size was set to 32 by 32"
            }));
        }

        [Test]
        public void A_filled_icon_records_the_fill_and_a_fixed_colour_records_the_colour()
        {
            var svg = SvgOutput.Build(Filled, IconColourMode.Fixed, Color.FromArgb(0x24, 0x24, 0x24), null,
                IconProvenance.For(new IconEntry(IconFamily.Fluent, "folder", "regular", IconStyle.Regular)));

            Assert.That(Provenance.Find(svg).Changes, Is.EqualTo(new[] { "the fill was set to #242424" }));
        }

        [Test]
        public void The_record_handed_in_is_left_alone()
        {
            // It describes the icon, not one particular write of it: the same record is used
            // again the next time, and a change list that grew each time would be a lie.
            var provenance = Lucide();
            SvgOutput.Build(Filled, IconColourMode.Theme, Color.Black, 32, provenance);

            Assert.That(provenance.Changes.Count, Is.EqualTo(1));
        }

        [Test]
        public void An_icon_written_without_one_carries_no_comment()
        {
            var svg = SvgOutput.Build(Filled, IconColourMode.Theme, Color.Black);

            Assert.That(Provenance.Find(svg), Is.Null);
            Assert.That(svg, Does.Not.Contain("<!--"));
        }

        [Test]
        public void A_notice_that_would_break_the_comment_is_tamed_rather_than_dropped()
        {
            // An XML comment may hold neither a double hyphen nor a quote, and a notice that
            // turned the icon into a file the platform refuses would be worse than no notice.
            var provenance = Lucide();
            provenance.Holder = "Someone -- \"Nobody\"\r\n  Else";

            var svg = SvgOutput.Build(Filled, IconColourMode.Theme, Color.Black, null, provenance);

            Assert.That(() => XDocument.Parse(svg), Throws.Nothing);
            Assert.That(Provenance.Find(svg).Holder, Is.EqualTo("Someone - 'Nobody' Else"));
        }

        [Test]
        public void Somebody_elses_comment_is_not_read_as_provenance()
        {
            // Lucide ships every file with its own licence comment already in it.
            Assert.That(Provenance.Parse(" @license lucide-static v1.39.0 - ISC "), Is.Null);
            Assert.That(Provenance.Find("<svg><!-- hand drawn --><path/></svg>"), Is.Null);
        }
    }

    [TestFixture]
    public class AttributionPageTests
    {
        private static readonly DateTime Written = new DateTime(2026, 9, 10);

        private static StoredIcon Icon(string name, IconFamily family, string icon = "folder")
        {
            var provenance = IconProvenance.For(new IconEntry(family, icon, "regular", IconStyle.Regular));
            provenance.Changes.Add("the fill was set to currentColor");
            return new StoredIcon(name, provenance);
        }

        [Test]
        public void The_page_goes_in_the_folder_the_icons_are_in()
        {
            Assert.That(AttributionPage.NameFor("new_/icons/account.svg"), Is.EqualTo("new_/icons/ATTRIBUTION.html"));
            Assert.That(AttributionPage.NameFor("contoso_/img/tables/account.svg"),
                Is.EqualTo("contoso_/img/tables/ATTRIBUTION.html"));
        }

        [Test]
        public void A_name_with_no_folder_in_it_falls_back_to_the_publisher_prefix()
        {
            // A bare ATTRIBUTION.html is not a name Dataverse would take, and scoping to the
            // prefix keeps the page about one publisher's icons.
            Assert.That(AttributionPage.NameFor("new_account_icon.svg"), Is.EqualTo("new_ATTRIBUTION.html"));
            Assert.That(Icons.Logic.WebResourceName.Validate(AttributionPage.NameFor("new_account_icon.svg")),
                Is.Null, "and the platform would take it");
        }

        [Test]
        public void The_page_this_tool_writes_is_a_name_the_platform_accepts()
        {
            Assert.That(Icons.Logic.WebResourceName.Validate("new_/icons/" + AttributionPage.FileName), Is.Null);
        }

        [Test]
        public void Every_icon_is_listed_under_the_set_that_drew_it()
        {
            var page = AttributionPage.Build(new[]
            {
                Icon("new_/icons/account.svg", IconFamily.Lucide),
                Icon("new_/icons/contact.svg", IconFamily.Lucide, "user"),
                Icon("new_/icons/case.svg", IconFamily.Tabler, "headset")
            }, Written);

            Assert.That(page, Does.Contain("new_/icons/account.svg"));
            Assert.That(page, Does.Contain("new_/icons/contact.svg"));
            Assert.That(page, Does.Contain("new_/icons/case.svg"));

            Assert.That(page, Does.Contain("Lucide Icons and Contributors, and Cole Bemis"));
            Assert.That(page, Does.Contain("Paweł Kuna"));
            Assert.That(page, Does.Contain("https://spdx.org/licenses/ISC.html"));

            Assert.That(Occurrences(page, "<h2>"), Is.EqualTo(2), "one heading per set, not one per icon");
        }

        [Test]
        public void What_was_done_to_each_icon_is_on_the_page()
        {
            // CC BY asks for changes to be indicated, and this is where that lands.
            var page = AttributionPage.Build(new[] { Icon("new_/icons/account.svg", IconFamily.Lucide) }, Written);

            Assert.That(page, Does.Contain("The class attribute was removed from every element; " +
                                           "the fill was set to currentColor."));
        }

        [Test]
        public void An_icon_that_says_nothing_about_itself_is_left_out_rather_than_guessed_at()
        {
            var read = AttributionPage.Read(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>(
                    "new_/icons/account.svg",
                    SvgOutput.Build("<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M0 0H1V1Z\"/></svg>",
                        IconColourMode.Theme, Color.Black, null,
                        IconProvenance.For(new IconEntry(IconFamily.Fluent, "folder", "regular", IconStyle.Regular)))),
                new System.Collections.Generic.KeyValuePair<string, string>(
                    "new_/icons/handmade.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M0 0H1V1Z\"/></svg>"),
                new System.Collections.Generic.KeyValuePair<string, string>("new_/icons/broken.svg", "not xml at all")
            });

            Assert.That(read.Select(i => i.WebResourceName), Is.EqualTo(new[] { "new_/icons/account.svg" }));
        }

        [Test]
        public void A_folder_with_nothing_creditable_in_it_still_makes_a_page()
        {
            // Rather than leaving whatever the last one said standing.
            var page = AttributionPage.Build(new StoredIcon[0], Written);

            Assert.That(page, Does.Contain("No icons written by this tool are stored here."));
            Assert.That(() => XDocument.Parse(page.Replace("<!DOCTYPE html>", "")), Throws.Nothing);
        }

        [Test]
        public void A_name_with_markup_in_it_cannot_break_the_page()
        {
            var provenance = IconProvenance.For(new IconEntry(IconFamily.Fluent, "folder", "regular", IconStyle.Regular));
            provenance.Holder = "<script>alert(1)</script>";

            var page = AttributionPage.Build(new[] { new StoredIcon("new_/icons/a&b.svg", provenance) }, Written);

            Assert.That(page, Does.Not.Contain("<script>"));
            Assert.That(page, Does.Contain("&lt;script&gt;"));
            Assert.That(page, Does.Contain("new_/icons/a&amp;b.svg"));
        }

        [Test]
        public void The_same_environment_twice_produces_the_same_page()
        {
            var icons = new[]
            {
                Icon("new_/icons/contact.svg", IconFamily.Lucide, "user"),
                Icon("new_/icons/account.svg", IconFamily.Lucide)
            };

            Assert.That(AttributionPage.Build(icons, Written),
                Is.EqualTo(AttributionPage.Build(Enumerable.Reverse(icons).ToArray(), Written)));
        }

        private static int Occurrences(string text, string needle)
        {
            var count = 0;
            for (var i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0;
                 i = text.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
                count++;

            return count;
        }
    }
}
