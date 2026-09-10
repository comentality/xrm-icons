using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Icons.Logic;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace Icons.Tests
{
    [TestFixture]
    public class IconApplierTests
    {
        private const string Svg =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\" " +
            "fill=\"currentColor\"><path d=\"M2 2H22V22H2Z\"/></svg>";

        private FakeOrganizationService _service;

        [SetUp]
        public void SetUp()
        {
            _service = new FakeOrganizationService();
            _service.Tables["account"] = FakeOrganizationService.Table("account");
        }

        private static IconApplyRequest Request(string table = "account")
        {
            return new IconApplyRequest
            {
                TableLogicalName = table,
                Svg = Svg,
                WebResourceName = "new_/icons/" + table + ".svg",
                RasterColour = Color.Black
            };
        }

        [Test]
        public void A_new_icon_is_created_pointed_at_and_published()
        {
            var result = IconApplier.Apply(_service, Request());

            var stored = _service.WebResource("new_/icons/account.svg");
            Assert.That(stored, Is.Not.Null, "the drawing should have been stored");
            Assert.That(stored.GetAttributeValue<OptionSetValue>("webresourcetype").Value, Is.EqualTo(11),
                "an SVG web resource is type 11");

            Assert.That(_service.LastUpdatedTable.IconVectorName, Is.EqualTo("new_/icons/account.svg"));
            Assert.That(result.Published, Is.True);
        }

        [Test]
        public void The_stored_content_is_the_svg_that_was_asked_for()
        {
            IconApplier.Apply(_service, Request());

            var content = _service.WebResource("new_/icons/account.svg").GetAttributeValue<string>("content");
            var decoded = new UTF8Encoding(false).GetString(Convert.FromBase64String(content));

            Assert.That(decoded, Is.EqualTo(Svg));
        }

        [Test]
        public void The_table_and_the_web_resource_are_published_together()
        {
            // Publish only one and the app shows the old icon, or asks for one that is not there.
            var result = IconApplier.Apply(_service, Request());
            var xml = XDocument.Parse(_service.LastPublishXml);

            Assert.That(xml.Descendants("entity").Single().Value, Is.EqualTo("account"));
            Assert.That(xml.Descendants("webresource").Select(e => e.Value),
                Is.EquivalentTo(result.WebResourceIds.Select(id => id.ToString("D"))),
                "the drawing and the attribution page beside it");
        }

        [Test]
        public void Labels_are_merged_so_other_languages_survive_the_update()
        {
            IconApplier.Apply(_service, Request());

            Assert.That(_service.LastUpdateMergedLabels, Is.True,
                "without MergeLabels an icon change wipes translations this connection cannot see");
        }

        [Test]
        public void Running_twice_updates_one_web_resource_rather_than_making_two()
        {
            IconApplier.Apply(_service, Request());
            var afterFirst = _service.WebResourceCount;

            IconApplier.Apply(_service, Request());

            Assert.That(_service.WebResourceCount, Is.EqualTo(afterFirst));
            Assert.That(_service.Calls.Count(c => c == "Update"), Is.EqualTo(2),
                "the second run rewrites the drawing and the attribution page, and creates neither");
        }

        [Test]
        public void An_unpublished_web_resource_is_found_rather_than_recreated()
        {
            // A plain RetrieveMultiple cannot see an unpublished web resource, so the tool would
            // try to create a second one with the same name and the server would refuse.
            _service.AddWebResource("new_/icons/account.svg", 11);

            IconApplier.Apply(_service, Request());

            Assert.That(_service.Calls, Does.Contain("RetrieveUnpublishedMultiple"));
            Assert.That(_service.WebResourceCount, Is.EqualTo(2),
                "the one that was already there, rewritten, and the attribution page");
        }

        [Test]
        public void A_name_already_used_by_a_different_kind_of_resource_is_refused()
        {
            _service.AddWebResource("new_/icons/account.svg", 3); // a script

            Assert.That(() => IconApplier.Apply(_service, Request()),
                Throws.TypeOf<InvalidOperationException>().And.Message.Contains("not the right kind"));
        }

        [Test]
        public void A_managed_web_resource_is_not_overwritten()
        {
            _service.AddWebResource("new_/icons/account.svg", 11, managed: true);

            Assert.That(() => IconApplier.Apply(_service, Request()),
                Throws.TypeOf<InvalidOperationException>().And.Message.Contains("managed"));
        }

        [Test]
        public void A_table_locked_against_customisation_is_refused_before_anything_is_published()
        {
            _service.Tables["account"] = FakeOrganizationService.Table("account", customizable: false);

            Assert.That(() => IconApplier.Apply(_service, Request()),
                Throws.TypeOf<InvalidOperationException>().And.Message.Contains("locked"));

            Assert.That(_service.LastPublishXml, Is.Null, "nothing should have been published");
        }

        [Test]
        public void An_invalid_web_resource_name_never_reaches_the_server()
        {
            var request = Request();
            request.WebResourceName = "new_/icons/my icon.svg";

            Assert.That(() => IconApplier.Apply(_service, request), Throws.TypeOf<ArgumentException>());
            Assert.That(_service.Calls, Is.Empty);
        }

        [Test]
        public void The_previous_icon_is_reported_so_a_change_can_be_undone()
        {
            _service.Tables["account"] =
                FakeOrganizationService.Table("account", existingVectorIcon: "old_/icons/account.svg");

            var result = IconApplier.Apply(_service, Request());

            Assert.That(result.PreviousVectorIcon, Is.EqualTo("old_/icons/account.svg"));
        }

        [Test]
        public void A_solution_is_passed_on_when_one_is_chosen()
        {
            var request = Request();
            request.SolutionUniqueName = "MySolution";

            Assert.That(() => IconApplier.Apply(_service, request), Throws.Nothing);
        }

        [Test]
        public void Raster_icons_fill_the_three_classic_properties()
        {
            var request = Request();
            request.IncludeRasterIcons = true;
            request.Image = SvgImage.Parse(Svg);

            var result = IconApplier.Apply(_service, request);

            Assert.That(_service.WebResource("new_/icons/account_small.png"), Is.Not.Null);
            Assert.That(_service.WebResource("new_/icons/account_medium.png"), Is.Not.Null);
            Assert.That(_service.WebResource("new_/icons/account_large.png"), Is.Not.Null);

            Assert.That(_service.WebResource("new_/icons/account_small.png")
                .GetAttributeValue<OptionSetValue>("webresourcetype").Value, Is.EqualTo(5), "PNG is type 5");

            Assert.That(_service.LastUpdatedTable.IconSmallName, Is.EqualTo("new_/icons/account_small.png"));
            Assert.That(_service.LastUpdatedTable.IconMediumName, Is.EqualTo("new_/icons/account_medium.png"));
            Assert.That(_service.LastUpdatedTable.IconLargeName, Is.EqualTo("new_/icons/account_large.png"));

            Assert.That(result.WebResourceIds.Count, Is.EqualTo(5),
                "the drawing, its three PNGs and the attribution page all go in the one publish");
        }

        [Test]
        public void Raster_icons_without_a_drawing_are_refused()
        {
            var request = Request();
            request.IncludeRasterIcons = true;

            Assert.That(() => IconApplier.Apply(_service, request), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Cancelling_stops_the_calls_that_have_not_been_made_yet()
        {
            // The call already on the wire cannot be recalled. The ones behind it are most of
            // the wait on a slow link, and they are what cancelling has to prevent.
            var cancelled = false;
            _service.OnCall = call => { if (call == "RetrieveUnpublishedMultiple") cancelled = true; };

            Assert.That(() => IconApplier.Apply(_service, Request(), () => cancelled),
                Throws.TypeOf<IconApplier.CancelledException>());

            Assert.That(_service.Calls, Does.Not.Contain("PublishXml"));
            Assert.That(_service.LastUpdatedTable, Is.Null, "the table should not have been touched");
        }

        [Test]
        public void Cancelling_before_anything_happens_sends_nothing()
        {
            Assert.That(() => IconApplier.Apply(_service, Request(), () => true),
                Throws.TypeOf<IconApplier.CancelledException>());

            Assert.That(_service.Calls, Is.Empty);
        }

        [Test]
        public void Progress_is_reported_in_the_order_the_work_happens()
        {
            var progress = new List<string>();

            IconApplier.Apply(_service, Request(), null, progress.Add);

            Assert.That(progress.First(), Does.Contain("Storing"));
            Assert.That(progress.Last(), Is.EqualTo("Publishing"));
        }

        [Test]
        public void A_failure_partway_through_does_not_publish()
        {
            _service.FailOnce["UpdateEntity"] = new InvalidOperationException("no");

            Assert.That(() => IconApplier.Apply(_service, Request()), Throws.InvalidOperationException);

            Assert.That(_service.LastPublishXml, Is.Null);
        }

        [Test]
        public void A_table_the_org_does_not_have_is_reported()
        {
            Assert.That(() => IconApplier.Apply(_service, Request("nosuchtable")), Throws.Exception);
        }
    }
}
