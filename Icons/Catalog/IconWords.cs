using System;
using System.Collections.Generic;

namespace Icons.Catalog
{
    /// <summary>
    /// The words an icon answers to besides its own name, as the bundle carries them.
    ///
    /// There are two kinds and they are not worth the same, which is the whole reason they are
    /// told apart rather than poured into one list. An <see cref="Aliases">alias</see> is a name:
    /// one the set published for this drawing and later stopped leading with, so Font Awesome's
    /// address-card still answers to vcard. A <see cref="Tags">description</see> is what the
    /// people who drew it say it is for - Microsoft's own metadata says a shield is about
    /// security, protection and safety - which is a true thing about the icon and still not its
    /// name. A name is worth 100, this tool's own curated word for a Dataverse term 50, and a
    /// description 45; see <see cref="IconSearch"/> for why that order and not another.
    ///
    /// None of these words reach <see cref="IconEntry.Words"/>, and that is deliberate: that
    /// array is what the grid label is built from and what an icon is charged for when the query
    /// does not account for all of it. A word somebody else attached to the drawing belongs to
    /// neither.
    /// </summary>
    public sealed class IconWords
    {
        // Declared before None, and that is not tidiness: static fields are initialised in the
        // order they are written, and None runs the constructor, which reads both of these.
        private static readonly string[] Nothing = new string[0];
        private static readonly char[] Separators = { ' ', '_', '-', '/' };

        /// <summary>An icon nobody has said anything else about. Most of the library.</summary>
        public static readonly IconWords None = new IconWords(null, null);

        public IconWords(string[] aliases, string[] tags)
        {
            Aliases = aliases ?? Nothing;
            Tags = tags ?? Nothing;
            AliasWords = Split(Aliases);
            TagWords = Split(Tags);
        }

        /// <summary>Other names the icon's own set published for this same drawing.</summary>
        public string[] Aliases { get; }

        /// <summary>What the set that drew it says it depicts or is used for.</summary>
        public string[] Tags { get; }

        /// <summary>The aliases split into words, the way a name is.</summary>
        public string[] AliasWords { get; }

        /// <summary>The descriptions split into words. Upstream writes phrases as well as words.</summary>
        public string[] TagWords { get; }

        /// <summary>Whether this is worth asking anything of at all.</summary>
        public bool Any
        {
            get { return AliasWords.Length > 0 || TagWords.Length > 0; }
        }

        /// <summary>
        /// Every word in every one of these, in order. Split on the space as well as on the
        /// separators a name uses, because a description is written as English rather than as an
        /// identifier: Tabler says "adding up" and Microsoft says "3 lines".
        /// </summary>
        private static string[] Split(string[] names)
        {
            if (names.Length == 0) return Nothing;

            var words = new List<string>(names.Length);
            foreach (var name in names)
                words.AddRange(name.Split(Separators, StringSplitOptions.RemoveEmptyEntries));

            return words.Count == 0 ? Nothing : words.ToArray();
        }
    }
}
