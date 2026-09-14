using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Icons.Catalog;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// The names a set published for a drawing and later stopped leading with.
    ///
    /// Font Awesome ships every name it has ever used as its own file, so address-card arrives
    /// again as contact-card and as vcard. The bundle keeps one file per drawing, which is
    /// right - the tool recognises a stored icon by what it draws, and a drawing under three
    /// names is recognised as none of them - but the names went out with the files, and the set
    /// still answers to them. This fixture says they came back as words without coming back as
    /// pictures.
    /// </summary>
    [TestFixture]
    public class AliasTests
    {
        private IconCatalog _catalog;

        [OneTimeSetUp]
        public void LoadCatalog()
        {
            _catalog = IconCatalog.Load();
        }

        [OneTimeTearDown]
        public void DisposeCatalog()
        {
            _catalog?.Dispose();
        }

        private IconEntry AddressCard
        {
            get
            {
                return _catalog.Icons.First(i => i.Family == IconFamily.FontAwesome
                                                 && i.Name == "address-card"
                                                 && i.Weight == "solid");
            }
        }

        [Test]
        public void The_bundle_carries_the_names_it_dropped()
        {
            Assert.That(AddressCard.Aliases, Is.EquivalentTo(new[] { "contact-card", "vcard" }),
                "the bundle should carry " + IconCatalog.VocabularyEntryName
                + "; run tools\\build-catalog.ps1");
        }

        [Test]
        public void Every_weight_of_one_icon_answers_to_the_same_names()
        {
            // Keyed by name rather than by entry path, so the weights share one list rather
            // than hold a copy each. Same contents, and - for the families that draw an icon
            // many times over - the same array.
            var weights = _catalog.Icons
                .Where(i => i.Family == IconFamily.FontAwesome && i.Name == "address-card")
                .ToList();

            Assert.That(weights.Count, Is.GreaterThan(1), "address-card is drawn solid and regular");
            foreach (var weight in weights)
                Assert.That(weight.Aliases, Is.SameAs(weights[0].Aliases));
        }

        [Test]
        public void A_name_the_set_dropped_still_finds_the_drawing()
        {
            // Nothing in the library is called vcard. Until now nothing answered to it either,
            // and the drawing it names sat in the grid under a word nobody had typed.
            var found = IconSearch.Rank(_catalog.Icons, "vcard").FirstOrDefault();

            Assert.That(found, Is.Not.Null, "vcard should find something");
            Assert.That(found.Name, Is.EqualTo("address-card"));
            Assert.That(found.Family, Is.EqualTo(IconFamily.FontAwesome));
        }

        [Test]
        public void An_alias_is_worth_exactly_what_the_name_is_worth()
        {
            // Which is the decision this half turns on. An alias is not a hint about the
            // drawing the way a synonym is - it is a name the set itself published for it,
            // and scoring it lower would put the icon somebody asked for by name below every
            // icon that merely has the word in it somewhere.
            var icon = AddressCard;

            Assert.That(IconSearch.Score(icon, new[] { "vcard" }),
                Is.EqualTo(IconSearch.Score(icon, new[] { "address" })));
        }

        [Test]
        public void An_alias_is_not_a_word_the_grid_shows_or_the_score_charges_for()
        {
            // Words feeds two other things: the label under the thumbnail, and the penalty an
            // icon pays for words the query never accounted for. An alias belongs to neither.
            var icon = AddressCard;

            Assert.That(icon.DisplayName, Is.EqualTo("Address card"));
            Assert.That(icon.Words, Is.EqualTo(new[] { "address", "card" }));

            var named = _catalog.Icons.First(i => i.Family == IconFamily.FontAwesome
                                                  && i.Name == "clock"
                                                  && i.Weight == "solid");

            Assert.That(named.Aliases, Is.Not.Empty, "clock is another of the aliased ones");
            Assert.That(IconSearch.Score(named, new[] { "clock" }), Is.EqualTo(100),
                "and it pays nothing for carrying them");
        }

        [Test]
        public void An_alias_ties_with_the_icon_really_called_that_and_loses_the_tie()
        {
            // The consequence of paying a name's worth for an alias is that the two tie - and
            // the tie breaks the way it breaks between any two icons of one name, on the
            // family order. So the grid is not taken away from the icon somebody's word is
            // actually the name of: Fluent draws a contact_card, and it still leads.
            var ranked = IconSearch.Rank(_catalog.Icons, "contact card");

            Assert.That(ranked.First().Family, Is.EqualTo(IconFamily.Fluent));
            Assert.That(ranked.First().Name, Is.EqualTo("contact_card"));
            Assert.That(ranked.Any(i => i.Name == "address-card"), Is.True,
                "and the one that answers to the same words by alias is in the grid with it");
        }

        [Test]
        public void Every_name_in_the_vocabulary_belongs_to_an_icon_the_bundle_shows()
        {
            // A name against an icon that is not here is dead weight that reads like coverage -
            // the same rule VocabularyTests holds the curated synonyms to. The reader skips
            // such a line rather than refusing the file, so nothing would ever say so.
            var byKey = new HashSet<string>(
                _catalog.Icons.Select(i => IconEntry.FolderFor(i.Family) + "/" + i.Name),
                StringComparer.OrdinalIgnoreCase);

            var orphans = new List<string>();
            var words = 0;

            foreach (var line in Vocabulary().Split('\n'))
            {
                var fields = line.TrimEnd('\r').Split('\t');
                if (fields.Length < 3) continue;

                if (!byKey.Contains(fields[1])) orphans.Add(fields[1]);
                words += fields.Length - 2;
            }

            Assert.That(orphans, Is.Empty, "these name nothing the bundle holds: " + string.Join(", ", orphans));
            Assert.That(words, Is.GreaterThan(500), "Font Awesome alone drops the better part of six hundred names");
        }

        [Test]
        public void A_bundle_carrying_no_vocabulary_is_still_a_working_library()
        {
            // What a bundle built before this file existed looks like, and what a rebuild that
            // forgets it looks like. The words go; nothing else does.
            using (var stripped = new MemoryStream(BundleWith(null)))
            using (var catalog = IconCatalog.Load(stripped))
            {
                Assert.That(catalog.Icons.Count, Is.EqualTo(_catalog.Icons.Count));
                Assert.That(catalog.Icons.All(i => i.Aliases.Length == 0), Is.True);
                Assert.That(catalog.Icons.All(i => i.Tags.Length == 0), Is.True);
                Assert.That(IconSearch.Rank(catalog.Icons, "vcard"), Is.Empty);
                Assert.That(IconSearch.Rank(catalog.Icons, "address card").First().Name, Is.EqualTo("address-card"));
            }
        }

        [Test]
        public void No_table_changed_its_mind_about_what_to_search_for()
        {
            // The names are for somebody typing, not for a box nobody filled in. WorthSeeding
            // was left alone on purpose - but SeedFor's first question is whether the whole
            // name answers, and that one is not gated by it, so a two-word table that used to
            // fall back to its last word could in principle now answer whole on an alias.
            // Asked of all 162 rather than reasoned about.
            //
            // Against the aliases alone, and not against the whole vocabulary: Fluent's
            // descriptions did move eleven of these, on purpose and for the better, and
            // DescriptionTests is where that is pinned. Mixing the two would let one of them
            // cover for the other.
            using (var before = new MemoryStream(BundleWith(null)))
            using (var was = IconCatalog.Load(before))
            using (var after = new MemoryStream(BundleWith(OnlyAliases())))
            using (var now = IconCatalog.Load(after))
            {
                Assert.That(now.Icons.Any(i => i.Aliases.Length > 0), Is.True, "the alias-only bundle has aliases in it");
                Assert.That(now.Icons.All(i => i.Tags.Length == 0), Is.True, "and nothing else");

                var moved = new List<string>();

                foreach (var table in VocabularyTests.StandardTables)
                {
                    var then = IconSearch.SeedFor(was.Icons, table);
                    var since = IconSearch.SeedFor(now.Icons, table);
                    if (then != since) moved.Add(table + ": " + then + " -> " + since);
                }

                Assert.That(moved, Is.Empty, "these tables now seed something else: " + string.Join(", ", moved));
            }
        }

        [Test]
        public void A_line_this_build_cannot_place_is_skipped_rather_than_refused()
        {
            // The opposite of what the drawing index does with the same situation. A stale
            // fingerprint is a confident wrong answer, so that file is refused whole; a stale
            // name is a word nothing ever matches, and refusing the file over it would cost
            // every other name in it.
            var doctored = "alias\tfontawesome/an-icon-that-went\tformer-name\n"
                           + "songs\tfontawesome/address-card\tnot-a-kind-this-build-knows\n"
                           + "\n"
                           + "alias\tfontawesome/address-card\tcontact-card\tvcard\n";

            using (var stream = new MemoryStream(BundleWith(doctored)))
            using (var catalog = IconCatalog.Load(stream))
            {
                Assert.That(catalog.Icons.Count, Is.EqualTo(_catalog.Icons.Count));
                Assert.That(IconSearch.Rank(catalog.Icons, "vcard").First().Name, Is.EqualTo("address-card"));
            }
        }

        /// <summary>The bundle's vocabulary with everything but the aliases taken out of it.</summary>
        private static string OnlyAliases()
        {
            var kept = Vocabulary()
                .Split('\n')
                .Where(l => l.StartsWith("alias\t", StringComparison.Ordinal))
                .ToList();

            Assert.That(kept, Is.Not.Empty, "the bundle should carry alias lines");
            return string.Join("\n", kept) + "\n";
        }

        /// <summary>The vocabulary the bundled archive carries, as text.</summary>
        private static string Vocabulary()
        {
            using (var source = typeof(IconCatalog).Assembly.GetManifestResourceStream("Icons.Catalog.icons.zip"))
            using (var archive = new ZipArchive(source, ZipArchiveMode.Read))
            {
                var entry = archive.GetEntry(IconCatalog.VocabularyEntryName);
                Assert.That(entry, Is.Not.Null,
                    "the bundle should carry " + IconCatalog.VocabularyEntryName
                    + "; run tools\\build-catalog.ps1");

                using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// The bundled archive again, with its vocabulary replaced by the given text - or left
        /// out altogether when that is null. There is no second library to point a build at, so
        /// a test about a bundle that says something else has to make one.
        /// </summary>
        private static byte[] BundleWith(string vocabulary)
        {
            var made = new MemoryStream();

            using (var source = typeof(IconCatalog).Assembly.GetManifestResourceStream("Icons.Catalog.icons.zip"))
            using (var reading = new ZipArchive(source, ZipArchiveMode.Read))
            using (var writing = new ZipArchive(made, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var entry in reading.Entries)
                {
                    if (entry.FullName == IconCatalog.VocabularyEntryName) continue;

                    var copy = writing.CreateEntry(entry.FullName);
                    using (var from = entry.Open())
                    using (var to = copy.Open())
                        from.CopyTo(to);
                }

                if (vocabulary != null)
                {
                    var copy = writing.CreateEntry(IconCatalog.VocabularyEntryName);
                    using (var to = copy.Open())
                    using (var writer = new StreamWriter(to, new UTF8Encoding(false)))
                        writer.Write(vocabulary);
                }
            }

            return made.ToArray();
        }
    }
}
