using System.Drawing;

namespace Icons.MockHarness
{
    /// <summary>
    /// Every number the mock draws with, in one place, each saying where it came from.
    ///
    /// Nothing here has been measured yet. Every value is marked GUESS, and the point of the
    /// mark is that it is a lie until a number from docs/ui-measurements replaces it. A site
    /// map entry drawn four pixels off is not a preview of anything; it is a confident claim
    /// about what the user is shipping. So the provenance travels with the constant rather
    /// than in a commit message nobody reads again.
    ///
    /// Sizes are CSS pixels as a browser at 100% zoom would use them. The renderer multiplies
    /// by DeviceDpi / 96 so the drawing is the same physical size on the screen as the app.
    /// </summary>
    internal static class Chrome
    {
        // ----- site map, expanded -----
        public const int NavWidth = 224;      // GUESS
        public const int NavRowHeight = 40;   // GUESS
        public const int NavIconBox = 20;     // GUESS
        public const int NavPadLeft = 16;     // GUESS
        public const int NavGapToText = 12;   // GUESS
        public const int NavFontPx = 14;      // GUESS

        // ----- site map, collapsed to a rail -----
        public const int RailWidth = 48;      // GUESS
        public const int RailIconBox = 20;    // GUESS

        // ----- the page header over a grid -----
        public const int HeaderHeight = 56;   // GUESS
        public const int HeaderIconBox = 24;  // GUESS
        public const int HeaderFontPx = 20;   // GUESS
        public const int HeaderGap = 12;      // GUESS

        // ----- a lookup's results, and the Recent flyout -----
        public const int ListRowHeight = 36;  // GUESS
        public const int ListIconBox = 16;    // GUESS
        public const int ListFontPx = 14;     // GUESS
        public const int ListPadLeft = 12;    // GUESS
        public const int ListGapToText = 10;  // GUESS

        // ----- a record's form header -----
        public const int FormHeaderHeight = 64; // GUESS
        public const int FormIconBox = 20;      // GUESS
        public const int FormTitleFontPx = 20;  // GUESS
        public const int FormSubFontPx = 12;    // GUESS

        public const string FontFamily = "Segoe UI";
    }

    /// <summary>Light and dark, as two sets of the same colours.</summary>
    internal sealed class Theme
    {
        public static readonly Theme Light = new Theme
        {
            Name = "Light",
            Page = Color.FromArgb(0xFF, 0xFF, 0xFF),        // GUESS
            NavBack = Color.FromArgb(0xFA, 0xF9, 0xF8),     // GUESS
            NavSelected = Color.FromArgb(0xED, 0xEB, 0xE9), // GUESS
            Text = Color.FromArgb(0x24, 0x24, 0x24),        // GUESS
            SubText = Color.FromArgb(0x61, 0x61, 0x61),     // GUESS
            Line = Color.FromArgb(0xE1, 0xDF, 0xDD),        // GUESS
            Accent = Color.FromArgb(0x0F, 0x6C, 0xBD),      // GUESS
            IconColour = Color.FromArgb(0x24, 0x24, 0x24)   // what currentColor resolves to
        };

        public static readonly Theme Dark = new Theme
        {
            Name = "Dark",
            Page = Color.FromArgb(0x1B, 0x1A, 0x19),        // GUESS
            NavBack = Color.FromArgb(0x25, 0x24, 0x23),     // GUESS
            NavSelected = Color.FromArgb(0x32, 0x31, 0x30), // GUESS
            Text = Color.FromArgb(0xF3, 0xF2, 0xF1),        // GUESS
            SubText = Color.FromArgb(0xA1, 0x9F, 0x9D),     // GUESS
            Line = Color.FromArgb(0x3B, 0x3A, 0x39),        // GUESS
            Accent = Color.FromArgb(0x47, 0x9E, 0xF5),      // GUESS
            IconColour = Color.FromArgb(0xF3, 0xF2, 0xF1)
        };

        public string Name;
        public Color Page, NavBack, NavSelected, Text, SubText, Line, Accent, IconColour;
    }
}
