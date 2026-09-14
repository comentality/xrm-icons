using System.Linq;
using Icons.Catalog;
using Icons.Logic;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// What somebody is told about a licence that asks for a credit line, while they are still
    /// choosing the icon.
    ///
    /// No family bundled today asks for one, so the set these build sentences about is one made
    /// here. That is the point of the tests rather than a weakness of them: the code that has to
    /// be right the first time a CC BY set is bundled is exactly the code nothing exercises until
    /// then.
    /// </summary>
    [TestFixture]
    public class AttributionNoticeTests
    {
        private static IconLicence Asking()
        {
            return new IconLicence(
                IconFamily.Fluent,
                "Example Icons",
                "Someone Else",
                "CC-BY-4.0",
                "https://spdx.org/licenses/CC-BY-4.0.html",
                "https://example.invalid/icons",
                null,
                true);
        }

        [Test]
        public void A_permissive_set_gets_no_line_at_all()
        {
            // The page is still written for these - it is just not a thing worth warning about,
            // and a warning line that is always on is a warning line nobody reads.
            foreach (var licence in IconLicence.All)
            {
                Assert.That(AttributionNotice.For(licence, "new_/icons/account.svg"), Is.Null,
                            licence.SetName + " asks only that the notice travel with the software");
                Assert.That(AttributionNotice.Why(licence), Is.Null, licence.SetName);
            }
        }

        [Test]
        public void The_line_names_the_set_the_licence_and_the_file_it_will_write()
        {
            var line = AttributionNotice.For(Asking(), "new_/icons/account.svg");

            Assert.That(line, Does.Contain("Example Icons"));
            Assert.That(line, Does.Contain("CC-BY-4.0"));
            Assert.That(line, Does.Contain("new_/icons/ATTRIBUTION.html"),
                        "the file that is about to appear in somebody's solution is worth naming");
            Assert.That(line, Does.Contain("credit line"),
                        "and why it is going to appear, which is the half a file name cannot say");
        }

        [Test]
        public void It_says_the_icon_does_not_go_on_without_the_page()
        {
            // The difference between this and the courtesy: refusing the page here refuses the
            // icon. Somebody reading the line should not have to work that out from the apply
            // failing later.
            Assert.That(AttributionNotice.For(Asking(), "new_/icons/account.svg"),
                        Does.Contain("not put on the table"));
            Assert.That(AttributionNotice.Why(Asking()), Does.Contain("not put on the table"));
        }

        [Test]
        public void Why_leaves_out_the_file_name_for_a_gate_that_has_already_said_it()
        {
            var why = AttributionNotice.Why(Asking());

            Assert.That(why, Does.Contain("Example Icons"));
            Assert.That(why, Does.Not.Contain("ATTRIBUTION.html"),
                        "the confirmation names the page in its own sentence, one line above");
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("   ")]
        [TestCase("no prefix and a space")]
        public void A_name_still_being_typed_does_not_cost_the_file_name(string name)
        {
            // The line is on screen while somebody is filling the name box in, so the half-typed
            // state is the ordinary one. Saying the bare file name is worse than saying the full
            // one and better than the sentence losing the thing it is about.
            var line = AttributionNotice.For(Asking(), name);

            Assert.That(line, Does.Contain("ATTRIBUTION.html"));
            Assert.That(line, Does.Contain("CC-BY-4.0"));
        }

        [Test]
        public void A_name_with_no_folder_puts_the_page_under_the_prefix()
        {
            Assert.That(AttributionNotice.For(Asking(), "new_account_icon.svg"),
                        Does.Contain("new_ATTRIBUTION.html"),
                        "which is where the apply would write it, so it is what the line has to say");
        }
    }
}
