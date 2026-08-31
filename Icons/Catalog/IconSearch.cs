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
        /// Words a Dynamics customiser types, and the words Fluent draws them under. Deliberately
        /// one-way and deliberately short: it exists to stop the obvious searches returning
        /// nothing, not to be a thesaurus. Add to it when a search that should work does not.
        /// </summary>
        private static readonly Dictionary<string, string[]> Synonyms =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "account",     new[] { "building", "briefcase", "organization" } },
            { "contact",     new[] { "person", "card" } },
            { "customer",    new[] { "person", "people", "building" } },
            { "user",        new[] { "person" } },
            { "team",        new[] { "people" } },
            { "lead",        new[] { "target", "trending", "ribbon" } },
            { "opportunity", new[] { "money", "trophy", "chart", "trending" } },
            { "case",        new[] { "headset", "ticket", "briefcase" } },
            { "incident",    new[] { "warning", "headset", "ticket" } },
            { "quote",       new[] { "document", "receipt" } },
            { "order",       new[] { "cart", "receipt", "clipboard" } },
            { "invoice",     new[] { "receipt", "money", "document" } },
            { "product",     new[] { "box", "cube", "tag" } },
            { "price",       new[] { "tag", "money" } },
            { "appointment", new[] { "calendar", "clock" } },
            { "activity",    new[] { "history", "list" } },
            { "task",        new[] { "checkbox", "checkmark", "list" } },
            { "email",       new[] { "mail" } },
            { "call",        new[] { "phone" } },
            { "campaign",    new[] { "megaphone" } },
            { "competitor",  new[] { "people", "swap", "trophy" } },
            { "territory",   new[] { "globe", "map", "location" } },
            { "site",        new[] { "building", "location" } },
            { "resource",    new[] { "wrench", "toolbox" } },
            { "note",        new[] { "document" } },
            { "attachment",  new[] { "attach", "paperclip" } },
            { "report",      new[] { "chart", "document" } },
            { "dashboard",   new[] { "board", "chart" } },
            { "workflow",    new[] { "flow", "branch" } },
            { "solution",    new[] { "puzzle", "box" } },
            { "security",    new[] { "shield", "lock" } },
            { "setting",     new[] { "settings", "options", "wrench" } },
            { "table",       new[] { "grid" } },
            { "entity",      new[] { "table", "grid", "database" } },
            { "field",       new[] { "textbox", "form" } },
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

            // Ties broken by the shorter name, then alphabetically, so the ordering is stable
            // and person outranks person_available at equal relevance.
            return scored
                .OrderByDescending(p => p.Key)
                .ThenBy(p => p.Value.Name.Length)
                .ThenBy(p => p.Value.Name, StringComparer.Ordinal)
                .ThenBy(p => p.Value.Style)
                .Select(p => p.Value)
                .ToList();
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

        private static int ScoreTerm(IconEntry icon, string term)
        {
            var best = 0;

            foreach (var word in icon.Words)
            {
                if (string.Equals(word, term, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 100);
                else if (word.StartsWith(term, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 60);
                else if (word.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) best = Math.Max(best, 30);
            }

            // A term that spans the underscores, so "arrowtrending" and "arrow trending" agree.
            if (best == 0 && icon.Name.Replace("_", "").IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                best = 20;

            // Only when the icon says nothing about the word itself. A synonym hit is a hint, not
            // a match, and must never outrank the icon actually called that.
            if (best == 0)
            {
                string[] mapped;
                if (Synonyms.TryGetValue(term, out mapped))
                {
                    foreach (var alias in mapped)
                    {
                        foreach (var word in icon.Words)
                        {
                            if (string.Equals(word, alias, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 15);
                            else if (word.StartsWith(alias, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 10);
                        }
                    }
                }
            }

            return best;
        }
    }
}
