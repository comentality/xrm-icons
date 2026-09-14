using System;

namespace Icons.Catalog
{
    /// <summary>
    /// Which of the two classes of weight an icon is drawn in, whatever its family calls it.
    ///
    /// Every family bundled here draws its icons both as outlines and as solids, under its own
    /// words: Fluent says regular and filled, Phosphor says regular, bold and fill, Tabler says
    /// outline and filled. The weight filter is about the distinction rather than the words, so
    /// Filled here means solid and Regular means everything else. <see cref="IconEntry.Weight"/>
    /// keeps the family's own word for the reader.
    /// </summary>
    public enum IconStyle
    {
        Regular,
        Filled
    }

    /// <summary>The icon sets bundled in this tool.</summary>
    public enum IconFamily
    {
        Fluent,
        Phosphor,
        Lucide,
        Tabler,

        // Appended rather than slotted in: IconSearch breaks a tie on this order, so where
        // a member sits decides where its icons rank against an identical score. Anywhere
        // but the end would move every result the older families already give.
        FontAwesome,
        Solar
    }

    /// <summary>
    /// One icon in the bundled library. Holds no SVG: the text is read from the archive on
    /// demand, because a library of this size is worth about ten megabytes and the window only
    /// ever shows a screenful.
    /// </summary>
    public sealed class IconEntry
    {
        /// <summary>A Fluent icon, which is what an entry was before there was more than one family.</summary>
        public IconEntry(string name, IconStyle style)
            : this(IconFamily.Fluent, name, style == IconStyle.Filled ? "filled" : "regular", style)
        {
        }

        public IconEntry(IconFamily family, string name, string weight, IconStyle style)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Weight = weight ?? throw new ArgumentNullException(nameof(weight));
            Family = family;
            Style = style;

            // Fluent separates words with underscores and every other family with dashes.
            // Splitting on both gives one search vocabulary rather than one per family.
            Words = name.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            DisplayName = BuildDisplayName(Words);
            EntryPath = FolderFor(family) + "/" + weight + "/" + name + ".svg";
        }

        /// <summary>Which set drew it.</summary>
        public IconFamily Family { get; }

        /// <summary>The family's own name for the icon: <c>arrow_trending_lines</c>, <c>shopping-cart</c>.</summary>
        public string Name { get; }

        /// <summary>The family's own word for the weight: regular, filled, bold, fill, outline.</summary>
        public string Weight { get; }

        /// <summary>Outline or solid, in the terms the weight filter offers.</summary>
        public IconStyle Style { get; }

        /// <summary>The same name as a person would read it: "Arrow trending lines".</summary>
        public string DisplayName { get; }

        /// <summary>The name split into words. What search matches against.</summary>
        public string[] Words { get; }

        /// <summary>Where this icon lives inside the bundled archive.</summary>
        public string EntryPath { get; }

        /// <summary>Family and weight, as the window says it: "Phosphor bold".</summary>
        public string Origin
        {
            get { return Family.ToString() + " " + Weight; }
        }

        public override string ToString()
        {
            return DisplayName + " (" + Origin + ")";
        }

        /// <summary>The archive folder a family's icons are under.</summary>
        public static string FolderFor(IconFamily family)
        {
            return family.ToString().ToLowerInvariant();
        }

        private static string BuildDisplayName(string[] words)
        {
            if (words.Length == 0) return "";
            var joined = string.Join(" ", words);
            return char.ToUpperInvariant(joined[0]) + joined.Substring(1);
        }
    }
}
