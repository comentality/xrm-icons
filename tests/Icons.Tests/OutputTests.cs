using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Icons.Logic;
using NUnit.Framework;

namespace Icons.Tests
{
    [TestFixture]
    public class SvgOutputTests
    {
        private const string Plain =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\">" +
            "<path d=\"M2 2H22V22H2Z\"/></svg>";

        private static XElement Root(string svg)
        {
            return XDocument.Parse(svg).Root;
        }

        [Test]
        public void Theme_colouring_defers_to_the_app()
        {
            // Microsoft's guidance for a table icon: no hard-coded colour, so the theme decides
            // and the icon stays legible in dark mode.
            var result = SvgOutput.Build(Plain, IconColourMode.Theme, Color.Red);

            Assert.That((string)Root(result).Attribute("fill"), Is.EqualTo("currentColor"));
        }

        [Test]
        public void A_fixed_colour_is_written_as_hex()
        {
            var result = SvgOutput.Build(Plain, IconColourMode.Fixed, Color.FromArgb(0x10, 0x9A, 0xF0));

            Assert.That((string)Root(result).Attribute("fill"), Is.EqualTo("#109AF0"));
        }

        [Test]
        public void The_viewBox_is_never_touched()
        {
            // Rewriting the viewBox would crop the drawing rather than resize it.
            var result = SvgOutput.Build(Plain, IconColourMode.Theme, Color.Black, sizePixels: 48);

            Assert.That((string)Root(result).Attribute("viewBox"), Is.EqualTo("0 0 24 24"));
            Assert.That((string)Root(result).Attribute("width"), Is.EqualTo("48"));
            Assert.That((string)Root(result).Attribute("height"), Is.EqualTo("48"));
        }

        [Test]
        public void Leaving_the_size_alone_leaves_it_alone()
        {
            var result = SvgOutput.Build(Plain, IconColourMode.Theme, Color.Black);

            Assert.That((string)Root(result).Attribute("width"), Is.EqualTo("24"));
        }

        [Test]
        public void An_existing_fill_is_replaced_not_duplicated()
        {
            var withFill = Plain.Replace("<svg ", "<svg fill=\"#FF0000\" ");
            var result = SvgOutput.Build(withFill, IconColourMode.Theme, Color.Black);

            Assert.That(Root(result).Attributes("fill").Count(), Is.EqualTo(1));
            Assert.That((string)Root(result).Attribute("fill"), Is.EqualTo("currentColor"));
        }

        [Test]
        public void A_missing_namespace_is_added()
        {
            // A web resource without the SVG namespace renders as nothing in a browser.
            var bare = "<svg width=\"24\" height=\"24\" viewBox=\"0 0 24 24\"><path d=\"M0 0H24V24H0Z\"/></svg>";
            var result = SvgOutput.Build(bare, IconColourMode.Theme, Color.Black);

            Assert.That(Root(result).Name.Namespace.NamespaceName, Is.EqualTo("http://www.w3.org/2000/svg"));
            Assert.That(Root(result).Descendants().First().Name.Namespace.NamespaceName,
                Is.EqualTo("http://www.w3.org/2000/svg"), "children need it too, not just the root");
        }

        [Test]
        public void There_is_no_xml_declaration_to_trip_up_the_browser()
        {
            var result = SvgOutput.Build(Plain, IconColourMode.Theme, Color.Black);

            Assert.That(result, Does.StartWith("<svg"));
        }

        [Test]
        public void Malformed_input_is_reported_as_such()
        {
            Assert.That(() => SvgOutput.Build("<svg><path d=", IconColourMode.Theme, Color.Black),
                Throws.TypeOf<FormatException>());
            Assert.That(() => SvgOutput.Build("<html/>", IconColourMode.Theme, Color.Black),
                Throws.TypeOf<FormatException>());
        }

        [Test]
        public void A_non_positive_size_is_refused()
        {
            Assert.That(() => SvgOutput.Build(Plain, IconColourMode.Theme, Color.Black, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void A_clean_icon_has_nothing_to_warn_about()
        {
            Assert.That(SvgOutput.Inspect(Plain), Is.Empty);
        }

        [Test]
        public void A_missing_viewBox_is_warned_about()
        {
            var noViewBox = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\"><path d=\"M0 0H1V1H0Z\"/></svg>";

            Assert.That(SvgOutput.Inspect(noViewBox).Any(w => w.Contains("viewBox")), Is.True);
        }

        [Test]
        public void Baked_colours_are_warned_about()
        {
            var coloured = Plain.Replace("<path ", "<path fill=\"#FF0000\" ");

            Assert.That(SvgOutput.Inspect(coloured).Any(w => w.Contains("own colour")), Is.True);
        }

        [Test]
        public void CurrentColor_and_none_are_not_treated_as_baked_colours()
        {
            var deferring = Plain.Replace("<path ", "<path fill=\"currentColor\" ");

            Assert.That(SvgOutput.Inspect(deferring).Any(w => w.Contains("own colour")), Is.False);
        }

        [Test]
        public void Stylesheets_and_classes_are_warned_about()
        {
            var styled = Plain.Replace("<path ", "<path class=\"a\" ");

            Assert.That(SvgOutput.Inspect(styled).Any(w => w.Contains("stylesheet")), Is.True);
        }

        [Test]
        public void Base64_round_trips_as_utf8_without_a_byte_order_mark()
        {
            // A BOM at the front of an SVG web resource is a rendering bug in some browsers.
            var encoded = SvgOutput.ToBase64(Plain);
            var bytes = Convert.FromBase64String(encoded);

            Assert.That(bytes[0], Is.EqualTo((byte)'<'));
            Assert.That(new UTF8Encoding(false).GetString(bytes), Is.EqualTo(Plain));
        }
    }
}
