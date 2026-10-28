using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Icons.Catalog;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// A Dataverse word that a family has drawn in another sense. Quote is a price offered to a
    /// customer, and Lucide and Tabler both draw quotation marks under that name; a log is a
    /// record of what happened, and Phosphor draws a piece of wood. Both are icons really called
    /// the word, so both scored as a name does and led the grid.
    ///
    /// Nothing in the catalog tells the two senses apart. Only Fluent says what its icons depict,
    /// so Phosphor's wooden log and Phosphor's user look exactly alike to the search: a name and
    /// nothing else. Distrusting a name, or distrusting the newer families, would have cost the
    /// fourteen words those families answer rightly - user, invoice, goal, milestone - so the
    /// knowledge is kept where the rest of it is, in the vocabulary, by hand, one word at a time.
    /// </summary>
    [TestFixture]
    public class OtherSenseTests
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

        private static Dictionary<string, string[]> Vocabulary()
        {
            var field = typeof(IconSearch).GetField("Synonyms", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, "IconSearch.Synonyms has been renamed; so has this test's grip on it");
            return (Dictionary<string, string[]>)field.GetValue(null);
        }

        private static ICollection<string> OtherSense()
        {
            var field = typeof(IconSearch).GetField("NamedOtherwise", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, "IconSearch.NamedOtherwise has been renamed; so has this test's grip on it");
            return (ICollection<string>)field.GetValue(null);
        }

        /// <summary>Whether an icon is one of the answers the vocabulary gives for a word.</summary>
        private static bool IsCuratedAnswer(IconEntry icon, string word, Dictionary<string, string[]> vocabulary)
        {
            return vocabulary[word].Any(a => icon.Words.Any(w => w.StartsWith(a, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// The six, each with the icon that used to lead. The other sense is still in the grid -
        /// somebody may want quotation marks - it just no longer answers first.
        /// </summary>
        [TestCase("quote", "quote", "quotation marks, in Lucide and Tabler")]
        [TestCase("log", "log", "a wooden log, in Phosphor")]
        [TestCase("rating", "rating_mature", "a content rating, not a score out of five")]
        [TestCase("security", "video_security", "a CCTV camera")]
        [TestCase("order", "re_order", "a drag handle for sorting")]
        [TestCase("unit", "shelving-unit", "a bookcase, when a unit is a unit of measure")]
        public void A_name_in_another_sense_ranks_below_what_the_word_means(string word, string used, string why)
        {
            var vocabulary = Vocabulary();
            var ranked = IconSearch.Rank(_catalog.Icons, word);

            var top = ranked.Take(8).ToList();
            var strays = top.Where(i => !IsCuratedAnswer(i, word, vocabulary)).Select(i => i.Family + ":" + i.Name).ToList();
            Assert.That(strays, Is.Empty, word + " should lead with what it means, not with " + why);

            var answers = ranked.Count(i => IsCuratedAnswer(i, word, vocabulary));
            var firstStray = ranked.TakeWhile(i => IsCuratedAnswer(i, word, vocabulary)).Count();
            Assert.That(firstStray, Is.GreaterThanOrEqualTo(8), word + ": the curated answers come first");
            Assert.That(answers, Is.GreaterThan(0));

            Assert.That(ranked.Any(i => i.Name == used), Is.True,
                used + " should still be in the grid, further down - it is not wrong to want it");
        }

        /// <summary>
        /// The tables the item was raised for. The seed was right in every one of them; the
        /// ranking was not, and this says the seed did not move to fix it.
        /// </summary>
        [TestCase("Quote", "quote")]
        [TestCase("Rating Model", "rating")]
        [TestCase("Field Security Profile", "security")]
        [TestCase("Work Order Service", "order")]
        [TestCase("Unit", "unit")]
        public void The_seed_is_unchanged_and_now_leads_with_its_meaning(string table, string seed)
        {
            Assert.That(IconSearch.SeedFor(_catalog.Icons, table), Is.EqualTo(seed));

            var first = IconSearch.Rank(_catalog.Icons, seed).First();
            Assert.That(IsCuratedAnswer(first, seed, Vocabulary()), Is.True,
                table + " leads with " + first.Family + ":" + first.Name);
        }

        /// <summary>
        /// And what must not move. Person is the reason an exact hit is worth what it is worth;
        /// the fourteen are the Dataverse words the newer families name outright and draw in the
        /// Dataverse sense; the last four are names a Dataverse word is one part of and still
        /// means what it says.
        /// </summary>
        [TestCase("person", "person")]
        [TestCase("contact", "contact")]
        [TestCase("user", "user")]
        [TestCase("invoice", "invoice")]
        [TestCase("activity", "activity")]
        [TestCase("report", "report")]
        [TestCase("workflow", "workflow")]
        [TestCase("student", "student")]
        [TestCase("warehouse", "warehouse")]
        [TestCase("discount", "discount")]
        [TestCase("license", "license")]
        [TestCase("article", "article")]
        [TestCase("goal", "goal")]
        [TestCase("milestone", "milestone")]
        [TestCase("connection", "connection")]
        [TestCase("account", "person_account")]
        [TestCase("team", "people_team")]
        [TestCase("price", "tag-price")]
        [TestCase("payment", "payment")]
        public void An_exact_name_that_means_the_word_still_leads(string word, string leads)
        {
            Assert.That(IconSearch.Rank(_catalog.Icons, word).First().Name, Is.EqualTo(leads));
        }

        [Test]
        public void Every_word_named_otherwise_has_a_meaning_to_rank_instead()
        {
            // Without one, pushing the name down leaves the grid led by whatever is left - which
            // is worse than the name it pushed down.
            var vocabulary = Vocabulary();
            var orphans = OtherSense().Where(w => !vocabulary.ContainsKey(w)).ToList();

            Assert.That(orphans, Is.Empty, "named otherwise with nothing in Synonyms: " + string.Join(", ", orphans));
        }

        [Test]
        public void Every_word_named_otherwise_is_one_some_icon_is_called()
        {
            // A word on the list that no icon is named after is dead weight that reads like care.
            var dead = OtherSense()
                .Where(w => !_catalog.Icons.Any(i => i.Words.Any(x => string.Equals(x, w, StringComparison.OrdinalIgnoreCase))))
                .ToList();

            Assert.That(dead, Is.Empty, "no icon is named after: " + string.Join(", ", dead));
        }
    }
}
