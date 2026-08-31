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
        public void Tokenizing_drops_punctuation_and_case()
        {
            Assert.That(IconSearch.Tokenize("Person, Add-Circle"), Is.EqualTo(new[] { "person", "add", "circle" }));
            Assert.That(IconSearch.Tokenize(""), Is.Empty);
        }
    }
}
