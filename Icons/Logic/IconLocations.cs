using System;
using System.Collections.Generic;
using System.Linq;

namespace Icons.Logic
{
    /// <summary>
    /// Where this environment already keeps its table icons, and therefore where the next one
    /// should go.
    ///
    /// The conventional <c>prefix_/icons/table.svg</c> is a guess, and it is the wrong guess in
    /// every org that settled on something else years ago - which is most of the ones that have
    /// any icons at all. The answer is already in hand: the load reads every table's
    /// IconVectorName, so what the org does can be read off what the org has, without another
    /// round trip.
    ///
    /// Only icons named with one of this environment's own publisher prefixes count. Microsoft's
    /// first-party solutions put hundreds of icons in their own folders, and a majority vote that
    /// counted those would confidently follow a convention nobody here chose.
    /// </summary>
    public sealed class IconLocations
    {
        /// <summary>What to suggest when the org has no icons of its own to learn from.</summary>
        public const string DefaultFolder = "icons";

        /// <summary>An org nothing is known about yet: before a load, or after one that failed.</summary>
        public static readonly IconLocations None = new IconLocations(DefaultFolder, new string[0]);

        private readonly HashSet<string> _ourPrefixes;

        private IconLocations(string folder, IEnumerable<string> ourPrefixes)
        {
            Folder = folder;
            _ourPrefixes = new HashSet<string>(
                ourPrefixes.Where(p => !string.IsNullOrWhiteSpace(p)).Select(Normalise),
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The folder new icons should go in: whichever one this org's existing icons are in
        /// most often. Empty means the icons sit directly under the prefix, which is a
        /// convention too.
        /// </summary>
        public string Folder { get; }

        /// <summary>
        /// Reads the convention off what is already there.
        /// </summary>
        /// <param name="existingIcons">Every table's IconVectorName, nulls and all.</param>
        /// <param name="ourPrefixes">The publisher prefixes of the environment's unmanaged solutions.</param>
        public static IconLocations From(IEnumerable<string> existingIcons, IEnumerable<string> ourPrefixes)
        {
            var prefixes = (ourPrefixes ?? new string[0]).ToList();
            var mine = new IconLocations(DefaultFolder, prefixes);

            var folders = (existingIcons ?? new string[0])
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Where(mine.IsOurs)
                .Select(FolderOf)
                .Where(folder => folder != null)
                .ToList();

            if (folders.Count == 0) return mine;

            // Counted case-insensitively, because an org holding both Icons/ and icons/ has one
            // convention and two typists. Ties break alphabetically rather than on whichever
            // table happened to come back first, so the same org always gets the same answer.
            var convention = folders
                .GroupBy(folder => folder, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .First();

            // The spelling that wins is the one the org uses most, since the name it goes out
            // under is the exact string, not the comparison.
            var winner = convention
                .GroupBy(folder => folder, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .First();

            return new IconLocations(winner.Key, prefixes);
        }

        /// <summary>
        /// What to store the icon for this table under: the name it already uses when that name
        /// is one this connection can write, and the conventional name in the learned folder
        /// otherwise.
        ///
        /// Reusing the existing name is what makes a second visit to a table update one web
        /// resource rather than leave a second one beside it with nothing pointing at the first.
        /// </summary>
        public string NameFor(string currentIcon, string prefix, string tableLogicalName)
        {
            if (Reusable(currentIcon)) return currentIcon.Trim();

            return WebResourceName.ForTable(
                string.IsNullOrWhiteSpace(prefix) ? "new" : prefix,
                tableLogicalName,
                Folder);
        }

        /// <summary>
        /// Whether the name a table already carries is one to write over. A managed icon - and
        /// anything under a prefix that is not this environment's - is not: suggesting it would
        /// only buy a round trip and the platform's refusal.
        /// </summary>
        public bool Reusable(string currentIcon)
        {
            if (string.IsNullOrWhiteSpace(currentIcon)) return false;

            var name = currentIcon.Trim();

            // Only an SVG belongs in IconVectorName, and the upsert refuses to write one over a
            // web resource of another kind. If a table points at something else, leave it be.
            if (!name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) return false;

            if (WebResourceName.Validate(name) != null) return false;

            return IsOurs(name);
        }

        /// <summary>Whether a web resource name belongs to one of this environment's publishers.</summary>
        private bool IsOurs(string name)
        {
            var prefix = PrefixOf(name);
            return prefix != null && _ourPrefixes.Contains(prefix);
        }

        /// <summary>
        /// The publisher prefix a web resource name begins with, or null if it has none. The
        /// prefix is everything before the first underscore: <c>new_/icons/a.svg</c> is new's.
        /// </summary>
        public static string PrefixOf(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var underscore = name.IndexOf('_');
            if (underscore <= 0) return null;

            var prefix = name.Substring(0, underscore);

            // A slash before the underscore means there is no prefix, just a path.
            return prefix.IndexOf('/') >= 0 ? null : prefix.ToLowerInvariant();
        }

        /// <summary>
        /// The folder part of a web resource name - everything between the publisher prefix and
        /// the file name - or "" when the file sits directly under the prefix. Null when the
        /// name has no prefix to strip, which means it is not one to learn from.
        ///
        /// Multi-segment folders are kept whole: an org that files icons under
        /// <c>Icons/Tables</c> gets its next one there, not in <c>Icons</c>.
        /// </summary>
        public static string FolderOf(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var underscore = name.IndexOf('_');
            if (underscore < 0) return null;

            var rest = name.Substring(underscore + 1).TrimStart('/');
            var lastSlash = rest.LastIndexOf('/');
            return lastSlash < 0 ? "" : rest.Substring(0, lastSlash).Trim('/');
        }

        private static string Normalise(string prefix)
        {
            return prefix.Trim().TrimEnd('_').ToLowerInvariant();
        }
    }
}
