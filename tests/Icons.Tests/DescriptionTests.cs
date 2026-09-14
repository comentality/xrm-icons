using System;
using System.Collections.Generic;
using System.Linq;
using Icons.Catalog;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// What the people who drew an icon say it is about.
    ///
    /// Microsoft writes a line of metadata for each of its icons - a shield is security,
    /// protection and safety; a key is security, lock, password, access, keychain - and until
    /// now this tool threw all of it away, because the npm package it fetches carries none of
    /// it and the repository was never asked. These are the words a Dynamics customiser types
    /// and the icon does not have: exactly the gap the hand-written <see cref="IconSearch"/>
    /// synonym map was built to paper over, answered by the set that drew the thing.
    ///
    /// A description is not a name, and this fixture is mostly about keeping that true. It is
    /// worth 60 where a name is worth 100, so an icon described as security can never lead the
    /// grid over an icon actually called that.
    /// </summary>
    [TestFixture]
    public class DescriptionTests
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

        private IconEntry Fluent(string name)
        {
            return _catalog.Icons.First(i => i.Family == IconFamily.Fluent
                                             && i.Name == name
                                             && i.Style == IconStyle.Regular);
        }

        [Test]
        public void The_bundle_carries_what_Microsoft_says_its_icons_are_about()
        {
            Assert.That(Fluent("shield").Tags, Is.EquivalentTo(new[] { "security", "protection", "safety" }),
                "the bundle should carry Fluent's metadata in " + IconCatalog.VocabularyEntryName
                + "; run tools\\build-catalog.ps1");
        }

        [Test]
        public void Most_of_Fluent_has_something_said_about_it()
        {
            var fluent = _catalog.Icons.Where(i => i.Family == IconFamily.Fluent).ToList();
            var described = fluent.Count(i => i.TagWords.Length > 0);

            TestContext.Out.WriteLine(described + " of " + fluent.Count + " Fluent icons are described, "
                                      + "carrying " + fluent.Sum(i => i.TagWords.Length) + " words.");

            Assert.That(described * 100 / fluent.Count, Is.GreaterThan(80),
                "Microsoft writes a metadata file for about nine icons in ten; far fewer than that "
                + "means the folder names stopped matching the icon names");
        }

        [Test]
        public void A_word_the_library_never_names_an_icon_after_now_finds_one()
        {
            // Nothing in six icon sets is called a keychain. Microsoft's own metadata says its
            // key is one, and that is the whole point of reading it.
            Assert.That(_catalog.Icons.Any(i => i.Words.Contains("keychain")), Is.False,
                "if something is now named keychain this test is measuring the wrong thing");

            var found = IconSearch.Rank(_catalog.Icons, "keychain").FirstOrDefault();

            Assert.That(found, Is.Not.Null, "keychain should find something");
            Assert.That(found.Name, Is.EqualTo("key"));
            Assert.That(found.Family, Is.EqualTo(IconFamily.Fluent));
        }

        [Test]
        public void A_description_is_worth_less_than_a_name_and_less_than_this_tools_own_word()
        {
            // 45, under the 50 a curated Dataverse word scores and the 100 a name scores. The
            // icon really is about the word, is really not called it, and what its set says
            // about it is a gloss written for icon browsers rather than for Dataverse.
            Assert.That(IconSearch.Score(Fluent("shield"), new[] { "security" }), Is.EqualTo(45));
            Assert.That(IconSearch.Score(Fluent("shield"), new[] { "shield" }), Is.EqualTo(100));
        }

        [Test]
        public void This_tools_own_vocabulary_still_reaches_the_grid_over_a_description()
        {
            // The measurement that set the bands. Descriptions went in at 60, level with a name
            // prefix, and 27 of the 92 curated words lost their hand-picked answer out of the
            // top of the grid - the curated tier scored 15, below even a substring match, so a
            // description did not outrank it so much as delete it. account is the worst of them:
            // building, briefcase and organization all went, for person icons Microsoft happens
            // to describe as accounts.
            var top = IconSearch.Rank(_catalog.Icons, "account").Select(i => i.Name).Distinct().Take(8).ToList();

            Assert.That(top, Does.Contain("building"), "account should still offer a building: " + string.Join(", ", top));
            Assert.That(top.First(), Does.Contain("account"),
                "and an icon named for the word still leads it");
        }

        [Test]
        public void A_description_never_leads_over_an_icon_really_called_that()
        {
            // Three icons across the six families have security in their name, and Fluent's
            // shield is only described as it. The named ones have to come first, or every
            // search would start answering with something adjacent to what was asked for.
            var ranked = IconSearch.Rank(_catalog.Icons, "security");

            Assert.That(ranked.First().Words, Does.Contain("security"),
                "the grid should lead with an icon actually called security");
            Assert.That(ranked.Any(i => i.Family == IconFamily.Fluent && i.Name == "shield"), Is.True,
                "and the shield should still be in it");

            var leader = ranked.ToList().IndexOf(ranked.First(i => i.Family == IconFamily.Fluent && i.Name == "shield"));
            var named = ranked.ToList().IndexOf(ranked.First(i => i.Words.Contains("security")));
            Assert.That(named, Is.LessThan(leader));
        }

        [Test]
        public void A_description_is_not_a_word_the_grid_shows_or_the_score_charges_for()
        {
            var shield = Fluent("shield");

            Assert.That(shield.DisplayName, Is.EqualTo("Shield"));
            Assert.That(shield.Words, Is.EqualTo(new[] { "shield" }));
            Assert.That(IconSearch.Score(shield, new[] { "shield" }), Is.EqualTo(100),
                "three words are attached to this icon and it pays for none of them");
        }

        [Test]
        public void Nothing_is_described_as_what_it_is_already_called()
        {
            // Dropped at bundle time rather than carried and ignored. A word the name already
            // has cannot raise the score - the name scores higher - and every word kept is
            // another comparison on every keystroke over the whole library.
            var repeated = _catalog.Icons
                .Where(i => i.TagWords.Any(t => i.Words.Contains(t, StringComparer.OrdinalIgnoreCase)))
                .Select(i => i.Name)
                .Distinct()
                .Take(10)
                .ToList();

            Assert.That(repeated, Is.Empty,
                "these carry a word their own name already has: " + string.Join(", ", repeated));
        }

        /// <summary>
        /// The eleven tables whose suggested search got better, and what each of them used to
        /// ask instead.
        ///
        /// SeedFor offers a table's whole name when the whole name answers, and falls back to
        /// one word when it does not. Eleven of the 162 standard tables could not answer whole
        /// before, because no icon was named after both of their words - so each fell back to
        /// the word that carries least. Now the descriptions answer the other word, and the
        /// pair is offered instead: a Recurring Appointment asks for a repeating calendar
        /// rather than for whatever Fluent draws an appointment as, and an Invoice Line asks
        /// for a receipt rather than for a straight line.
        ///
        /// Written down by name because this is a change to what somebody sees in a box they
        /// did not type into, which is the thing the seeding rule is most careful about.
        /// </summary>
        /// <remarks>
        /// The leader is only pinned where it would be worth arguing for. Upstream refreshes its
        /// metadata in bulk every year or two, and an assertion that Document Template leads with
        /// document_lightning would then fail for a reason nobody cares about - what matters
        /// there is that the table asks its whole name and gets something, not which of two
        /// adequate documents comes first. Pass null to say so.
        /// </remarks>
        [TestCase("Recurring Appointment", "appointment", "calendar_arrow_repeat_all")]
        [TestCase("Purchase Order", "order", "cart")]
        [TestCase("Invoice Line", "line", "receipt")]
        [TestCase("Quote Line", "line", "receipt")]
        [TestCase("Resource Group", "group", "people_toolbox")]
        [TestCase("Unit Group", "group", "people_community")]
        [TestCase("Process Session", "session", "flow")]
        [TestCase("Web Resource", "resource", null)]
        [TestCase("Document Location", "location", null)]
        [TestCase("Document Template", "template", null)]
        [TestCase("Work Order", "order", null)]
        public void A_table_that_could_only_ask_half_its_name_now_asks_all_of_it(
            string table, string used, string leads)
        {
            var seed = IconSearch.SeedFor(_catalog.Icons, table);

            Assert.That(seed, Is.EqualTo(table.ToLowerInvariant()),
                table + " should now seed its whole name rather than " + used);

            var first = IconSearch.Rank(_catalog.Icons, seed).FirstOrDefault();
            Assert.That(first, Is.Not.Null, seed + " should find something");

            if (leads != null) Assert.That(first.Name, Is.EqualTo(leads));
        }

        [Test]
        public void The_words_are_shared_by_every_weight_of_one_icon()
        {
            var weights = _catalog.Icons.Where(i => i.Family == IconFamily.Fluent && i.Name == "shield").ToList();

            Assert.That(weights.Count, Is.GreaterThan(1), "Fluent draws a regular and a filled shield");
            foreach (var weight in weights)
                Assert.That(weight.Extra, Is.SameAs(weights[0].Extra));
        }
    }
}
