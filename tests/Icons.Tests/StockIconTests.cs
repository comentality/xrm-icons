using System.Collections.Generic;
using System.Linq;
using Icons.Logic;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// Asking the org which tables an app already draws for itself. Every case here is one a real
    /// environment produces: the address that answers with a drawing, the one that answers 404
    /// because the app has no glyph for that table, and the several ways a network answers 200
    /// with something that is not an icon at all.
    /// </summary>
    [TestFixture]
    public class StockIconTests
    {
        private const string Drawing = "<svg xmlns=\"http://www.w3.org/2000/svg\"><title>Account_EED6</title></svg>";

        [Test]
        public void The_web_application_url_is_the_first_thing_believed()
        {
            Assert.That(StockIcons.OrgAddress("https://contoso.crm.dynamics.com/",
                    "https://elsewhere.example", "https://elsewhere.example/XRMServices/2011/Organization.svc"),
                Is.EqualTo("https://contoso.crm.dynamics.com"));
        }

        [Test]
        public void The_url_it_was_made_from_answers_when_the_web_application_one_does_not()
        {
            Assert.That(StockIcons.OrgAddress(null, "https://contoso.crm.dynamics.com", null),
                Is.EqualTo("https://contoso.crm.dynamics.com"));
            Assert.That(StockIcons.OrgAddress("   ", "https://contoso.crm.dynamics.com", null),
                Is.EqualTo("https://contoso.crm.dynamics.com"));
        }

        /// <summary>
        /// The last resort, and the one a connection always has: the endpoint the SDK talks to,
        /// with the endpoint cut off.
        /// </summary>
        [Test]
        public void The_service_endpoint_answers_with_its_service_path_cut_off()
        {
            Assert.That(StockIcons.OrgAddress(null, null,
                    "https://contoso.crm.dynamics.com/XRMServices/2011/Organization.svc"),
                Is.EqualTo("https://contoso.crm.dynamics.com"));
        }

        /// <summary>
        /// An on-premise org lives under its organization name, and /_imgs hangs off that rather
        /// than off the host - so the path is kept.
        /// </summary>
        [Test]
        public void An_on_premise_org_keeps_the_name_it_lives_under()
        {
            Assert.That(StockIcons.OrgAddress(null, null,
                    "https://crm.contoso.local/Contoso/XRMServices/2011/Organization.svc"),
                Is.EqualTo("https://crm.contoso.local/Contoso"));
            Assert.That(StockIcons.Address(
                    StockIcons.OrgAddress(null, null,
                        "https://crm.contoso.local/Contoso/XRMServices/2011/Organization.svc"), 1),
                Is.EqualTo("https://crm.contoso.local/Contoso/_imgs/svg_1.svg"));
        }

        [Test]
        public void A_connection_that_can_say_nothing_gets_no_address()
        {
            Assert.That(StockIcons.OrgAddress(null, null, null), Is.Null);
            Assert.That(StockIcons.OrgAddress("", "  ", ""), Is.Null);
        }

        [Test]
        public void The_address_is_the_classic_icon_store_keyed_by_object_type_code()
        {
            Assert.That(StockIcons.Address("https://contoso.crm.dynamics.com", 1),
                Is.EqualTo("https://contoso.crm.dynamics.com/_imgs/svg_1.svg"));
        }

        [Test]
        public void A_trailing_slash_does_not_double_up()
        {
            Assert.That(StockIcons.Address("https://contoso.crm.dynamics.com/", 4212),
                Is.EqualTo("https://contoso.crm.dynamics.com/_imgs/svg_4212.svg"));
        }

        [Test]
        public void An_org_with_no_address_is_asked_nothing()
        {
            Assert.That(StockIcons.Address(null, 1), Is.Null);
            Assert.That(StockIcons.Address("", 1), Is.Null);
        }

        [Test]
        public void An_svg_is_a_drawing()
        {
            Assert.That(StockIcons.IsDrawing(200, "image/svg+xml", 339), Is.True);
            Assert.That(StockIcons.IsDrawing(200, "image/svg+xml; charset=utf-8", 339), Is.True);
        }

        [Test]
        public void A_table_the_app_has_no_glyph_for_is_not_a_drawing()
        {
            Assert.That(StockIcons.IsDrawing(404, "text/html", 28), Is.False);
        }

        /// <summary>
        /// The case that matters most. A proxy sign-in page, a captive portal or a friendly 404
        /// page all come back 200, and believing one would tell the user that every table in the
        /// org is already drawn - hiding the whole list this tool exists to shorten.
        /// </summary>
        [Test]
        public void A_page_that_answers_200_is_not_a_drawing()
        {
            Assert.That(StockIcons.IsDrawing(200, "text/html", 4096), Is.False);
            Assert.That(StockIcons.IsDrawing(200, "application/json", 120), Is.False);
            Assert.That(StockIcons.IsDrawing(200, null, 4096), Is.False);
        }

        [Test]
        public void An_empty_answer_is_not_a_drawing()
        {
            Assert.That(StockIcons.IsDrawing(200, "image/svg+xml", 0), Is.False);
        }

        /// <summary>
        /// The drawing comes back, not a yes: the list needs to know the table is not empty and
        /// the preview needs something to paint, and that is one answer, not two round trips.
        /// </summary>
        [Test]
        public void The_drawing_comes_back_for_the_tables_the_org_answers_for()
        {
            var asked = new List<int>();
            var drawn = StockIcons.DrawnAmong(
                new int?[] { 1, 2, 7103, 10485 },
                code => { asked.Add(code); return code == 1 ? Drawing : null; });

            Assert.That(drawn.Keys, Is.EquivalentTo(new[] { 1 }));
            Assert.That(drawn[1], Is.EqualTo(Drawing));
            Assert.That(asked, Is.EquivalentTo(new[] { 1, 2, 7103, 10485 }));
        }

        [Test]
        public void An_answer_that_is_empty_is_no_drawing_at_all()
        {
            var drawn = StockIcons.DrawnAmong(new int?[] { 1, 2 }, code => code == 1 ? "" : null);

            Assert.That(drawn, Is.Empty);
        }

        [Test]
        public void A_table_with_no_object_type_code_is_not_asked_about()
        {
            var asked = new List<int>();
            StockIcons.DrawnAmong(new int?[] { null, 1 }, code => { asked.Add(code); return Drawing; });

            Assert.That(asked, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void The_same_code_is_only_asked_about_once()
        {
            var asked = new List<int>();
            StockIcons.DrawnAmong(new int?[] { 1, 1, 1 }, code => { asked.Add(code); return Drawing; });

            Assert.That(asked, Is.EqualTo(new[] { 1 }));
        }

        /// <summary>
        /// One address that will not answer is one table this tool cannot speak for - not a
        /// reason to lose the answers either side of it, and not a reason to fail the load.
        /// </summary>
        [Test]
        public void One_address_that_throws_does_not_take_the_rest_with_it()
        {
            var drawn = StockIcons.DrawnAmong(
                new int?[] { 1, 2, 3 },
                code =>
                {
                    if (code == 2) throw new System.Net.WebException("the proxy hung up");
                    return Drawing;
                });

            Assert.That(drawn.Keys, Is.EquivalentTo(new[] { 1, 3 }));
        }

        [Test]
        public void An_org_that_answers_nothing_says_nothing_is_drawn()
        {
            var drawn = StockIcons.DrawnAmong(new int?[] { 1, 2, 3 }, code => null);

            Assert.That(drawn, Is.Empty);
        }

        [Test]
        public void Nothing_to_ask_about_is_not_an_error()
        {
            Assert.That(StockIcons.DrawnAmong(null, code => Drawing), Is.Empty);
            Assert.That(StockIcons.DrawnAmong(new int?[] { 1 }, null), Is.Empty);
        }
    }
}
