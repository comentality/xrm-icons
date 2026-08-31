using System;

namespace Icons.Catalog
{
    /// <summary>Which of the two weights Fluent draws every icon in.</summary>
    public enum IconStyle
    {
        Regular,
        Filled
    }

    /// <summary>
    /// One icon in the bundled library. Holds no SVG: the text is read from the archive on
    /// demand, because a library of six thousand icons is worth about three megabytes and the
    /// window only ever shows a screenful.
    /// </summary>
    public sealed class IconEntry
    {
        public IconEntry(string name, IconStyle style)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Style = style;
            Words = name.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            DisplayName = BuildDisplayName(Words);
            EntryPath = (style == IconStyle.Filled ? "filled/" : "regular/") + name + ".svg";
        }

        /// <summary>Fluent's own name, underscore separated: <c>arrow_trending_lines</c>.</summary>
        public string Name { get; }

        public IconStyle Style { get; }

        /// <summary>The same name as a person would read it: "Arrow trending lines".</summary>
        public string DisplayName { get; }

        /// <summary>The name split on underscores. What search matches against.</summary>
        public string[] Words { get; }

        /// <summary>Where this icon lives inside the bundled archive.</summary>
        public string EntryPath { get; }

        public override string ToString()
        {
            return DisplayName + " (" + Style.ToString().ToLowerInvariant() + ")";
        }

        private static string BuildDisplayName(string[] words)
        {
            if (words.Length == 0) return "";
            var joined = string.Join(" ", words);
            return char.ToUpperInvariant(joined[0]) + joined.Substring(1);
        }
    }
}
