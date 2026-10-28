using System;
using System.Linq;

namespace Icons.Logic
{
    /// <summary>
    /// Taking a table's icon off when there is no apply on the undo stack to take back - an icon
    /// put on in another session, by another tool, or by hand.
    ///
    /// It is the undo's own write with nothing to go back to: an empty IconVectorName, which
    /// clears, where a null would publish and change nothing. So it is built as an
    /// <see cref="IconRevertRequest"/> and goes out through <see cref="IconApplier.Revert"/>,
    /// and the two requests here are the removal and the way back from it.
    ///
    /// The classic PNG slots go with the vector only when they are the vector's own - the three
    /// this tool writes beside it, named after it. Those are one icon in four sizes and leaving
    /// them would be taking a quarter of it off. A PNG named anything else is somebody else's
    /// decision, made separately, and stays where it is.
    ///
    /// What it never does is uncustomise. RemoveActiveCustomizations drops the table's whole
    /// unmanaged layer, cannot be undone, and would let a managed value underneath reappear -
    /// see the item in TODO.md. Writing empty over the top is the one thing that takes the icon
    /// off and nothing else.
    /// </summary>
    public static class IconRemoval
    {
        /// <summary>
        /// The write that takes the icon off: the vector emptied, and each classic slot emptied
        /// if it is one of the vector's own and written back as it stands if not.
        /// </summary>
        public static IconRevertRequest Off(string tableLogicalName, string vector,
                                            string small, string medium, string large,
                                            string solutionUniqueName)
        {
            if (string.IsNullOrWhiteSpace(tableLogicalName))
                throw new ArgumentException("No table to take the icon off.", nameof(tableLogicalName));

            var classic = TakesClassic(vector, small, medium, large);

            return new IconRevertRequest
            {
                TableLogicalName = tableLogicalName,
                VectorName = "",
                RestoreRasterIcons = classic,
                SmallName = classic && IsOwn(vector, small, IconSlot.Small) ? "" : small,
                MediumName = classic && IsOwn(vector, medium, IconSlot.Medium) ? "" : medium,
                LargeName = classic && IsOwn(vector, large, IconSlot.Large) ? "" : large,
                SolutionUniqueName = solutionUniqueName
            };
        }

        /// <summary>
        /// The write that puts it back: every slot the removal touched, pointed at what it named
        /// before. Nothing was written over and nothing deleted, so there is no drawing to restore
        /// and no attribution page to rebuild - the folder is exactly as the removal found it.
        /// </summary>
        public static IconRevertRequest Back(string tableLogicalName, string vector,
                                             string small, string medium, string large,
                                             string solutionUniqueName)
        {
            if (string.IsNullOrWhiteSpace(tableLogicalName))
                throw new ArgumentException("No table to put the icon back on.", nameof(tableLogicalName));

            return new IconRevertRequest
            {
                TableLogicalName = tableLogicalName,
                VectorName = vector,
                RestoreRasterIcons = TakesClassic(vector, small, medium, large),
                SmallName = small,
                MediumName = medium,
                LargeName = large,
                SolutionUniqueName = solutionUniqueName
            };
        }

        /// <summary>
        /// Whether taking this vector off takes any classic slot with it: true when at least one
        /// of them points at the PNG this tool would have written beside it.
        /// </summary>
        public static bool TakesClassic(string vector, string small, string medium, string large)
        {
            return IsOwn(vector, small, IconSlot.Small) ||
                   IsOwn(vector, medium, IconSlot.Medium) ||
                   IsOwn(vector, large, IconSlot.Large);
        }

        /// <summary>
        /// The classic slots that point somewhere other than the vector's own PNGs, by name. They
        /// stay on, and the confirmation has to say so, because a table with its vector gone and
        /// a classic icon still on is not a table with no icon on every surface.
        /// </summary>
        public static string[] Staying(string vector, string small, string medium, string large)
        {
            return new[]
                {
                    IsOwn(vector, small, IconSlot.Small) ? null : small,
                    IsOwn(vector, medium, IconSlot.Medium) ? null : medium,
                    IsOwn(vector, large, IconSlot.Large) ? null : large
                }
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool IsOwn(string vector, string slotValue, IconSlot slot)
        {
            if (string.IsNullOrWhiteSpace(vector) || string.IsNullOrWhiteSpace(slotValue)) return false;

            return string.Equals(slotValue.Trim(), RasterIcons.NameFor(vector.Trim(), slot),
                                 StringComparison.OrdinalIgnoreCase);
        }
    }
}
