using System.Drawing;
using System.Linq;
using System.Xml.Linq;
using Icons.Logic;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// The half of the library that is drawn as lines rather than as shapes.
    ///
    /// Lucide and Tabler state an icon as a path of a given stroke width with no fill; Fluent
    /// and Phosphor state the same drawing as a region to fill. Reading the second kind as the
    /// first gives a solid blob where the drawing was, and reading the first kind's "none" as
    /// "the caller's colour" gives a black square over the top of it. Both were true of this
    /// tool before there was more than one family in it, so both are pinned here.
    /// </summary>
    [TestFixture]
    public class StrokeTests
    {
        /// <summary>A Lucide icon, in the shape they all take: fill none, stroke on the root.</summary>
        private const string Stroked =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\" " +
            "fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" " +
            "stroke-linejoin=\"round\"><path d=\"M4 6h16v12H4Z\"/></svg>";

        /// <summary>What every Tabler icon opens with: a full-canvas rectangle painted with nothing.</summary>
        private const string WithBackdrop =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\" " +
            "fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\">" +
            "<path stroke=\"none\" d=\"M0 0h24v24H0z\" fill=\"none\"/>" +
            "<path d=\"M6 12h12\"/></svg>";

        private static int Ink(Bitmap bitmap)
        {
            var count = 0;
            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                    if (bitmap.GetPixel(x, y).A > 32) count++;
            return count;
        }

        [Test]
        public void An_outline_icon_is_drawn_as_an_outline_not_a_blob()
        {
            using (var bitmap = SvgImage.Parse(Stroked).Render(64, Color.Black))
            {
                // Something was drawn.
                Assert.That(Ink(bitmap), Is.GreaterThan(100), "the stroke left no ink at all");

                // And the middle of the box is still empty, which is the whole difference
                // between drawing the outline and filling the shape.
                Assert.That(bitmap.GetPixel(32, 32).A, Is.LessThan(32),
                    "the inside of an unfilled shape was painted, so it was filled rather than stroked");
            }
        }

        [Test]
        public void A_fill_of_none_paints_nothing_at_all()
        {
            using (var bitmap = SvgImage.Parse(WithBackdrop).Render(64, Color.Black))
            {
                // The backdrop covers the whole canvas. If none were read as a colour, every
                // corner would be black and the icon under it invisible.
                Assert.That(bitmap.GetPixel(2, 2).A, Is.LessThan(32), "the backdrop rectangle was painted");
                Assert.That(bitmap.GetPixel(61, 61).A, Is.LessThan(32), "the backdrop rectangle was painted");
                Assert.That(bitmap.GetPixel(32, 32).A, Is.GreaterThan(32), "the line itself was not drawn");
            }
        }

        [Test]
        public void An_open_path_is_not_closed_behind_the_pen()
        {
            // Two sides of a triangle. Closing the figure would draw the third, which is what
            // happens to every cart handle and headset band if the fill rule is applied to a
            // stroke.
            var open =
                "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" " +
                "stroke=\"currentColor\" stroke-width=\"2\"><path d=\"M4 20L12 4L20 20\"/></svg>";

            using (var bitmap = SvgImage.Parse(open).Render(64, Color.Black))
            {
                // The base of the triangle. It is only there if the figure was closed.
                var baseline = Enumerable.Range(16, 32).Count(x => bitmap.GetPixel(x, 55).A > 32);
                Assert.That(baseline, Is.LessThan(4), "the open path was closed, drawing a line that is not in it");
            }
        }

        [Test]
        public void The_primitives_lucide_uses_are_drawn_rather_than_reported()
        {
            var shapes =
                "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" " +
                "stroke=\"currentColor\" stroke-width=\"2\">" +
                "<circle cx=\"6\" cy=\"6\" r=\"3\"/><rect x=\"14\" y=\"3\" width=\"7\" height=\"6\" rx=\"2\"/>" +
                "<line x1=\"3\" y1=\"18\" x2=\"21\" y2=\"18\"/><polyline points=\"3,14 8,10 13,14\"/>" +
                "<polygon points=\"16,16 21,16 18,21\"/><ellipse cx=\"6\" cy=\"22\" rx=\"3\" ry=\"1.5\"/>" +
                "</svg>";

            var image = SvgImage.Parse(shapes);

            Assert.That(image.Unsupported, Is.Empty,
                "these are the elements Lucide is drawn with; reporting them would warn on 881 icons");

            using (var bitmap = image.Render(64, Color.Black))
                Assert.That(Ink(bitmap), Is.GreaterThan(200));
        }

        [Test]
        public void A_stroked_icon_is_recoloured_on_its_stroke()
        {
            var stored = SvgOutput.Build(Stroked, IconColourMode.Theme, Color.Black);
            var root = XDocument.Parse(stored).Root;

            Assert.That((string)root.Attribute("stroke"), Is.EqualTo("currentColor"));
            Assert.That((string)root.Attribute("fill"), Is.EqualTo("none"),
                "setting fill on an outline icon fills it in, which is not a recolour but a different picture");
        }

        [Test]
        public void A_filled_icon_is_still_recoloured_on_its_fill()
        {
            var filled =
                "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"currentColor\">" +
                "<path d=\"M2 2H22V22H2Z\"/></svg>";

            var root = XDocument.Parse(SvgOutput.Build(filled, IconColourMode.Fixed, Color.Red)).Root;

            Assert.That((string)root.Attribute("fill"), Is.EqualTo("#FF0000"));
            Assert.That(root.Attribute("stroke"), Is.Null);
        }
    }
}
