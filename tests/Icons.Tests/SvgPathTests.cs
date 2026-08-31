using System;
using System.Drawing;
using System.Linq;
using Icons.Logic;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// The path parser is the one piece of this tool that reimplements a specification rather
    /// than calling something that already does. These pin the parts of that specification the
    /// bundled icons actually lean on - minified numbers, undelimited arc flags, the smooth
    /// curve forms - and the arithmetic of the arc conversion, which is where a mistake shows
    /// up as a shape that is subtly the wrong one rather than as an exception.
    /// </summary>
    [TestFixture]
    public class SvgPathTests
    {
        private static RectangleF Bounds(string d)
        {
            using (var path = SvgPath.Parse(d))
                return path.GetBounds();
        }

        [Test]
        public void A_triangle_closes_and_bounds_itself()
        {
            var bounds = Bounds("M0 0 L10 0 L10 10 Z");

            Assert.That(bounds.Left, Is.EqualTo(0).Within(0.01));
            Assert.That(bounds.Top, Is.EqualTo(0).Within(0.01));
            Assert.That(bounds.Right, Is.EqualTo(10).Within(0.01));
            Assert.That(bounds.Bottom, Is.EqualTo(10).Within(0.01));
        }

        [Test]
        public void Relative_commands_accumulate_from_the_current_point()
        {
            var absolute = Bounds("M5 5 L15 5 L15 15 Z");
            var relative = Bounds("m5 5 l10 0 l0 10 z");

            Assert.That(relative.Left, Is.EqualTo(absolute.Left).Within(0.01));
            Assert.That(relative.Right, Is.EqualTo(absolute.Right).Within(0.01));
            Assert.That(relative.Bottom, Is.EqualTo(absolute.Bottom).Within(0.01));
        }

        [Test]
        public void Horizontal_and_vertical_shorthands_move_one_axis_only()
        {
            var bounds = Bounds("M0 0 H10 V10 H0 Z");

            Assert.That(bounds.Width, Is.EqualTo(10).Within(0.01));
            Assert.That(bounds.Height, Is.EqualTo(10).Within(0.01));
        }

        [Test]
        public void A_repeated_moveto_draws_lines_after_the_first_point()
        {
            // Per the grammar, "M0 0 10 0 10 10" is a move and then two lines, not three moves.
            var bounds = Bounds("M0 0 10 0 10 10");

            Assert.That(bounds.Right, Is.EqualTo(10).Within(0.01));
            Assert.That(bounds.Bottom, Is.EqualTo(10).Within(0.01));
        }

        [Test]
        public void Numbers_that_run_together_scan_as_separate_numbers()
        {
            // ".5.5" is 0.5 then 0.5. A naive scanner reads one number and then fails.
            var bounds = Bounds("M0 0 l.5.5");

            Assert.That(bounds.Right, Is.EqualTo(0.5).Within(0.01));
            Assert.That(bounds.Bottom, Is.EqualTo(0.5).Within(0.01));
        }

        [Test]
        public void A_negative_sign_separates_numbers_without_a_delimiter()
        {
            var bounds = Bounds("M10 10l-10-10");

            Assert.That(bounds.Left, Is.EqualTo(0).Within(0.01));
            Assert.That(bounds.Top, Is.EqualTo(0).Within(0.01));
        }

        [Test]
        public void Exponent_notation_is_a_number_not_a_command()
        {
            var bounds = Bounds("M0 0 L1e1 1E1");

            Assert.That(bounds.Right, Is.EqualTo(10).Within(0.01));
            Assert.That(bounds.Bottom, Is.EqualTo(10).Within(0.01));
        }

        [Test]
        public void A_letter_after_a_number_is_the_next_command_not_an_exponent()
        {
            // "0e" looks like the start of an exponent, but nothing follows the e, so the
            // scanner has to give the character back and let it be read as a command. There is
            // no e command, so it is rejected as one - which is the proof it was not swallowed
            // as part of the number.
            Assert.That(() => SvgPath.Parse("M0 0 L10 0e"),
                Throws.TypeOf<FormatException>().And.Message.Contains("command"));
        }

        [Test]
        public void Arc_flags_are_single_characters_and_need_no_delimiter()
        {
            // This is Fluent's own way of writing a circle of radius 10 centred at (12, 12),
            // and the flags run straight into the coordinate that follows: "1 0-20 0".
            var bounds = Bounds("M22 12a10 10 0 1 0-20 0 10 10 0 0 0 20 0");

            Assert.That(bounds.Left, Is.EqualTo(2).Within(0.05));
            Assert.That(bounds.Top, Is.EqualTo(2).Within(0.05));
            Assert.That(bounds.Right, Is.EqualTo(22).Within(0.05));
            Assert.That(bounds.Bottom, Is.EqualTo(22).Within(0.05));
        }

        [Test]
        public void A_half_circle_arc_bulges_the_way_the_sweep_flag_says()
        {
            // Same endpoints, opposite sweep: one arcs above the line, the other below. Note
            // which is which - SVG's y axis points down, so the "positive angle" direction the
            // sweep flag names comes out looking clockwise, and sweep 1 arcs upward here.
            var sweepOne = Bounds("M0 0 A5 5 0 0 1 10 0");
            var sweepZero = Bounds("M0 0 A5 5 0 0 0 10 0");

            Assert.That(sweepOne.Top, Is.EqualTo(-5).Within(0.05), "sweep 1 should bulge upward");
            Assert.That(sweepOne.Bottom, Is.EqualTo(0).Within(0.05));

            Assert.That(sweepZero.Bottom, Is.EqualTo(5).Within(0.05), "sweep 0 should bulge downward");
            Assert.That(sweepZero.Top, Is.EqualTo(0).Within(0.05));
        }

        [Test]
        public void The_large_arc_flag_picks_the_long_way_round()
        {
            // The radius has to exceed half the chord for there to be a long way round at all:
            // at exactly half, both arcs are the same semicircle.
            var small = Bounds("M0 0 A8 8 0 0 1 10 0");
            var large = Bounds("M0 0 A8 8 0 1 1 10 0");

            Assert.That(small.Height, Is.LessThan(8), "the short way stays inside the radius");
            Assert.That(large.Height, Is.GreaterThan(8), "the long way passes the far side of the circle");
            Assert.That(large.Height, Is.EqualTo(8 + Math.Sqrt(8 * 8 - 5 * 5)).Within(0.05));
        }

        [Test]
        public void Radii_too_small_to_span_the_endpoints_are_scaled_up_to_fit()
        {
            // The specification says to grow the radii until the arc just reaches, rather than
            // to give up. With rx=1 this has to become a semicircle of radius 5.
            var bounds = Bounds("M0 0 A1 1 0 0 1 10 0");

            Assert.That(bounds.Width, Is.EqualTo(10).Within(0.05));
            Assert.That(bounds.Height, Is.EqualTo(5).Within(0.05));
        }

        [Test]
        public void A_zero_radius_arc_is_a_straight_line()
        {
            var bounds = Bounds("M0 0 A0 0 0 0 1 10 0");

            Assert.That(bounds.Width, Is.EqualTo(10).Within(0.01));
            Assert.That(bounds.Height, Is.EqualTo(0).Within(0.01));
        }

        [Test]
        public void An_arc_that_goes_nowhere_draws_nothing()
        {
            Assert.That(() => Bounds("M5 5 A5 5 0 1 1 5 5"), Throws.Nothing);
        }

        [Test]
        public void A_rotated_ellipse_arc_is_not_axis_aligned()
        {
            // GraphicsPath.AddArc cannot express this, which is why the conversion is by hand.
            var upright = Bounds("M0 0 A10 5 0 0 1 20 0");
            var rotated = Bounds("M0 0 A10 5 45 0 1 20 0");

            Assert.That(rotated.Height, Is.Not.EqualTo(upright.Height).Within(0.1));
        }

        [Test]
        public void The_smooth_cubic_reflects_the_previous_control_point()
        {
            // S after C mirrors the last control point; S after a line uses the current point.
            var afterCubic = Bounds("M0 0 C0 -10 10 -10 10 0 S20 10 20 0");
            var afterLine = Bounds("M0 0 L10 0 S20 10 20 0");

            Assert.That(afterCubic.Top, Is.LessThan(-1), "the cubic should still be arching upward");
            Assert.That(afterLine.Top, Is.EqualTo(0).Within(0.01), "no cubic before it, so no reflection");
        }

        [Test]
        public void The_smooth_quadratic_reflects_the_previous_control_point()
        {
            var bounds = Bounds("M0 0 Q5 -10 10 0 T20 0");

            Assert.That(bounds.Top, Is.LessThan(-1));
            Assert.That(bounds.Bottom, Is.GreaterThan(1), "the reflection should send the second hump the other way");
        }

        [Test]
        public void A_quadratic_is_raised_to_the_cubic_that_draws_the_same_curve()
        {
            // Degree elevation is exact: the control points move to a third of the way along,
            // and the curve is unchanged. Written as the equivalent cubic, the two must agree
            // exactly - which is a stronger statement than any tolerance on the drawn shape.
            var quadratic = Bounds("M0 0 Q5 10 10 0");
            var equivalentCubic = Bounds("M0 0 C3.3333333 6.6666667 6.6666667 6.6666667 10 0");

            Assert.That(quadratic.Top, Is.EqualTo(equivalentCubic.Top).Within(0.001));
            Assert.That(quadratic.Bottom, Is.EqualTo(equivalentCubic.Bottom).Within(0.001));
            Assert.That(quadratic.Left, Is.EqualTo(equivalentCubic.Left).Within(0.001));
            Assert.That(quadratic.Right, Is.EqualTo(equivalentCubic.Right).Within(0.001));
        }

        [Test]
        public void The_curve_a_quadratic_draws_reaches_its_midpoint_and_no_further()
        {
            // GraphicsPath.GetBounds is generous around curves - it bounds the control points
            // rather than the ink - so measure the curve itself. At t=0.5 a quadratic sits at a
            // quarter, a half, a quarter of its three points: y = 0.25*0 + 0.5*10 + 0.25*0 = 5.
            using (var path = SvgPath.Parse("M0 0 Q5 10 10 0"))
            {
                path.Flatten(null, 0.01f);
                var deepest = path.PathPoints.Max(p => p.Y);

                Assert.That(deepest, Is.EqualTo(5).Within(0.05));
            }
        }

        [Test]
        public void Close_returns_the_pen_to_where_the_subpath_started()
        {
            var bounds = Bounds("M10 10 L20 10 L20 20 Z L30 10");

            Assert.That(bounds.Right, Is.EqualTo(30).Within(0.01), "the line after Z starts back at 10,10");
        }

        [Test]
        public void Whitespace_and_commas_are_interchangeable()
        {
            var spaced = Bounds("M 0 0 L 10 0 L 10 10");
            var commas = Bounds("M0,0L10,0L10,10");

            Assert.That(commas.Right, Is.EqualTo(spaced.Right).Within(0.01));
            Assert.That(commas.Bottom, Is.EqualTo(spaced.Bottom).Within(0.01));
        }

        [Test]
        public void An_unknown_command_is_reported_rather_than_ignored()
        {
            Assert.That(() => SvgPath.Parse("M0 0 X10 10"), Throws.TypeOf<FormatException>());
        }

        [Test]
        public void An_arc_flag_that_is_not_zero_or_one_is_reported()
        {
            Assert.That(() => SvgPath.Parse("M0 0 A5 5 0 2 1 10 0"), Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Truncated_path_data_is_reported()
        {
            Assert.That(() => SvgPath.Parse("M0 0 L10"), Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Nonzero_winding_keeps_overlapping_shapes_solid()
        {
            // Two squares wound the same way overlap into one solid shape under nonzero, and
            // would show a hole under the alternate rule GDI+ starts with.
            using (var path = SvgPath.Parse("M0 0 H10 V10 H0 Z M2 2 H8 V8 H2 Z"))
            {
                Assert.That(path.FillMode, Is.EqualTo(System.Drawing.Drawing2D.FillMode.Winding));
                Assert.That(path.IsVisible(new PointF(5, 5)), Is.True, "the middle should be filled, not a hole");
            }
        }

        [Test]
        public void An_opposite_wound_subpath_punches_a_hole()
        {
            // Outer one way round, inner the other: this is how every ring icon is drawn, and
            // the only thing that makes the middle a hole under the nonzero rule.
            using (var path = SvgPath.Parse("M0 0 H10 V10 H0 Z M2 2 V8 H8 V2 Z"))
            {
                Assert.That(path.IsVisible(new PointF(5, 5)), Is.False, "the middle should be a hole");
                Assert.That(path.IsVisible(new PointF(1, 5)), Is.True, "the ring itself should be solid");
            }
        }
    }
}
