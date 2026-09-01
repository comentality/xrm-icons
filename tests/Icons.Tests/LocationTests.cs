using Icons.Logic;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// Reading an org's icon convention off the icons it already has. Every case here is one a
    /// real environment produces: nobody starts from an empty org, and the folder somebody chose
    /// three years ago is the one the next icon belongs in.
    /// </summary>
    [TestFixture]
    public class IconLocationsTests
    {
        private static readonly string[] Ours = { "new", "con" };

        [Test]
        public void An_org_with_no_icons_gets_the_convention()
        {
            var locations = IconLocations.From(new string[] { null, "", "   " }, Ours);

            Assert.That(locations.Folder, Is.EqualTo("icons"));
            Assert.That(locations.NameFor(null, "con", "con_project"),
                Is.EqualTo("con_/icons/con_project.svg"));
        }

        [Test]
        public void The_folder_the_org_uses_most_is_the_one_suggested()
        {
            var locations = IconLocations.From(new[]
            {
                "new_/images/tables/account.svg",
                "new_/images/tables/contact.svg",
                "con_/icons/lead.svg"
            }, Ours);

            Assert.That(locations.Folder, Is.EqualTo("images/tables"));
            Assert.That(locations.NameFor(null, "new", "new_thing"),
                Is.EqualTo("new_/images/tables/new_thing.svg"));
        }

        [Test]
        public void Microsofts_folders_do_not_get_a_vote()
        {
            // A real org has hundreds of first-party icons and a handful of its own. Counting
            // theirs would follow a convention nobody here chose.
            var locations = IconLocations.From(new[]
            {
                "msdyn_/Icons/SvgIcons/one.svg",
                "msdyn_/Icons/SvgIcons/two.svg",
                "msdyn_/Icons/SvgIcons/three.svg",
                "new_/brand/account.svg"
            }, Ours);

            Assert.That(locations.Folder, Is.EqualTo("brand"));
        }

        [Test]
        public void An_org_that_keeps_icons_at_the_root_keeps_them_there()
        {
            var locations = IconLocations.From(new[] { "new_account.svg", "new_contact.svg" }, Ours);

            Assert.That(locations.Folder, Is.EqualTo(""));
            Assert.That(locations.NameFor(null, "new", "new_thing"), Is.EqualTo("new_/new_thing.svg"));
        }

        [Test]
        public void Two_spellings_of_one_folder_count_as_one_and_the_common_spelling_wins()
        {
            var locations = IconLocations.From(new[]
            {
                "new_/Icons/a.svg",
                "new_/icons/b.svg",
                "new_/icons/c.svg",
                "new_/brand/d.svg",
                "new_/brand/e.svg"
            }, Ours);

            // Three under one name against two under another, and the spelling used most of the
            // three is the one written.
            Assert.That(locations.Folder, Is.EqualTo("icons"));
        }

        [Test]
        public void A_tie_breaks_the_same_way_every_time()
        {
            var one = IconLocations.From(new[] { "new_/alpha/a.svg", "new_/beta/b.svg" }, Ours);
            var other = IconLocations.From(new[] { "new_/beta/b.svg", "new_/alpha/a.svg" }, Ours);

            Assert.That(one.Folder, Is.EqualTo(other.Folder));
            Assert.That(one.Folder, Is.EqualTo("alpha"));
        }

        [Test]
        public void A_table_that_already_has_our_icon_keeps_the_name_it_has()
        {
            // The whole point of a predictable name: one web resource updated, not a second one
            // left beside it with nothing pointing at the first.
            var locations = IconLocations.From(new[] { "new_/icons/account.svg" }, Ours);

            Assert.That(locations.NameFor("new_/legacy/account_v2.svg", "new", "account"),
                Is.EqualTo("new_/legacy/account_v2.svg"));
        }

        [Test]
        public void A_managed_icon_is_not_written_over()
        {
            // msdyn_ is nobody's publisher here, so that icon belongs to a managed solution and
            // the write would be refused. Suggest a name of our own instead.
            var locations = IconLocations.From(new[] { "new_/icons/account.svg" }, Ours);

            Assert.That(locations.Reusable("msdyn_/Icons/case.svg"), Is.False);
            Assert.That(locations.NameFor("msdyn_/Icons/case.svg", "new", "incident"),
                Is.EqualTo("new_/icons/incident.svg"));
        }

        [Test]
        public void An_icon_that_is_not_an_svg_is_left_alone()
        {
            // IconVectorName should only ever hold an SVG, but a table pointing at something else
            // must not turn into an upsert that tries to change a web resource's type.
            var locations = IconLocations.From(new string[0], Ours);

            Assert.That(locations.Reusable("new_/icons/account.png"), Is.False);
        }

        [Test]
        public void A_current_icon_Dataverse_would_reject_is_not_suggested_back()
        {
            var locations = IconLocations.From(new string[0], Ours);

            Assert.That(locations.Reusable("new_/icons/my icon.svg"), Is.False);
            Assert.That(locations.NameFor("new_/icons/my icon.svg", "new", "account"),
                Is.EqualTo("new_/icons/account.svg"));
        }

        [Test]
        public void A_prefix_is_matched_however_it_was_written()
        {
            var locations = IconLocations.From(new[] { "NEW_/Brand/account.svg" }, new[] { "New_", " new " });

            Assert.That(locations.Folder, Is.EqualTo("Brand"));
            Assert.That(locations.Reusable("new_/brand/account.svg"), Is.True);
        }

        [TestCase("new_/icons/a.svg", "new")]
        [TestCase("new_a.svg", "new")]
        [TestCase("noprefix.svg", null)]
        [TestCase("/icons/a_b.svg", null)]
        public void The_prefix_is_what_comes_before_the_first_underscore(string name, string expected)
        {
            Assert.That(IconLocations.PrefixOf(name), Is.EqualTo(expected));
        }

        [TestCase("new_/icons/a.svg", "icons")]
        [TestCase("new_/Icons/Tables/a.svg", "Icons/Tables")]
        [TestCase("new_a.svg", "")]
        [TestCase("new_/a.svg", "")]
        [TestCase("new_icons/a.svg", "icons")]
        public void The_folder_is_everything_between_the_prefix_and_the_file(string name, string expected)
        {
            Assert.That(IconLocations.FolderOf(name), Is.EqualTo(expected));
        }

        [Test]
        public void Nothing_learned_yet_still_suggests_something_valid()
        {
            var name = IconLocations.None.NameFor(null, null, "My Table");

            Assert.That(WebResourceName.Validate(name), Is.Null);
            Assert.That(name, Is.EqualTo("new_/icons/my_table.svg"));
        }
    }
}
