using System;
using System.Collections.Generic;
using System.Linq;

namespace Icons.Catalog
{
    /// <summary>
    /// Ranks the library against what somebody typed.
    ///
    /// Two things make this more than a substring filter. Fluent names icons after what they
    /// depict - person, building_bank - while Dataverse names tables after what they mean:
    /// account, contact, lead. Nobody typing "account" wants zero results. And a bare substring
    /// match puts person_accounts above person for the query "person", which is backwards.
    /// </summary>
    public static class IconSearch
    {
        /// <summary>
        /// Words a Dynamics customiser types, and the words Fluent draws them under. One-way on
        /// purpose: it exists to stop the obvious searches returning nothing, not to be a
        /// thesaurus, and a synonym hit never outranks an icon actually called that - nor one
        /// whose name merely starts with the word - unless <see cref="NamedOtherwise"/> says
        /// that name means something else.
        ///
        /// It is longer than it looks like it should be because it was measured rather than
        /// guessed. Running 162 standard Dataverse tables through <see cref="SeedFor"/> left a
        /// third of them with no suggestion at all - Employee, Supplier, Budget, Milestone -
        /// while the bundle held a perfectly good icon for every one of them. The gap was never
        /// the library. Add to it when a search that should work does not, and check the word
        /// you are adding matches something first: an alias the bundle does not draw is dead
        /// weight that reads like coverage.
        /// </summary>
        private static readonly Dictionary<string, string[]> Synonyms =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // What the first pass covered: the sales and service tables, and the customisation surface.
            { "account",        new[] { "building", "briefcase", "organization" } },
            { "contact",        new[] { "person", "card" } },
            { "customer",       new[] { "person", "people", "building" } },
            { "user",           new[] { "person" } },
            { "team",           new[] { "people" } },
            { "lead",           new[] { "target", "trending", "ribbon" } },
            { "opportunity",    new[] { "money", "trophy", "chart", "trending" } },
            { "case",           new[] { "headset", "ticket", "briefcase" } },
            { "incident",       new[] { "warning", "headset", "ticket" } },
            { "quote",          new[] { "document", "receipt" } },
            { "order",          new[] { "cart", "receipt", "clipboard" } },
            { "invoice",        new[] { "receipt", "money", "document" } },
            { "product",        new[] { "box", "cube", "tag" } },
            { "price",          new[] { "tag", "money" } },
            { "appointment",    new[] { "calendar", "clock" } },
            { "activity",       new[] { "history", "list" } },
            { "task",           new[] { "checkbox", "checkmark", "list" } },
            { "email",          new[] { "mail" } },
            { "call",           new[] { "phone" } },
            { "campaign",       new[] { "megaphone" } },
            { "competitor",     new[] { "people", "swap", "trophy" } },
            { "territory",      new[] { "globe", "map", "location" } },
            { "site",           new[] { "building", "location" } },
            { "resource",       new[] { "wrench", "toolbox" } },
            { "note",           new[] { "document" } },
            { "attachment",     new[] { "attach", "paperclip" } },
            { "report",         new[] { "chart", "document" } },
            { "dashboard",      new[] { "board", "chart" } },
            { "workflow",       new[] { "flow", "branch" } },
            { "solution",       new[] { "puzzle", "box" } },
            { "security",       new[] { "shield", "lock" } },
            { "setting",        new[] { "settings", "options", "wrench" } },
            { "table",          new[] { "grid" } },
            { "entity",         new[] { "table", "grid", "database" } },
            { "field",          new[] { "textbox", "form" } },

            // The rest came from measuring 162 standard tables against the library. A third of them seeded
            // nothing, not because Fluent lacks an icon but because it does not name one after the word
            // Dataverse uses. Anything added here must name a word the bundle actually draws.

            // Who and where.
            { "employee",       new[] { "person", "badge" } },
            { "student",        new[] { "hat", "person" } },
            { "supplier",       new[] { "building", "box" } },
            { "vendor",         new[] { "building", "box" } },
            { "department",     new[] { "building", "people" } },
            { "facility",       new[] { "building" } },
            { "warehouse",      new[] { "building", "box" } },
            { "property",       new[] { "building", "home" } },
            { "publisher",      new[] { "building", "book" } },

            // Money.
            { "discount",       new[] { "tag", "percent" } },
            { "expense",        new[] { "money", "receipt" } },
            { "budget",         new[] { "money", "calculator" } },
            { "commission",     new[] { "money", "percent" } },
            { "loan",           new[] { "money", "building" } },
            { "forecast",       new[] { "chart", "trending" } },
            { "subscription",   new[] { "card", "calendar" } },
            { "membership",     new[] { "card", "badge" } },
            { "payment",        new[] { "money", "card" } },

            // Paperwork.
            { "agreement",      new[] { "handshake", "document" } },
            { "policy",         new[] { "shield", "document" } },
            { "claim",          new[] { "shield", "document" } },
            { "application",    new[] { "document", "form" } },
            { "license",        new[] { "certificate", "key" } },
            { "permit",         new[] { "certificate", "document" } },
            { "entitlement",    new[] { "ticket", "shield" } },
            { "literature",     new[] { "book", "document" } },
            { "article",        new[] { "document", "book" } },
            { "knowledge",      new[] { "book", "lightbulb" } },
            { "course",         new[] { "book", "hat" } },
            { "annotation",     new[] { "note", "pin" } },
            { "subject",        new[] { "folder", "bookmark" } },

            // Work being tracked.
            { "goal",           new[] { "target", "trophy", "flag" } },
            { "milestone",      new[] { "flag" } },
            { "metric",         new[] { "gauge", "chart" } },
            { "booking",        new[] { "calendar", "clock" } },
            { "enrollment",     new[] { "person", "checkmark" } },
            { "shipment",       new[] { "box", "vehicle" } },
            { "delivery",       new[] { "box", "vehicle" } },
            { "inventory",      new[] { "box", "clipboard" } },
            { "inspection",     new[] { "search", "checkmark" } },
            { "finding",        new[] { "search", "warning" } },
            { "issue",          new[] { "warning", "bug" } },
            { "characteristic", new[] { "tag", "star" } },

            // Platform and customisation words.
            { "plugin",         new[] { "puzzle", "plug" } },
            { "connection",     new[] { "link", "plug" } },
            { "relationship",   new[] { "link", "branch" } },
            { "choice",         new[] { "list", "checkmark" } },
            { "audit",          new[] { "clipboard", "search" } },
            { "trace",          new[] { "bug", "list" } },
            { "log",            new[] { "list", "document" } },
            { "session",        new[] { "clock", "flow" } },
            { "rollup",         new[] { "sum", "chart" } },

            // Words the seeding rule stranded. Each of these used to seed on a substring the
            // library was never asked for - Business Unit on a shelving unit, Rating Model on a
            // box model, Time Entry on a brand called Sentry - and now falls back to the word
            // before it. These are the seven tables where there was no word before it either.
            { "app",            new[] { "apps", "grid", "window" } },
            { "business",       new[] { "building", "briefcase" } },
            { "duplicate",      new[] { "copy", "stamp" } },
            { "rating",         new[] { "star" } },
            { "time",           new[] { "clock", "timer" } },

            // Two words that were seeding on a collision rather than on a meaning. A Work Order
            // found document_border_print - "order" inside b-order, and "work" out of that icon's
            // description, where it means paper work - and a Unit Group found people_community,
            // "unit" inside comm-unit-y, when a unit group is a unit of measure. Both now have a
            // word of their own, so the table asks about what it is rather than about what its
            // letters happen to spell.
            { "work",           new[] { "clipboard", "wrench", "toolbox" } },
            { "unit",           new[] { "box", "scale", "ruler" } },
            { "group",          new[] { "people", "multiple", "stack" } },
        };

        /// <summary>
        /// Dataverse words that some bundled family names an icon after in another sense. Each
        /// is a word in <see cref="Synonyms"/> too, and on these alone the vocabulary outranks
        /// the name: an icon called the word, or starting with it, scores no more than one that
        /// merely contains it, so the curated answers lead and the other sense follows them.
        ///
        /// Kept by hand because nothing in the catalog could keep it. Only Fluent says what its
        /// icons depict, so Phosphor's wooden log and Phosphor's user are the same to the search:
        /// a name, and nothing else. Of the sixteen Dataverse words the newer families name
        /// outright, fourteen are drawn in the Dataverse sense - user, invoice, goal, milestone -
        /// so distrusting a name, or a family, would have cost more than these six. Add a word
        /// only after looking at every icon named after it: the cap falls on all of them.
        /// </summary>
        private static readonly HashSet<string> NamedOtherwise =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "quote",    // quotation marks in Fluent, Lucide, Tabler and Font Awesome
            "log",      // a wooden log in Phosphor; log-in and log-out in Lucide
            "rating",   // a content rating - mature, 12+, 18+ - in Fluent and Tabler
            "security", // a CCTV camera in Fluent and Phosphor
            "order",    // a drag handle for sorting, re_order, in Fluent; menu-order in Tabler
            "unit",     // a bookcase, shelving-unit, in Lucide; Phosphor's unite is a set union
        };

        /// <summary>
        /// The icons matching the query, best first. An empty query returns the whole library in
        /// its natural order rather than nothing, so the grid is never blank.
        /// </summary>
        public static IReadOnlyList<IconEntry> Rank(IReadOnlyList<IconEntry> icons, string query)
        {
            if (icons == null) throw new ArgumentNullException(nameof(icons));

            var terms = Tokenize(query);
            if (terms.Count == 0) return icons;

            var scored = new List<KeyValuePair<int, IconEntry>>();
            for (var i = 0; i < icons.Count; i++)
            {
                var score = Score(icons[i], terms);
                if (score > 0) scored.Add(new KeyValuePair<int, IconEntry>(score, icons[i]));
            }

            // Ties broken by family, then by the shorter name, then alphabetically, so the
            // ordering is stable and person outranks person_available at equal relevance.
            //
            // Family outranks the name length rather than the other way round, and that is a
            // decision rather than a detail. Four families draw a folder and all four score the
            // same, so something has to choose; and past the exact hits the score is a tie
            // between whole rows of icons, where the shorter name would otherwise decide. Lucide
            // names things tersely - folder-x, folder-up - and Fluent does not, so on length
            // alone Fluent loses its own grid to whichever family writes shortest. Fluent leads
            // each band of equal relevance, the others follow it in turn, and the family filter
            // is how somebody asks for one of them instead.
            return scored
                .OrderByDescending(p => p.Key)
                .ThenBy(p => p.Value.Family)
                .ThenBy(p => p.Value.Name.Length)
                .ThenBy(p => p.Value.Name, StringComparer.Ordinal)
                .ThenBy(p => p.Value.Style)
                .Select(p => p.Value)
                .ToList();
        }

        /// <summary>
        /// The search a table's own name is worth typing, or "" when the library has no answer
        /// to it. Picking the Account table should show the buildings without anybody typing
        /// "account" first, and that is all this is for.
        ///
        /// Every term has to hit for a query to return anything, so the whole name is only worth
        /// offering when the whole name matches: "Sales Order" would show nothing, because
        /// Fluent draws no sales. The fallback is the last word, then the ones before it, because
        /// an English name of two words is usually the second one - an order that is about sales,
        /// a unit that is about business. A name that asks for nothing the library has gets ""
        /// rather than a query with no results, which reads worse than an untouched box.
        /// </summary>
        public static string SeedFor(IReadOnlyList<IconEntry> icons, string tableName)
        {
            if (icons == null) throw new ArgumentNullException(nameof(icons));

            var terms = Tokenize(tableName);
            if (terms.Count == 0) return "";

            if (Answers(icons, terms)) return string.Join(" ", terms);

            for (var i = terms.Count - 1; i >= 0; i--)
            {
                if (!WorthSeeding(icons, terms[i])) continue;

                var single = new[] { terms[i] };
                if (Answers(icons, single)) return terms[i];
            }

            return "";
        }

        /// <summary>
        /// Whether a single word is one this library can be trusted to answer, rather than one
        /// it merely produces results for.
        ///
        /// The difference is what a hit is made of. <see cref="Score"/> takes a prefix or a
        /// substring, which is right for somebody typing - "fold" should reach folder - and
        /// wrong for a word nobody typed: Payment Term finds <c>terminal</c>, Hierarchy Rule
        /// finds <c>ruler</c>, Social Profile finds <c>vehicle_car_profile</c>, and Time Entry
        /// finds a brand called Sentry. Each is a confident wrong answer in a box the user did
        /// not fill in, which reads worse than the box left alone.
        ///
        /// So the word has to be one of two things, and a number would decide neither of them:
        /// literal, meaning some icon is called exactly that and the library really does draw
        /// the thing - or curated, meaning <see cref="Synonyms"/> says what this Dataverse word
        /// looks like. Anything else, and the search moves on to the word before it: Rating
        /// Model asks about ratings rather than about box models.
        /// </summary>
        private static bool WorthSeeding(IReadOnlyList<IconEntry> icons, string term)
        {
            if (Synonyms.ContainsKey(term)) return true;

            for (var i = 0; i < icons.Count; i++)
                if (string.Equals(icons[i].Name, term, StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        /// <summary>Whether anything in the library answers these terms at all.</summary>
        private static bool Answers(IReadOnlyList<IconEntry> icons, IReadOnlyList<string> terms)
        {
            for (var i = 0; i < icons.Count; i++)
                if (Score(icons[i], terms) > 0) return true;

            return false;
        }

        /// <summary>Splits a query into lowercase words. Punctuation and case are noise here.</summary>
        public static List<string> Tokenize(string query)
        {
            var terms = new List<string>();
            if (string.IsNullOrWhiteSpace(query)) return terms;

            var separators = new[] { ' ', '\t', '_', '-', ',', '.', '/', '\\' };
            foreach (var raw in query.Split(separators, StringSplitOptions.RemoveEmptyEntries))
            {
                var term = raw.Trim().ToLowerInvariant();
                if (term.Length > 0) terms.Add(term);
            }
            return terms;
        }

        /// <summary>
        /// How well one icon answers the query. Zero means it does not. Every term has to hit
        /// something: answering "person add" with icons that are only ever a person is a worse
        /// answer than admitting there is no match.
        /// </summary>
        public static int Score(IconEntry icon, IReadOnlyList<string> terms)
        {
            if (icon == null) throw new ArgumentNullException(nameof(icon));
            if (terms == null || terms.Count == 0) return 1;

            var total = 0;
            foreach (var term in terms)
            {
                var best = ScoreTerm(icon, term);
                if (best == 0) return 0;
                total += best;
            }

            // Prefer an icon whose whole name the query accounts for: "person" should lead with
            // person, not person_money. Words the query never mentioned cost a little.
            var unmatched = icon.Words.Length - terms.Count;
            if (unmatched > 0) total -= Math.Min(unmatched * 2, 20);

            return Math.Max(total, 1);
        }

        /// <summary>
        /// The best any of these words does against one term: named exactly, named as a prefix,
        /// or merely containing it. Never below <paramref name="floor"/>, which is what some
        /// other set of words already scored.
        /// </summary>
        private static int Ladder(string[] words, string term, int floor)
        {
            var best = floor;

            foreach (var word in words)
            {
                if (string.Equals(word, term, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 100);
                else if (word.StartsWith(term, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 60);
                else if (word.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) best = Math.Max(best, 30);
            }

            return best;
        }

        /// <summary>
        /// The best any description does against one term. A shorter ladder than a name's, and a
        /// lower one: described exactly is 45 where a name would score 100 and a curated word 50,
        /// and a description merely starting with the word is 25. Nothing for a description that
        /// only contains it - an icon described as ''accessibility'' is not an answer to ''ilit''.
        /// </summary>
        private static int Described(string[] words, string term, int floor)
        {
            var best = floor;

            foreach (var word in words)
            {
                if (string.Equals(word, term, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 45);
                else if (word.StartsWith(term, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 25);
            }

            return best;
        }

        /// <summary>A name with its separators taken out: arrow_trending_lines as arrowtrendinglines.</summary>
        private static string Squashed(string name)
        {
            return name.Replace("_", "").Replace("-", "");
        }

        private static int ScoreTerm(IconEntry icon, string term)
        {
            var best = Ladder(icon.Words, term, 0);

            // An alias is worth exactly what the name is worth, because it is a name: one the
            // set published for this same drawing and later stopped leading with. Font Awesome
            // ships address-card as contact-card and as vcard too, and the bundle keeps one
            // file per drawing - so without this, the two names the set still answers to find
            // nothing here, and the picture they name sits in the grid under a third word.
            if (best < 100) best = Ladder(icon.AliasWords, term, best);

            // Except where the vocabulary knows the name means something else. Quote is a price
            // offered to a customer and Lucide draws quotation marks under it; such a name is
            // worth what a substring is, 30, under the curated answer and the description both,
            // so it stays in the grid and stops leading it. See NamedOtherwise.
            if (best > 30 && NamedOtherwise.Contains(term)) best = 30;

            // And a description is worth less than a name and less than this tool's own
            // vocabulary. It is a true thing about the drawing, said by the people who drew it -
            // Microsoft's own metadata says a shield is about security, protection and safety -
            // so it beats an icon that merely has the letters in it somewhere. It is still a
            // gloss written for icon browsers rather than for Dataverse, which is why 45 sits
            // under the 50 a curated word scores. That order was measured rather than argued:
            // at 60 a description outranked the curated map, and 27 of its 92 words lost their
            // hand-picked answer out of the top of the grid - account stopped offering a
            // building, a briefcase or an organization, and product ranked a Surface tablet
            // above a box. See docs/search-vocabulary.md.
            if (best < 45) best = Described(icon.TagWords, term, best);

            // A term that spans the separators, so "arrowtrending" and "arrow trending" agree,
            // and "shoppingcart" finds shopping-cart.
            if (best == 0 && Squashed(icon.Name).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                best = 20;

            if (best == 0)
            {
                foreach (var alias in icon.Aliases)
                    if (Squashed(alias).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) { best = 20; break; }
            }

            // Only when the icon says nothing about the word itself, in its name or in what its
            // set says about it. This is the tool's own vocabulary rather than anybody else's,
            // measured against 162 real tables, and it is the best thing here about what a
            // Dataverse word means - so it outranks a description. It still never outranks the
            // icon actually called the word, nor one whose name merely starts with it, unless
            // that name is one NamedOtherwise has already pushed down to a substring's 30.
            if (best == 0)
            {
                string[] mapped;
                if (Synonyms.TryGetValue(term, out mapped))
                {
                    foreach (var alias in mapped)
                    {
                        foreach (var word in icon.Words)
                        {
                            if (string.Equals(word, alias, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 50);
                            else if (word.StartsWith(alias, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 35);
                        }
                    }
                }
            }

            return best;
        }
    }
}
