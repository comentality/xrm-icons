using System.Collections.Generic;
using System.Linq;
using Icons.Logic;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// Reading an app's navigation out of its site map.
    ///
    /// The shapes pinned here are the ones a real environment turned out to have, not the ones
    /// the schema suggests: a row whose icon is named on the row rather than on the table, a row
    /// with no title of its own, and a row pointing at a bitmap the platform serves and this
    /// tool cannot fetch.
    /// </summary>
    [TestFixture]
    public class SiteMapTests
    {
        private static Dictionary<string, SiteMapTable> Tables(params string[] triples)
        {
            var tables = new Dictionary<string, SiteMapTable>(System.StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i + 2 < triples.Length; i += 3)
                tables[triples[i]] = new SiteMapTable { DisplayName = triples[i + 1], VectorIcon = triples[i + 2] };

            return tables;
        }

        private static string Map(string subAreas, string groupTitle = "Customers")
        {
            return "<SiteMap><Area Id=\"a\"><Group Id=\"g\">"
                   + "<Titles><Title LCID=\"1033\" Title=\"" + groupTitle + "\"/></Titles>"
                   + subAreas
                   + "</Group></Area></SiteMap>";
        }

        [Test]
        public void A_row_takes_the_icon_named_on_the_row_over_the_one_on_the_table()
        {
            // The Settings app names an icon on every row. Where it does, that is the one the
            // app shows, and reading only IconVectorName would draw something else.
            var entries = SiteMap.Parse(
                Map("<SubArea Id=\"s\" Entity=\"account\" Icon=\"/WebResources/onTheRow.svg\"/>"),
                Tables("account", "Account", "pub_/icons/onTheTable.svg"));

            var row = entries.Single(e => e.Kind == SiteMapEntryKind.Table);
            Assert.That(row.IconWebResource, Is.EqualTo("onTheRow.svg"));
        }

        [Test]
        public void A_row_with_no_icon_of_its_own_falls_back_to_the_table()
        {
            var entries = SiteMap.Parse(
                Map("<SubArea Id=\"s\" Entity=\"account\"/>"),
                Tables("account", "Account", "pub_/icons/onTheTable.svg"));

            Assert.That(entries.Single(e => e.Kind == SiteMapEntryKind.Table).IconWebResource,
                Is.EqualTo("pub_/icons/onTheTable.svg"));
        }

        [Test]
        public void A_bitmap_the_platform_serves_is_not_offered_as_a_web_resource()
        {
            // /_imgs/ico_18_audit.gif is real, and there is nothing this tool can do with it.
            // An empty answer says so; a name that cannot be fetched would not.
            var entries = SiteMap.Parse(
                Map("<SubArea Id=\"s\" Entity=\"account\" Icon=\"/_imgs/ico_18_audit.gif\"/>"),
                Tables("account", "Account", null));

            Assert.That(entries.Single(e => e.Kind == SiteMapEntryKind.Table).IconWebResource, Is.Empty);
        }

        [Test]
        public void A_row_with_no_title_borrows_the_tables_name()
        {
            var entries = SiteMap.Parse(
                Map("<SubArea Id=\"s\" Entity=\"mspp_website\"/>"),
                Tables("mspp_website", "Website", null));

            Assert.That(entries.Single(e => e.Kind == SiteMapEntryKind.Table).Label, Is.EqualTo("Website"));
        }

        [Test]
        public void A_resource_id_is_never_shown_as_a_name()
        {
            // ResourceId is a key into the platform's own string table. Showing Homepage_SyncError
            // to somebody reads as a bug, so an unknown table's row falls back to its logical name.
            var entries = SiteMap.Parse(
                Map("<SubArea Id=\"s\" Entity=\"syncerror\" ResourceId=\"Homepage_SyncError\"/>"),
                Tables());

            Assert.That(entries.Single(e => e.Kind == SiteMapEntryKind.Table).Label, Is.EqualTo("syncerror"));
        }

        [Test]
        public void A_row_with_no_table_is_a_page_not_a_table()
        {
            var entries = SiteMap.Parse(
                Map("<SubArea Id=\"s\" Url=\"/tools/audit/audit_area.aspx\">"
                    + "<Titles><Title LCID=\"1033\" Title=\"Auditing\"/></Titles></SubArea>"),
                Tables());

            var row = entries.Single(e => e.Kind != SiteMapEntryKind.Group);
            Assert.That(row.Kind, Is.EqualTo(SiteMapEntryKind.Page));
            Assert.That(row.Label, Is.EqualTo("Auditing"));
        }

        [Test]
        public void A_group_appears_only_when_something_is_under_it()
        {
            // A heading over nothing is not a heading, and a preview that shows one is claiming
            // the app has an empty section.
            var withNothing = SiteMap.Parse(Map(""), Tables());
            Assert.That(withNothing, Is.Empty);

            var withSomething = SiteMap.Parse(Map("<SubArea Id=\"s\" Entity=\"account\"/>"),
                Tables("account", "Account", null));

            Assert.That(withSomething.Select(e => e.Kind),
                Is.EqualTo(new[] { SiteMapEntryKind.Group, SiteMapEntryKind.Table }));
        }

        [Test]
        public void The_order_is_the_order_the_app_puts_them_in()
        {
            var entries = SiteMap.Parse(
                Map("<SubArea Id=\"1\" Entity=\"zebra\"/><SubArea Id=\"2\" Entity=\"apple\"/>"),
                Tables("zebra", "Zebra", null, "apple", "Apple", null));

            Assert.That(entries.Where(e => e.Kind == SiteMapEntryKind.Table).Select(e => e.Label),
                Is.EqualTo(new[] { "Zebra", "Apple" }));
        }

        [Test]
        public void A_site_map_that_will_not_parse_is_an_empty_navigation()
        {
            // A preview that cannot be drawn is a preview that is not shown, not a tool that
            // stops in the middle of somebody's work.
            Assert.That(SiteMap.Parse("<SiteMap><Area>", null), Is.Empty);
            Assert.That(SiteMap.Parse("", null), Is.Empty);
            Assert.That(SiteMap.Parse(null, null), Is.Empty);
        }

        [Test]
        public void A_versioned_web_resource_path_is_read_back_to_its_name()
        {
            Assert.That(SiteMap.WebResource("/%7B637%7D/WebResources/pub_/icons/account.svg"),
                Is.EqualTo("pub_/icons/account.svg"));
            Assert.That(SiteMap.WebResource("/WebResources/account.svg"), Is.EqualTo("account.svg"));
            Assert.That(SiteMap.WebResource("pub_/icons/account.svg"), Is.EqualTo("pub_/icons/account.svg"));
        }
    }
}
