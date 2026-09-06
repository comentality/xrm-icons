using System.Drawing;
using Icons.Logic;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// What an SVG says about a shape somewhere other than on the shape.
    ///
    /// The library bundled in this tool states everything in attributes, so none of this was
    /// reachable until the tool drew an icon an environment already had. Microsoft's own web
    /// resource icons are drawn the other way: a full-canvas rect, cancelled by a class in a
    /// style block. Read only the attributes and that rect takes the caller's colour and paints
    /// a solid square over the drawing, which is exactly what a real app's navigation came out
    /// as - twenty of twenty-two icons, black squares.
    /// </summary>
    [TestFixture]
    public class StylesheetTests
    {
        /// <summary>The shape Microsoft ships: the backdrop is cancelled by a class, not an attribute.</summary>
        private const string ClassCancelled =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\">" +
            "<defs><style>.cls-1{fill:none;}</style></defs>" +
            "<rect class=\"cls-1\" width=\"24\" height=\"24\"/>" +
            "<path d=\"M10 10h4v4h-4Z\"/></svg>";

        private static int Ink(Bitmap bitmap)
        {
            var count = 0;
            for (var y = 0; y < bitmap.Height; y++)
                for (var x = 0; x < bitmap.Width; x++)
                    if (bitmap.GetPixel(x, y).A > 32) count++;
            return count;
        }

        private static int InkOf(string svg)
        {
            using (var bitmap = SvgImage.Parse(svg).Render(24, Color.Black))
                return Ink(bitmap);
        }

        [Test]
        public void A_backdrop_cancelled_by_a_class_does_not_cover_the_drawing()
        {
            var ink = InkOf(ClassCancelled);

            // The drawing is a 4x4 square in a 24x24 box. Anything near the full canvas means
            // the backdrop was painted.
            Assert.That(ink, Is.LessThan(24 * 24 / 2), "the backdrop rect was painted over the drawing");
            Assert.That(ink, Is.GreaterThan(0), "the drawing itself was lost");
        }

        [Test]
        public void A_backdrop_cancelled_by_a_style_attribute_does_not_cover_the_drawing()
        {
            var ink = InkOf(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\">" +
                "<rect style=\"fill:none\" width=\"24\" height=\"24\"/>" +
                "<path d=\"M10 10h4v4h-4Z\"/></svg>");

            Assert.That(ink, Is.LessThan(24 * 24 / 2));
            Assert.That(ink, Is.GreaterThan(0));
        }

        [Test]
        public void A_rule_beats_the_attribute_it_contradicts()
        {
            // The cascade: a presentation attribute is the weakest thing an element can say.
            // Measured against the same drawing with no backdrop at all: equal means the rect
            // painted nothing, which is what the class asked for and the attribute did not.
            var withBackdrop = InkOf(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\">" +
                "<style>.hidden{fill:none;}</style>" +
                "<rect class=\"hidden\" fill=\"currentColor\" width=\"24\" height=\"24\"/>" +
                "<path d=\"M10 10h4v4h-4Z\"/></svg>");

            var alone = InkOf(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\">" +
                "<path d=\"M10 10h4v4h-4Z\"/></svg>");

            Assert.That(withBackdrop, Is.EqualTo(alone));
        }

        [Test]
        public void A_style_attribute_beats_the_rule_it_contradicts()
        {
            var ink = InkOf(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\">" +
                "<style>.hidden{fill:none;}</style>" +
                "<rect class=\"hidden\" style=\"fill:currentColor\" width=\"24\" height=\"24\"/></svg>");

            Assert.That(ink, Is.GreaterThan(24 * 24 / 2));
        }

        [Test]
        public void A_selector_this_reader_cannot_judge_is_left_alone()
        {
            // A descendant selector might or might not match. Obeying it where it does not
            // belong hides a drawing for no reason a reader could ever find, so it is skipped
            // and the attribute stands.
            var ink = InkOf(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\">" +
                "<style>#somewhere .hidden{fill:none;}</style>" +
                "<rect class=\"hidden\" width=\"24\" height=\"24\"/></svg>");

            Assert.That(ink, Is.GreaterThan(24 * 24 / 2));
        }

        [Test]
        public void A_comment_is_not_read_as_a_rule()
        {
            var ink = InkOf(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\">" +
                "<style>/* .hidden{fill:red;} */ .hidden{fill:none;}</style>" +
                "<rect class=\"hidden\" width=\"24\" height=\"24\"/>" +
                "<path d=\"M10 10h4v4h-4Z\"/></svg>");

            Assert.That(ink, Is.LessThan(24 * 24 / 2));
            Assert.That(ink, Is.GreaterThan(0));
        }
    }
}
