using System.Collections.Generic;
using System.Linq;
using Icons.Catalog;
using NUnit.Framework;

namespace Icons.Tests
{
    [TestFixture]
    public class SearchTests
    {
        private static readonly IReadOnlyList<IconEntry> Library = new[]
        {
            new IconEntry("person", IconStyle.Regular),
            new IconEntry("person_add", IconStyle.Regular),
            new IconEntry("person_accounts", IconStyle.Regular),
            new IconEntry("people_team", IconStyle.Regular),
            new IconEntry("building_bank", IconStyle.Regular),
            new IconEntry("building", IconStyle.Filled),
            new IconEntry("arrow_trending_lines", IconStyle.Regular),
            new IconEntry("calendar_clock", IconStyle.Regular),
            new IconEntry("mail_read", IconStyle.Regular),
            new IconEntry("cart", IconStyle.Regular),
        };

        private static List<string> Names(string query)
        {
            return IconSearch.Rank(Library, query).Select(i => i.Name).ToList();
        }

        [Test]
        public void An_empty_query_returns_the_whole_library()
        {
            Assert.That(IconSearch.Rank(Library, "").Count, Is.EqualTo(Library.Count));
            Assert.That(IconSearch.Rank(Library, null).Count, Is.EqualTo(Library.Count));
            Assert.That(IconSearch.Rank(Library, "   ").Count, Is.EqualTo(Library.Count));
        }

        [Test]
        public void The_exact_name_leads()
        {
            // The bug this pins: a substring filter puts person_accounts above person.
            Assert.That(Names("person").First(), Is.EqualTo("person"));
        }

        [Test]
        public void Every_term_has_to_match_something()
        {
            var results = Names("person add");

            Assert.That(results, Does.Contain("person_add"));
            Assert.That(results, Does.Not.Contain("person"), "person says nothing about add");
        }

        [Test]
        public void Word_order_does_not_matter()
        {
            Assert.That(Names("add person").First(), Is.EqualTo("person_add"));
        }

        [Test]
        public void A_query_can_span_the_underscores()
        {
            Assert.That(Names("arrowtrending"), Does.Contain("arrow_trending_lines"));
        }

        [Test]
        public void Separators_in_the_query_are_ignored()
        {
            Assert.That(Names("calendar-clock"), Does.Contain("calendar_clock"));
            Assert.That(Names("calendar_clock"), Does.Contain("calendar_clock"));
        }

        [Test]
        public void Case_does_not_matter()
        {
            Assert.That(Names("PERSON").First(), Is.EqualTo("person"));
        }

        [Test]
        public void Dataverse_words_find_the_icons_Fluent_draws_them_as()
        {
            // Nobody browsing for an account icon searches for "building". This is the whole
            // reason the synonym table exists.
            Assert.That(Names("account"), Does.Contain("building_bank"));
            Assert.That(Names("contact"), Does.Contain("person"));
            Assert.That(Names("email"), Does.Contain("mail_read"));
            Assert.That(Names("team"), Does.Contain("people_team"));
            Assert.That(Names("appointment"), Does.Contain("calendar_clock"));
        }

        [Test]
        public void A_synonym_never_outranks_the_icon_actually_called_that()
        {
            // "account" maps to building, but person_accounts is literally named for it.
            var results = Names("account");

            Assert.That(results.First(), Is.EqualTo("person_accounts"));
        }

        [Test]
        public void A_query_that_matches_nothing_returns_nothing()
        {
            Assert.That(Names("zzzznotanicon"), Is.Empty);
        }

        [Test]
        public void Shorter_names_win_ties_so_the_order_is_stable()
        {
            var results = Names("building");

            Assert.That(results.First(), Is.EqualTo("building"));
            Assert.That(results, Does.Contain("building_bank"));
        }

        [Test]
        public void A_table_name_seeds_the_search_with_itself()
        {
            Assert.That(IconSearch.SeedFor(Library, "Account"), Is.EqualTo("account"));
            Assert.That(IconSearch.SeedFor(Library, "Contact"), Is.EqualTo("contact"));
        }

        [Test]
        public void A_two_word_table_falls_back_to_the_word_the_library_knows()
        {
            // Every term has to hit, and nothing here is a sale. The whole name would show an
            // empty grid, so the seed is the word that means something: the order.
            Assert.That(IconSearch.SeedFor(Library, "Sales Order"), Is.EqualTo("order"));

            // And the last word is only a preference, not a rule: fall further back when it
            // turns out to be the empty one.
            Assert.That(IconSearch.SeedFor(Library, "Person Zzzznotanicon"), Is.EqualTo("person"));
        }

        [Test]
        public void A_whole_name_the_library_answers_is_kept_whole()
        {
            Assert.That(IconSearch.SeedFor(Library, "Person Add"), Is.EqualTo("person add"));
        }

        [Test]
        public void A_table_the_library_says_nothing_about_seeds_nothing()
        {
            // Better an untouched search box than a query that shows an empty grid.
            Assert.That(IconSearch.SeedFor(Library, "Zzzznotanicon"), Is.Empty);
            Assert.That(IconSearch.SeedFor(Library, ""), Is.Empty);
            Assert.That(IconSearch.SeedFor(Library, null), Is.Empty);
        }

        [Test]
        public void A_custom_tables_prefix_does_not_become_the_search()
        {
            // A table with no display name falls back to its schema name, prefix and all.
            Assert.That(IconSearch.SeedFor(Library, "cr123_person"), Is.EqualTo("person"));
        }

        [Test]
        public void Tokenizing_drops_punctuation_and_case()
        {
            Assert.That(IconSearch.Tokenize("Person, Add-Circle"), Is.EqualTo(new[] { "person", "add", "circle" }));
            Assert.That(IconSearch.Tokenize(""), Is.Empty);
        }
    }
}
