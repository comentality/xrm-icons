using Icons.Logic;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// Taking an icon off a table this session never applied to. The write is the undo's own,
    /// so these ask two things: that the request built for it says what the confirmation
    /// promised, and that it goes through Revert and comes out the far side as an empty slot
    /// rather than a null one.
    /// </summary>
    [TestFixture]
    public class IconRemovalTests
    {
        private const string Vector = "new_/icons/project.svg";
        private const string Small = "new_/icons/project_small.png";
        private const string Medium = "new_/icons/project_medium.png";
        private const string Large = "new_/icons/project_large.png";

        [Test]
        public void The_vector_is_emptied_not_nulled()
        {
            var off = IconRemoval.Off("new_project", Vector, null, null, null, null);

            // Null in IconVectorName is "leave this one alone": a removal written that way
            // publishes, reports success and leaves the icon on.
            Assert.That(off.VectorName, Is.Empty);
        }

        [Test]
        public void Classic_slots_are_left_alone_when_there_are_none()
        {
            var off = IconRemoval.Off("new_project", Vector, null, null, null, null);

            Assert.That(off.RestoreRasterIcons, Is.False,
                "a table with no classic icons should have nothing written to those slots");
        }

        [Test]
        public void The_vectors_own_PNGs_come_off_with_it()
        {
            var off = IconRemoval.Off("new_project", Vector, Small, Medium, Large, null);

            Assert.That(off.RestoreRasterIcons, Is.True);
            Assert.That(off.SmallName, Is.Empty);
            Assert.That(off.MediumName, Is.Empty);
            Assert.That(off.LargeName, Is.Empty);
        }

        [Test]
        public void Somebody_elses_PNG_stays_where_it_is()
        {
            // The vector's own small one, and a medium that predates it.
            var off = IconRemoval.Off("new_project", Vector, Small, "old_/project32.png", null, null);

            Assert.That(off.RestoreRasterIcons, Is.True);
            Assert.That(off.SmallName, Is.Empty);
            Assert.That(off.MediumName, Is.EqualTo("old_/project32.png"),
                "a classic icon that is not this vector's is written back as it stands");
            Assert.That(IconRemoval.Staying(Vector, Small, "old_/project32.png", null),
                Is.EqualTo(new[] { "old_/project32.png" }));
        }

        [Test]
        public void Only_foreign_PNGs_means_the_classic_slots_are_not_touched()
        {
            var off = IconRemoval.Off("new_project", Vector, "a.png", "b.png", "c.png", null);

            Assert.That(off.RestoreRasterIcons, Is.False);
            Assert.That(IconRemoval.Staying(Vector, "a.png", "b.png", "c.png"), Has.Length.EqualTo(3));
        }

        [Test]
        public void Names_are_matched_whatever_their_case()
        {
            Assert.That(IconRemoval.TakesClassic(Vector, Small.ToUpperInvariant(), null, null), Is.True);
        }

        [Test]
        public void Putting_it_back_names_what_was_there()
        {
            var back = IconRemoval.Back("new_project", Vector, Small, "old_/project32.png", null, "Contoso");

            Assert.That(back.VectorName, Is.EqualTo(Vector));
            Assert.That(back.RestoreRasterIcons, Is.True);
            Assert.That(back.SmallName, Is.EqualTo(Small));
            Assert.That(back.MediumName, Is.EqualTo("old_/project32.png"));
            Assert.That(back.SolutionUniqueName, Is.EqualTo("Contoso"));

            // Nothing was written over and the folder did not change, so there is no drawing to
            // put back and no attribution page to rebuild.
            Assert.That(back.Overwritten, Is.Null);
            Assert.That(back.WebResourceName, Is.Null);
        }

        [Test]
        public void Through_revert_the_table_ends_up_with_no_icon_and_back_again()
        {
            var service = new FakeOrganizationService();
            service.Tables["new_project"] = FakeOrganizationService.Table("new_project", existingVectorIcon: Vector);

            IconApplier.Revert(service, IconRemoval.Off("new_project", Vector, null, null, null, null));
            Assert.That(service.LastUpdatedTable.IconVectorName, Is.Empty);
            Assert.That(service.LastPublishXml, Does.Contain("new_project"));
            Assert.That(service.WebResourceCount, Is.Zero, "nothing is written but the table");

            IconApplier.Revert(service, IconRemoval.Back("new_project", Vector, null, null, null, null));
            Assert.That(service.LastUpdatedTable.IconVectorName, Is.EqualTo(Vector));
        }
    }
}
