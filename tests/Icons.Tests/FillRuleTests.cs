using System.Drawing;
using Icons.Logic;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// Which rule decides what is inside a shape.
    ///
    /// SVG fills nonzero unless told otherwise, and a set drawn that way cuts a hole by winding
    /// the inner ring the other way. A set drawn for even-odd does not bother: both rings go the
    /// same way round and the rule does the cutting. Fill the second kind nonzero and the hole
    /// fills in - a calendar with no grid, a building with no windows - and nothing errors,
    /// because a solid blob is a perfectly valid drawing of nothing in particular.
    ///
    /// The two paths here are the same two rings. Only the rule differs.
    /// </summary>
    [TestFixture]
    public class FillRuleTests
    {
        /// <summary>Two circles, same winding, one inside the other.</summary>
        private const string Rings =
            "M12 2A10 10 0 1 0 12 22A10 10 0 1 0 12 2Z" +
            "M12 7A5 5 0 1 0 12 17A5 5 0 1 0 12 7Z";

        private static string Svg(string rule)
        {
            var says = rule == null ? "" : " fill-rule=\"" + rule + "\"";
            return "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\">" +
                   "<path" + says + " d=\"" + Rings + "\"/></svg>";
        }

        /// <summary>Whether the middle of the drawing was painted.</summary>
        private static bool CentreIsInk(string svg)
        {
            using (var bitmap = SvgImage.Parse(svg).Render(48, Color.Black))
                return bitmap.GetPixel(24, 24).A > 32;
        }

        [Test]
        public void Without_a_rule_the_hole_fills_in()
        {
            // Not a complaint about GDI+ - this is what nonzero means for two rings wound the
            // same way, and it is what the bundle's other five families depend on not changing.
            Assert.That(CentreIsInk(Svg(null)), Is.True);
            Assert.That(CentreIsInk(Svg("nonzero")), Is.True);
        }

        [Test]
        public void Even_odd_leaves_the_hole()
        {
            Assert.That(CentreIsInk(Svg("evenodd")), Is.False,
                        "a shape that asks for even-odd is asking for this hole");
        }

        [Test]
        public void The_rule_is_inherited_from_the_group_the_shape_is_in()
        {
            // Where a set that draws this way usually says it: once, on the group, rather than
            // on each of the shapes inside it.
            var grouped =
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\">" +
                "<g fill-rule=\"evenodd\"><path d=\"" + Rings + "\"/></g></svg>";

            Assert.That(CentreIsInk(grouped), Is.False);
        }

        [Test]
        public void A_shape_can_take_the_rule_back()
        {
            var overridden =
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\">" +
                "<g fill-rule=\"evenodd\"><path fill-rule=\"nonzero\" d=\"" + Rings + "\"/></g></svg>";

            Assert.That(CentreIsInk(overridden), Is.True);
        }

        [Test]
        public void Something_that_is_not_a_rule_at_all_changes_nothing()
        {
            Assert.That(CentreIsInk(Svg("inherit")), Is.True);
            Assert.That(CentreIsInk(Svg("")), Is.True);
        }
    }
}
