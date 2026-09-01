using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Icons.Logic
{
    /// <summary>
    /// The three classic PNG slots: what size each is, what this tool names it, and what to say
    /// when a table's slots already point somewhere else.
    ///
    /// One place owns all three because the sentence shown before a write is only true if it
    /// predicts exactly what the applier is about to do.
    /// </summary>
    public static class RasterIcons
    {
        /// <summary>The slots, in the order they are written and listed.</summary>
        public static readonly IList<KeyValuePair<IconSlot, Size>> Slots =
            new List<KeyValuePair<IconSlot, Size>>
            {
                new KeyValuePair<IconSlot, Size>(IconSlot.Small, new Size(16, 16)),
                new KeyValuePair<IconSlot, Size>(IconSlot.Medium, new Size(32, 32)),
                // Not square, and not a mistake: this is the shape Dataverse has always wanted here.
                new KeyValuePair<IconSlot, Size>(IconSlot.Large, new Size(66, 48))
            }.AsReadOnly();

        /// <summary>
        /// What this tool calls the PNG for a slot: the vector name with the slot appended, so
        /// a table's web resources sort together and a second run updates them rather than
        /// leaving a trail of near-duplicates.
        /// </summary>
        public static string NameFor(string vectorName, IconSlot slot)
        {
            if (vectorName == null) throw new ArgumentNullException(nameof(vectorName));

            var stem = vectorName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                ? vectorName.Substring(0, vectorName.Length - 4)
                : vectorName;
            return stem + "_" + slot.ToString().ToLowerInvariant() + ".png";
        }

        /// <summary>
        /// What the classic slots are about to stop pointing at, or null when there is nothing
        /// to say: the table has no classic icons, or the ones it has are the ones about to be
        /// written over.
        ///
        /// This states and does not advise. Whether the icons being displaced are dead weight
        /// or the only reason the table looks right on some surface is not knowable from here -
        /// Microsoft's documentation does not say where the raster icons still render - so the
        /// text says what changes and leaves the judgement where it belongs.
        /// </summary>
        public static string Handover(string vectorName, string small, string medium, string large)
        {
            if (string.IsNullOrWhiteSpace(vectorName)) return null;

            var current = new Dictionary<IconSlot, string>
            {
                { IconSlot.Small, small },
                { IconSlot.Medium, medium },
                { IconSlot.Large, large }
            };

            var lines = new List<string>();
            foreach (var slot in Slots)
            {
                var was = current[slot.Key];
                if (string.IsNullOrWhiteSpace(was)) continue;

                // Pointing at what is about to be rewritten is the ordinary second run, and
                // saying "this replaces itself" would be noise on every one of them.
                var willBe = NameFor(vectorName, slot.Key);
                if (string.Equals(was, willBe, StringComparison.OrdinalIgnoreCase)) continue;

                lines.Add("    " + slot.Value.Width + "x" + slot.Value.Height +
                          "  " + was + "  ->  " + willBe);
            }

            if (lines.Count == 0) return null;

            return "Its classic icons point somewhere else already:\n" +
                   string.Join("\n", lines) + "\n" +
                   "Writing the PNGs repoints them. What they point at now stays in the " +
                   "solution - nothing here deletes a web resource.";
        }
    }
}
