using System;
using System.Collections.Generic;
using System.Linq;

namespace Icons.Logic
{
    /// <summary>
    /// The icon a table already has without anybody having given it one.
    ///
    /// A standard table keeps no icon in Dataverse at all - account, contact and the rest come
    /// back from RetrieveAllEntities with all four icon slots null - and yet a model-driven app
    /// puts a building beside Account. The app draws it from a font of its own, "Dyn CRM Symbol",
    /// by the glyph that belongs to the table: measured in a real app, account is U+EED6,
    /// connection U+EFD4, activitypointer U+EFF4. A table that does have a web resource is drawn
    /// from the web resource instead, so the stock icon is what fills in where this tool has not
    /// been.
    ///
    /// The org serves the vector of that same glyph at /_imgs/svg_&lt;object type code&gt;.svg -
    /// the classic icon store, which names each drawing after the glyph it is: svg_1.svg is
    /// titled Account_EED6, svg_3234.svg Connection_EFD4. So the address is both the test of
    /// whether a table has a stock icon and the drawing itself, and neither has to be carried
    /// around in here going stale as Microsoft adds tables.
    ///
    /// Asked anonymously, because the address answers anonymously: it is static content on the
    /// org's own host, not the Web API, and needs no token. That keeps this off the connection
    /// entirely - nothing here can spend a token, invalidate one, or fail because the SDK client
    /// is busy with something else.
    ///
    /// Nothing here is required for the window to work. An org that will not answer - a proxy in
    /// the way, an on-premise deployment that serves none of this, a host that swallows the
    /// request - says nothing is drawn, which is what this tool believed before it asked.
    /// </summary>
    public static class StockIcons
    {
        /// <summary>
        /// The org's own address, out of whatever the connection can say about it.
        ///
        /// Three things can answer and the first that does is believed, because not every
        /// connection carries all three: the web application url XrmToolBox fills in for a
        /// connection somebody made, the url it was originally made from, and failing both the
        /// organization service endpoint with its service path cut off. A connection that can
        /// say none of them is one nothing can be asked of, and the stock icons go unread.
        ///
        /// The path is kept, not just the host. An online org is served from the root, but an
        /// on-premise one lives under its organization name, and /_imgs is relative to that.
        /// </summary>
        public static string OrgAddress(string webApplicationUrl, string originalUrl,
                                        string organizationServiceUrl)
        {
            var address = First(webApplicationUrl, originalUrl, WithoutService(organizationServiceUrl));
            return address?.TrimEnd('/');
        }

        private static string First(params string[] candidates)
        {
            return candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))?.Trim();
        }

        /// <summary>
        /// The organization service endpoint without the endpoint. What is left is the org, which
        /// is what everything else about it hangs off.
        /// </summary>
        private static string WithoutService(string organizationServiceUrl)
        {
            if (string.IsNullOrWhiteSpace(organizationServiceUrl)) return null;

            var cut = organizationServiceUrl.IndexOf("/XRMServices", StringComparison.OrdinalIgnoreCase);
            return cut < 0 ? organizationServiceUrl : organizationServiceUrl.Substring(0, cut);
        }

        /// <summary>
        /// Where the org serves a table's stock drawing. The object type code rather than the
        /// logical name: this is the classic icon store, and it was always keyed by code.
        /// </summary>
        public static string Address(string orgUrl, int objectTypeCode)
        {
            if (string.IsNullOrEmpty(orgUrl)) return null;
            return orgUrl.TrimEnd('/') + "/_imgs/svg_" + objectTypeCode + ".svg";
        }

        /// <summary>
        /// Whether an answer off that address is really a drawing.
        ///
        /// The media type is the whole test. A table the app has no glyph for is a 404, but a
        /// proxy that wants a sign in, or a captive portal, or a host that redirects the unknown
        /// to a friendly page all answer 200 with HTML - and an icon that is really an HTML page
        /// would put every table in the org beyond this tool's help. Only an SVG counts.
        /// </summary>
        public static bool IsDrawing(int status, string mediaType, int length)
        {
            if (status != 200) return false;
            if (length <= 0) return false;

            // Taken whole or straight off the header: a caller that hands over the raw
            // "image/svg+xml; charset=utf-8" means the same thing as one that has already had
            // the parameters stripped for it, and neither should have to know which this is.
            var type = (mediaType ?? "").Split(';')[0].Trim();
            return string.Equals(type, "image/svg+xml", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The stock drawing for each of these tables that has one, by object type code.
        ///
        /// The drawing rather than a yes or no, because both callers want it: the list needs to
        /// know a table is not empty, and the preview needs something to paint. Asking twice for
        /// the same bytes would be a second round trip for an answer already in hand.
        ///
        /// <paramref name="ask"/> is given an object type code and answers with the drawing, or
        /// null for a table the app has no glyph for. It is expected to answer null rather than
        /// throw, because one table's failure is not the run's. A table with no object type code
        /// is not asked about - there is no address to ask at - and has no stock icon.
        /// </summary>
        public static IDictionary<int, string> DrawnAmong(IEnumerable<int?> objectTypeCodes,
                                                          Func<int, string> ask)
        {
            var drawn = new Dictionary<int, string>();
            if (objectTypeCodes == null || ask == null) return drawn;

            foreach (var code in objectTypeCodes.Where(c => c.HasValue).Select(c => c.Value).Distinct())
            {
                string svg;
                try
                {
                    svg = ask(code);
                }
                catch (Exception)
                {
                    // One address that would not answer is one table this tool cannot speak for.
                    // It is not a reason to stop asking about the others, and not a reason to
                    // fail a load whose real work - the tables themselves - is already in hand.
                    svg = null;
                }

                if (!string.IsNullOrEmpty(svg)) drawn[code] = svg;
            }

            return drawn;
        }
    }
}
