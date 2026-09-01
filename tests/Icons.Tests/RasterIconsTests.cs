using Icons.Logic;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// The sentence shown before a write that includes the PNGs. Its whole worth is that it
    /// predicts what the applier does, so these tests are as much about the naming rule as
    /// about the wording.
    /// </summary>
    [TestFixture]
    public class RasterIconsTests
    {
        [Test]
        public void A_slot_is_named_after_the_vector_it_belongs_to()
        {
            Assert.That(RasterIcons.NameFor("new_/icons/account.svg", IconSlot.Small),
                Is.EqualTo("new_/icons/account_small.png"));
            Assert.That(RasterIcons.NameFor("new_/icons/account.svg", IconSlot.Medium),
                Is.EqualTo("new_/icons/account_medium.png"));
            Assert.That(RasterIcons.NameFor("new_/icons/account.svg", IconSlot.Large),
                Is.EqualTo("new_/icons/account_large.png"));
        }

        [Test]
        public void A_name_that_is_not_an_svg_still_gets_one_extension_not_two()
        {
            Assert.That(RasterIcons.NameFor("new_/icons/account", IconSlot.Small),
                Is.EqualTo("new_/icons/account_small.png"));
        }

        [Test]
        public void A_table_with_no_classic_icons_has_nothing_said_about_them()
        {
            Assert.That(RasterIcons.Handover("new_/icons/account.svg", null, null, ""), Is.Null);
        }

        [Test]
        public void A_second_run_over_this_tools_own_pngs_says_nothing()
        {
            // The ordinary re-apply. "This replaces itself" on every run is noise, and noise is
            // how the lines that matter stop being read.
            Assert.That(
                RasterIcons.Handover(
                    "new_/icons/account.svg",
                    "new_/icons/account_small.png",
                    "new_/icons/account_medium.png",
                    "new_/icons/account_large.png"),
                Is.Null);
        }

        [Test]
        public void Names_are_compared_the_way_Dataverse_compares_them()
        {
            // Web resource names are matched case-insensitively elsewhere in the tool; if this
            // compared them exactly, a table whose icons this tool wrote would be reported as
            // losing them the next time somebody typed the name differently.
            Assert.That(
                RasterIcons.Handover(
                    "NEW_/Icons/Account.svg",
                    "new_/icons/account_small.png",
                    null, null),
                Is.Null);
        }

        [Test]
        public void Somebody_elses_classic_icons_are_named_along_with_what_replaces_them()
        {
            var text = RasterIcons.Handover(
                "new_/icons/account.svg",
                "abc_/images/thing16.png",
                null,
                "abc_/images/thing66.png");

            Assert.That(text, Does.Contain("abc_/images/thing16.png"));
            Assert.That(text, Does.Contain("new_/icons/account_small.png"));
            Assert.That(text, Does.Contain("abc_/images/thing66.png"));
            Assert.That(text, Does.Contain("new_/icons/account_large.png"));

            // The slot that was empty is not mentioned: nothing is being taken over there.
            Assert.That(text, Does.Not.Contain("_medium.png"));

            // Each is labelled by size, because "16x16" is how the reader knows which of their
            // icons this is talking about.
            Assert.That(text, Does.Contain("16x16"));
            Assert.That(text, Does.Contain("66x48"));
        }

        [Test]
        public void It_says_the_old_web_resources_are_left_alone()
        {
            // The tool deletes nothing, and a dialog about icons being taken over has to say so
            // or it reads like one.
            var text = RasterIcons.Handover("new_/icons/account.svg", "abc_/images/thing16.png", null, null);

            Assert.That(text, Does.Contain("stays in the solution"));
            Assert.That(text, Does.Contain("deletes"));
        }

        [Test]
        public void The_slots_are_the_three_sizes_Dataverse_asks_for()
        {
            Assert.That(RasterIcons.Slots.Count, Is.EqualTo(3));
            Assert.That(RasterIcons.Slots[0].Value, Is.EqualTo(new System.Drawing.Size(16, 16)));
            Assert.That(RasterIcons.Slots[1].Value, Is.EqualTo(new System.Drawing.Size(32, 32)));
            Assert.That(RasterIcons.Slots[2].Value, Is.EqualTo(new System.Drawing.Size(66, 48)));
        }
    }
}
