using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Icons.Catalog;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// What a keystroke costs.
    ///
    /// Search runs over the whole library on every character typed, and the grid is redrawn from
    /// what it returns, so this is the one number that decides whether the box keeps up with a
    /// person typing or lags a letter behind them. It matters now because the library is being
    /// given words it did not have - upstream names and upstream descriptions - and every one of
    /// them is another comparison in the inner loop.
    ///
    /// The ceiling is deliberately far above the real figures. This is not here to police a
    /// millisecond on somebody's laptop; it is here so that a change which makes search several
    /// times slower is caught by a test rather than by a user, and so that the figures are
    /// printed on every run for whoever is about to add the next set of words.
    ///
    /// <b>Do not compare two runs of this to each other.</b> The figures move by a factor of two
    /// depending on whether the fixture ran alone or alongside the other three hundred tests, and
    /// that was learned the hard way: adding Fluent's vocabulary looked like it had doubled the
    /// cost of a keystroke, and then like it had made search faster, and it had done neither. To
    /// weigh one library against another, load both into one process and time them alternately,
    /// round by round, so the machine's mood falls on both equally. This test answers one
    /// question only - whether search is fast enough right now.
    /// </summary>
    [TestFixture]
    public class SearchSpeedTests
    {
        private const int Ceiling = 150;
        private const int Runs = 12;

        private IconCatalog _catalog;

        [OneTimeSetUp]
        public void LoadCatalog()
        {
            _catalog = IconCatalog.Load();

            // The first query pays for the JIT and for touching every entry once. Nobody types
            // into a cold process, and timing one would measure the wrong thing.
            IconSearch.Rank(_catalog.Icons, "warm");
        }

        [OneTimeTearDown]
        public void DisposeCatalog()
        {
            _catalog?.Dispose();
        }

        /// <summary>The queries worth timing, and what each of them is the worst case for.</summary>
        private static readonly KeyValuePair<string, string>[] Queries =
        {
            new KeyValuePair<string, string>("a", "one letter, which matches most of the library"),
            new KeyValuePair<string, string>("person", "a whole word, the ordinary case"),
            new KeyValuePair<string, string>("arrow right", "two words, so every entry is scored twice"),
            new KeyValuePair<string, string>("security", "a word Fluent mostly does not name an icon after"),
            new KeyValuePair<string, string>("zzzznotanicon", "a miss, which cannot stop early"),
        };

        [Test]
        public void A_keystroke_is_answered_in_time_to_be_typed_over()
        {
            var slow = new List<string>();

            TestContext.Out.WriteLine(_catalog.Icons.Count + " icons, "
                                      + Words(i => i.Words) + " words, "
                                      + Words(i => i.AliasWords) + " alias words and "
                                      + Words(i => i.TagWords) + " described words an icon.");

            foreach (var query in Queries)
            {
                var clock = new Stopwatch();
                var hits = 0;

                for (var run = 0; run < Runs; run++)
                {
                    clock.Start();
                    hits = IconSearch.Rank(_catalog.Icons, query.Key).Count;
                    clock.Stop();
                }

                var each = clock.Elapsed.TotalMilliseconds / Runs;
                TestContext.Out.WriteLine(string.Format("{0,-16} {1,7:0.0} ms  {2,6} hits   {3}",
                    "\"" + query.Key + "\"", each, hits, query.Value));

                if (each > Ceiling) slow.Add(query.Key + " took " + each.ToString("0.0") + " ms");
            }

            Assert.That(slow, Is.Empty,
                "search is meant to keep up with typing, and these did not: " + string.Join(", ", slow)
                + ". The figures above say how far off it is; the usual cause is words added to the "
                + "library without being counted first.");
        }

        private double Words(Func<IconEntry, string[]> of)
        {
            return Math.Round(_catalog.Icons.Sum(i => (double)of(i).Length) / _catalog.Icons.Count, 2);
        }
    }
}
