using Icons.Catalog;

namespace Icons.Logic
{
    /// <summary>
    /// What a licence that asks for a credit line means for somebody about to use one of its
    /// drawings, in words.
    ///
    /// The page this talks about is written beside every icon regardless - for the four families
    /// bundled today it is a courtesy, and a courtesy is not worth a warning line. This is for
    /// the set whose licence makes it the condition of using the drawing at all: there the extra
    /// web resource is not a nicety the tool threw in, it is the thing that makes the icon usable,
    /// and refusing to write it refuses the icon. Somebody should learn that from the library
    /// while they are still choosing, not from the confirmation they open already decided.
    ///
    /// It is here rather than in the window because a sentence that carries a licence obligation
    /// is worth being able to test.
    /// </summary>
    public static class AttributionNotice
    {
        /// <summary>
        /// The whole of it, for the line under the library: what the licence asks, what gets
        /// written because of it, and what happens if it cannot be. Null when this licence asks
        /// only that the notice travel with the software, which is every family bundled today.
        ///
        /// Short on purpose. It is one line among three the window can show at once, in a pane
        /// that has to find the height for all of them out of the preview above it, and a
        /// paragraph here is a scroll bar down the side of the form.
        /// </summary>
        public static string For(IconLicence licence, string iconWebResourceName)
        {
            if (licence == null || !licence.RequiresNotice) return null;

            return Asks(licence) + " Applying this icon writes " + PageFor(iconWebResourceName) +
                   " to carry it; without that page the icon is not put on the table.";
        }

        /// <summary>
        /// The reason on its own, for a gate that has already named the page it is about to
        /// write. Null on the same terms as <see cref="For"/>.
        /// </summary>
        public static string Why(IconLicence licence)
        {
            if (licence == null || !licence.RequiresNotice) return null;

            return Asks(licence) + " That page carries it, and without it the icon is not " +
                   "put on the table.";
        }

        private static string Asks(IconLicence licence)
        {
            return licence.SetName + " is licensed under " + licence.Licence +
                   ", which asks for a credit line.";
        }

        /// <summary>
        /// The page's name, or the bare file name when there is not yet a usable icon name to
        /// work it out from. A half-typed name is the ordinary state of the box this comes from,
        /// and naming no file at all would be the one part of the sentence that matters going
        /// missing exactly while somebody is deciding.
        /// </summary>
        private static string PageFor(string iconWebResourceName)
        {
            return string.IsNullOrWhiteSpace(iconWebResourceName)
                ? AttributionPage.FileName
                : AttributionPage.NameFor(iconWebResourceName);
        }
    }
}
