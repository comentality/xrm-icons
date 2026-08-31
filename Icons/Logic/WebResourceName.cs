using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Icons.Logic
{
    /// <summary>
    /// Builds and checks the name a web resource is stored under.
    ///
    /// The name is not cosmetic. It is the key the table's icon property points at, it cannot
    /// be changed afterwards without repointing everything that refers to it, and Dataverse
    /// rejects the whole create with a message that does not say which character it disliked.
    /// So the rules are enforced here, before the round trip.
    /// </summary>
    public static class WebResourceName
    {
        /// <summary>webresource.name is a 256 character column.</summary>
        public const int MaxLength = 256;

        /// <summary>
        /// The conventional name for a table's icon: publisher prefix, then a folder, then the
        /// table's logical name. Predictable, so a second run finds and updates the same record
        /// instead of littering the org with near-duplicates.
        /// </summary>
        public static string ForTable(string prefix, string tableLogicalName, string folder = "icons")
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("A publisher prefix is required.", nameof(prefix));
            if (string.IsNullOrWhiteSpace(tableLogicalName)) throw new ArgumentException("A table name is required.", nameof(tableLogicalName));

            var cleanPrefix = prefix.Trim().TrimEnd('_');
            var cleanFolder = (folder ?? "").Trim().Trim('/');

            var builder = new StringBuilder();
            builder.Append(cleanPrefix).Append('_').Append('/');
            if (cleanFolder.Length > 0) builder.Append(cleanFolder).Append('/');
            builder.Append(Sanitize(tableLogicalName)).Append(".svg");

            return builder.ToString();
        }

        /// <summary>
        /// Why Dataverse would refuse this name, or null if it would not.
        /// </summary>
        public static string Validate(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "The web resource needs a name.";

            if (name.Length > MaxLength)
                return "The name is " + name.Length + " characters; the limit is " + MaxLength + ".";

            var bad = name.Where(c => !IsAllowed(c)).Distinct().ToArray();
            if (bad.Length > 0)
            {
                var shown = string.Join(" ", bad.Select(Describe));
                return "A web resource name may only contain letters, digits, dot, underscore and forward slash. " +
                       "Remove: " + shown;
            }

            if (name.StartsWith("/", StringComparison.Ordinal))
                return "The name must start with the publisher prefix, not a slash.";

            if (name.Contains("//"))
                return "The name has an empty folder in it (a double slash).";

            var underscore = name.IndexOf('_');
            if (underscore <= 0)
                return "The name must begin with a publisher prefix followed by an underscore, such as new_/icons/thing.svg.";

            return null;
        }

        /// <summary>
        /// Makes a fragment safe to put in a name: lowercase, and anything Dataverse would
        /// reject replaced with an underscore rather than dropped, so two different inputs do
        /// not collapse onto the same name.
        /// </summary>
        public static string Sanitize(string fragment)
        {
            if (string.IsNullOrEmpty(fragment)) return "";

            var builder = new StringBuilder(fragment.Length);
            foreach (var c in fragment.Trim().ToLowerInvariant())
            {
                builder.Append(char.IsLetterOrDigit(c) || c == '.' || c == '_' ? c : '_');
            }
            return builder.ToString();
        }

        private static bool IsAllowed(char c)
        {
            return char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '/';
        }

        private static string Describe(char c)
        {
            if (c == ' ') return "space";
            if (char.IsControl(c)) return "U+" + ((int)c).ToString("X4", CultureInfo.InvariantCulture);
            return c.ToString();
        }
    }
}
