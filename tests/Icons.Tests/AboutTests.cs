using System;
using System.Linq;
using System.Reflection;
using Comentality.Xtb.About;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// The About box says two things nobody can check by looking at it: which build this is, and
    /// what a diagnostics block is about to put in somebody's mail. Both are checked here.
    /// </summary>
    [TestFixture]
    public class AboutTests
    {
        [Test]
        public void Version_carries_the_commit_when_the_build_stamped_one()
        {
            var version = ToolAbout.VersionOf(typeof(IconsPlugin).Assembly);

            // The project stamps AssemblyInformationalVersion with the version and the commit.
            Assert.That(version, Does.Match(@"^\d+\.\d+\.\d+(-[\w.]+)?( \([0-9a-f]{7}\))?$"),
                "The version line is what somebody types back into a bug report: " + version);
        }

        [Test]
        public void The_commit_is_cut_to_the_seven_characters_anybody_types()
        {
            var assembly = Build("1.0.1-dev+fee4a2ddfda1eafd2ef34e5704a6d39e91acc1ab");

            Assert.That(ToolAbout.VersionOf(assembly), Is.EqualTo("1.0.1-dev (fee4a2d)"));
        }

        [Test]
        public void A_build_with_no_commit_says_only_the_version()
        {
            Assert.That(ToolAbout.VersionOf(Build("1.2.3")), Is.EqualTo("1.2.3"));
            Assert.That(ToolAbout.VersionOf(Build("1.2.3+")), Is.EqualTo("1.2.3"));
        }

        [Test]
        public void Diagnostics_is_four_lines_about_software_and_nothing_else()
        {
            var lines = ToolAbout.Compose("Icons", "1.0.1-dev (fee4a2d)", "XrmToolBox 1.2026.8.75",
                                          ".NET Framework 4.8.9300.0", "Microsoft Windows 10.0.26200")
                .Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            Assert.That(lines, Has.Length.EqualTo(4));
            Assert.That(lines[0], Is.EqualTo("Icons 1.0.1-dev (fee4a2d)"));
            Assert.That(lines[1], Is.EqualTo("XrmToolBox 1.2026.8.75"));
        }

        [Test]
        public void Diagnostics_leaves_out_a_fact_the_machine_could_not_answer()
        {
            var lines = ToolAbout.Compose("Icons", "1.0.1", null, ".NET", "Windows")
                .Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            Assert.That(lines, Has.Length.EqualTo(3), "A blank line in a pasted block reads as a bug.");
        }

        [Test]
        public void The_tool_reads_its_own_name_description_and_logo_out_of_its_plugin_metadata()
        {
            var about = About();

            Assert.That(about.Name, Is.EqualTo("Icons"));
            Assert.That(about.Description, Does.Contain("icon on a Dataverse table"));
            Assert.That(about.Logo, Is.Not.Null, "XrmToolBox paints a tile from this; so does the box.");
        }

        [Test]
        public void The_links_point_where_the_tool_actually_lives()
        {
            var about = About();

            Assert.That(about.PageUrl, Is.EqualTo("https://comentality.com/xrm/xtb/icons"));
            Assert.That(about.RepositoryUrl, Is.EqualTo("https://github.com/comentality/xrm-icons"));
            Assert.That(about.ReleaseNotesUrl,
                Is.EqualTo("https://github.com/comentality/xrm-icons/releases/latest"));
        }

        [Test]
        public void The_mail_is_addressed_titled_and_already_carrying_the_versions()
        {
            var about = About();
            var mail = about.MailTo();

            Assert.That(mail.Scheme, Is.EqualTo("mailto"));
            Assert.That(mail.ToString(), Does.StartWith("mailto:feedback@comentality.com?"));

            var query = Uri.UnescapeDataString(mail.ToString());
            Assert.That(query, Does.Contain("subject=Icons "));
            Assert.That(query, Does.Contain(about.Diagnostics()));
        }

        [Test]
        public void The_mail_stays_short_enough_for_a_mail_client_to_take_it()
        {
            // Shell execute truncates a long argument, and mail clients differ on where. Well
            // under any of them is the only safe place to be.
            Assert.That(About().MailTo().ToString().Length, Is.LessThan(1500));
        }

        [Test]
        public void A_tool_needs_a_page_to_link_to()
        {
            Assert.Throws<ArgumentException>(() =>
                ToolAbout.FromPlugin(typeof(IconsPlugin), "  ", "https://example.invalid"));
        }

        private static ToolAbout About()
        {
            return ToolAbout.FromPlugin(
                typeof(IconsPlugin), "icons", "https://github.com/comentality/xrm-icons");
        }

        /// <summary>An assembly that says it was built at a given informational version, and nothing else.</summary>
        private static Assembly Build(string informationalVersion)
        {
            var name = new AssemblyName("About.Probe." + Guid.NewGuid().ToString("N"));
            var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
                name, System.Reflection.Emit.AssemblyBuilderAccess.Run);

            assembly.SetCustomAttribute(new System.Reflection.Emit.CustomAttributeBuilder(
                typeof(AssemblyInformationalVersionAttribute).GetConstructor(new[] { typeof(string) }),
                new object[] { informationalVersion }));

            return assembly;
        }
    }
}
