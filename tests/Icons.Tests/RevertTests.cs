using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Icons.Catalog;
using Icons.Logic;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// Taking an apply back. Every one of these applies first and then reverts with what that
    /// apply reported, because that is the only way the tool ever calls it: the values written
    /// back are the org's own answer from a moment before, not anything the window remembered.
    /// </summary>
    [TestFixture]
    public class IconRevertTests
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

        private static IconApplyRequest Apply(bool raster = false)
        {
            return new IconApplyRequest
            {
                TableLogicalName = "account",
                Svg = Svg,
                WebResourceName = "new_/icons/account.svg",
                IncludeRasterIcons = raster,
                Image = raster ? SvgImage.Parse(Svg) : null,
                RasterColour = Color.Black
            };
        }

        /// <summary>The revert the tool builds out of an apply's own result.</summary>
        private static IconRevertRequest Undoing(IconApplyRequest request, IconApplyResult result)
        {
            return new IconRevertRequest
            {
                TableLogicalName = request.TableLogicalName,
                VectorName = result.PreviousVectorIcon,
                RestoreRasterIcons = request.IncludeRasterIcons,
                SmallName = result.PreviousSmallIcon,
                MediumName = result.PreviousMediumIcon,
                LargeName = result.PreviousLargeIcon,
                Overwritten = result.Overwritten,
                SolutionUniqueName = request.SolutionUniqueName
            };
        }

        [Test]
        public void The_table_points_back_at_the_icon_it_had()
        {
            _service.Tables["account"] =
                FakeOrganizationService.Table("account", existingVectorIcon: "old_/icons/account.svg");

            var request = Apply();
            var result = IconApplier.Apply(_service, request);
            Assume.That(_service.LastUpdatedTable.IconVectorName, Is.EqualTo("new_/icons/account.svg"));

            IconApplier.Revert(_service, Undoing(request, result));

            Assert.That(_service.LastUpdatedTable.IconVectorName, Is.EqualTo("old_/icons/account.svg"));
        }

        [Test]
        public void A_table_that_had_no_icon_is_left_with_none_again()
        {
            var request = Apply();
            var result = IconApplier.Apply(_service, request);

            IconApplier.Revert(_service, Undoing(request, result));

            // Empty and not null. A null in an icon property is read by the platform as "leave
            // this one alone", so an undo written that way would publish and change nothing -
            // measured against a real org, and asked again by tests/e2e.ps1 on every run.
            Assert.That(_service.LastUpdatedTable.IconVectorName, Is.Empty);
        }

        [Test]
        public void The_drawing_that_was_written_over_goes_back()
        {
            // The second apply to a table reuses the first one's name, so the old drawing is not
            // sitting somewhere else under another name - it is inside the resource about to be
            // overwritten, and this is the only chance to keep it.
            var existing = _service.AddWebResource("new_/icons/account.svg", 11);
            var before = Convert.ToBase64String(Encoding.UTF8.GetBytes("<svg>the old one</svg>"));
            existing["content"] = before;

            var request = Apply();
            var result = IconApplier.Apply(_service, request);
            Assume.That(_service.WebResource("new_/icons/account.svg").GetAttributeValue<string>("content"),
                Is.Not.EqualTo(before), "the apply should have overwritten it");

            IconApplier.Revert(_service, Undoing(request, result));

            Assert.That(_service.WebResource("new_/icons/account.svg").GetAttributeValue<string>("content"),
                Is.EqualTo(before));
            Assert.That(_service.LastPublishXml, Does.Contain(existing.Id.ToString("D")),
                "the drawing that changed back has to be published, or the app keeps showing the new one");
        }

        [Test]
        public void A_web_resource_the_apply_created_is_left_where_it_is()
        {
            var request = Apply();
            var result = IconApplier.Apply(_service, request);
            var count = _service.WebResourceCount;

            IconApplier.Revert(_service, Undoing(request, result));

            Assert.That(_service.WebResourceCount, Is.EqualTo(count), "nothing here deletes a web resource");
            Assert.That(_service.LastPublishXml, Does.Not.Contain("webresource"),
                "nothing was written over, so there is no drawing to publish - only the table");
        }

        [Test]
        public void The_classic_slots_go_back_when_the_apply_took_them()
        {
            var table = FakeOrganizationService.Table("account");
            table.IconSmallName = "old_/icons/account_small.png";
            table.IconMediumName = "old_/icons/account_medium.png";
            table.IconLargeName = "old_/icons/account_large.png";
            _service.Tables["account"] = table;

            var request = Apply(raster: true);
            var result = IconApplier.Apply(_service, request);
            Assume.That(_service.LastUpdatedTable.IconSmallName, Is.EqualTo("new_/icons/account_small.png"));

            IconApplier.Revert(_service, Undoing(request, result));

            Assert.That(_service.LastUpdatedTable.IconSmallName, Is.EqualTo("old_/icons/account_small.png"));
            Assert.That(_service.LastUpdatedTable.IconMediumName, Is.EqualTo("old_/icons/account_medium.png"));
            Assert.That(_service.LastUpdatedTable.IconLargeName, Is.EqualTo("old_/icons/account_large.png"));
        }

        [Test]
        public void The_classic_slots_are_left_alone_when_the_apply_never_touched_them()
        {
            var table = FakeOrganizationService.Table("account");
            table.IconSmallName = "somebody_elses_small.png";
            _service.Tables["account"] = table;

            var request = Apply();
            var result = IconApplier.Apply(_service, request);

            IconApplier.Revert(_service, Undoing(request, result));

            Assert.That(_service.LastUpdatedTable.IconSmallName, Is.EqualTo("somebody_elses_small.png"),
                "an apply that never wrote a PNG has nothing to undo about the classic slots");
        }

        [Test]
        public void A_table_locked_against_customisation_is_refused_before_anything_is_published()
        {
            var request = Apply();
            var result = IconApplier.Apply(_service, request);

            // Locked between the apply and the undo, which is the only way this can happen -
            // the apply itself refuses a locked table.
            _service.Tables["account"] = FakeOrganizationService.Table("account", customizable: false);

            Assert.That(() => IconApplier.Revert(_service, Undoing(request, result)),
                Throws.InvalidOperationException);

            Assert.That(_service.Calls.Count(c => c == "PublishXml"), Is.EqualTo(1),
                "only the apply's own publish should have happened");
        }

        [Test]
        public void Cancelling_stops_the_calls_that_have_not_been_made_yet()
        {
            var request = Apply();
            var result = IconApplier.Apply(_service, request);
            var published = _service.Calls.Count(c => c == "PublishXml");

            var cancelled = false;
            _service.OnCall = call => { if (call == "RetrieveEntity") cancelled = true; };

            Assert.That(() => IconApplier.Revert(_service, Undoing(request, result), () => cancelled),
                Throws.TypeOf<IconApplier.CancelledException>());

            Assert.That(_service.Calls.Count(c => c == "PublishXml"), Is.EqualTo(published),
                "the publish is behind the cancellation check, so it must not have gone out");
        }

        [Test]
        public void Putting_it_back_twice_says_the_same_thing_as_once()
        {
            // Every step writes a value rather than a difference, so a revert that failed
            // halfway can be asked again without knowing how far it got.
            _service.Tables["account"] =
                FakeOrganizationService.Table("account", existingVectorIcon: "old_/icons/account.svg");

            var request = Apply();
            var result = IconApplier.Apply(_service, request);

            IconApplier.Revert(_service, Undoing(request, result));
            IconApplier.Revert(_service, Undoing(request, result));

            Assert.That(_service.LastUpdatedTable.IconVectorName, Is.EqualTo("old_/icons/account.svg"));
        }

        [Test]
        public void Progress_is_reported_in_the_order_the_work_happens()
        {
            _service.AddWebResource("new_/icons/account.svg", 11);

            var request = Apply();
            var result = IconApplier.Apply(_service, request);

            var progress = new List<string>();
            IconApplier.Revert(_service, Undoing(request, result), null, progress.Add);

            Assert.That(progress.First(), Does.Contain("Putting back"));
            Assert.That(progress.Last(), Is.EqualTo("Publishing"));
        }

        [Test]
        public void The_attribution_page_goes_back_with_the_drawing_it_described()
        {
            // The page an apply wrote over is a web resource it wrote over like any other, so it
            // comes back the same way. It goes back to what it said, not to describing the
            // folder: nothing here deletes, so the drawing it stops crediting is still stored
            // with nothing pointing at it until the next apply rewrites the page.
            _service.Tables["contact"] = FakeOrganizationService.Table("contact");

            var first = new IconApplyRequest
            {
                TableLogicalName = "account",
                Svg = SvgOutput.Build(Svg, IconColourMode.Theme, Color.Black, null,
                    IconProvenance.For(new IconEntry(IconFamily.Fluent, "building", "regular", IconStyle.Regular))),
                WebResourceName = "new_/icons/account.svg",
                RasterColour = Color.Black
            };
            IconApplier.Apply(_service, first);

            var second = new IconApplyRequest
            {
                TableLogicalName = "contact",
                Svg = SvgOutput.Build(Svg, IconColourMode.Theme, Color.Black, null,
                    IconProvenance.For(new IconEntry(IconFamily.Lucide, "user", "regular", IconStyle.Regular))),
                WebResourceName = "new_/icons/contact.svg",
                RasterColour = Color.Black
            };
            var result = IconApplier.Apply(_service, second);
            Assert.That(Page(), Does.Contain("new_/icons/contact.svg"), "the second apply should have credited it");

            IconApplier.Revert(_service, Undoing(second, result));

            Assert.That(Page(), Does.Contain("new_/icons/account.svg"));
            Assert.That(Page(), Does.Not.Contain("new_/icons/contact.svg"),
                "and the page should be back to describing the folder as the first apply left it");
        }

        private string Page()
        {
            var page = _service.WebResource("new_/icons/ATTRIBUTION.html");
            return new UTF8Encoding(false).GetString(
                Convert.FromBase64String(page.GetAttributeValue<string>("content")));
        }
    }
}
