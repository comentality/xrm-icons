using System.Drawing;

namespace Icons.Ui
{
    /// <summary>
    /// Every number the mock draws with, in one place, each saying where it came from.
    ///
    /// The sizes and the light colours were measured on 9 September 2026, in a model-driven
    /// app in a demo org: Power Pages Management, on the Web Pages list, at 100% zoom and
    /// devicePixelRatio 1, by `navChrome()` in docs/ui-measurements/probe.js. Rerun it there
    /// and the same numbers should come back; if they do not, these are what changed.
    ///
    /// The dark colours are still GUESS. That environment has no dark theme to measure: Set
    /// Personal Options offers high contrast and nothing else, so there was no dark app to
    /// point the probe at. A dark palette derived from a light one is a guess wearing a
    /// measurement's clothes, so they stay marked until someone runs it in an org that has one.
    ///
    /// Sizes are CSS pixels as a browser at 100% zoom would use them. The renderer multiplies
    /// by DeviceDpi / 96 so the drawing is the same physical size on the screen as the app.
    /// </summary>
    public static class NavChrome
    {
        // ----- site map, expanded -----
        // The pane is 200 wide including its 1px right border. Rows measured 181 because a
        // scrollbar sat inside a nav with more entries than fitted; the mock does not scroll,
        // so its rows are the full width.
        public const int NavWidth = 200;      // measured
        public const int NavRowHeight = 35;   // measured
        public const int NavIconBox = 16;     // measured - the app draws a 24-viewBox icon at 16
        public const int NavPadLeft = 14;     // measured 14.5, rounded
        public const int NavGapToText = 14;   // measured - label starts at x 44.5
        public const int NavFontPx = 14;      // measured - Segoe UI 400
        // A group is a 35px label line with 4px above and below it: 244.0 to 287.0.
        public const int NavGroupHeight = 43; // measured
        public const int NavGroupFontPx = 14; // measured - same size as a row, weight 600

        // The selection bar: 4 wide, 21 tall, 4 in from the left, centred in the row.
        public const int AccentWidth = 4;     // measured
        public const int AccentHeight = 21;   // measured
        public const int AccentInset = 4;     // measured 4.0

        // ----- site map, collapsed to a rail -----
        // The icon does not re-centre when the pane narrows: it stays at the same 14.5.
        public const int RailWidth = 44;      // measured
        public const int RailIconBox = 16;    // measured

        public const string FontFamily = "Segoe UI"; // measured
    }

    /// <summary>Light and dark, as two sets of the same colours.</summary>
    public sealed class NavTheme
    {
        public static readonly NavTheme Light = new NavTheme
        {
            Name = "Light",
            Page = Color.FromArgb(0xFF, 0xFF, 0xFF),        // measured - the canvas beside the nav
            NavBack = Color.FromArgb(0xF0, 0xF0, 0xF0),     // measured
            // Selection is lighter than the pane, not darker: the selected row is painted the
            // page's own white. In the rail there is no fill at all, only the accent bar.
            NavSelected = Color.FromArgb(0xFF, 0xFF, 0xFF), // measured
            Text = Color.FromArgb(0x24, 0x24, 0x24),        // measured
            // A group header is the same colour as a row, and differs only by weight.
            SubText = Color.FromArgb(0x24, 0x24, 0x24),     // measured
            Line = Color.FromArgb(0xD1, 0xD1, 0xD1),        // measured - the nav's 1px right border
            Accent = Color.FromArgb(0x0F, 0x6C, 0xBD),      // measured
            IconColour = Color.FromArgb(0x42, 0x42, 0x42)   // measured - what currentColor resolves to
        };

        public static readonly NavTheme Dark = new NavTheme
        {
            Name = "Dark",
            Page = Color.FromArgb(0x1B, 0x1A, 0x19),        // GUESS
            NavBack = Color.FromArgb(0x25, 0x24, 0x23),     // GUESS
            NavSelected = Color.FromArgb(0x32, 0x31, 0x30), // GUESS
            Text = Color.FromArgb(0xF3, 0xF2, 0xF1),        // GUESS
            SubText = Color.FromArgb(0xA1, 0x9F, 0x9D),     // GUESS
            Line = Color.FromArgb(0x3B, 0x3A, 0x39),        // GUESS
            Accent = Color.FromArgb(0x47, 0x9E, 0xF5),      // GUESS
            IconColour = Color.FromArgb(0xF3, 0xF2, 0xF1)   // GUESS
        };

        public string Name;
        public Color Page, NavBack, NavSelected, Text, SubText, Line, Accent, IconColour;
    }
}
