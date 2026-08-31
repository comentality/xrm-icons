using System;
using System.Linq;
using System.Xml.Linq;
using Icons.Logic;
using NUnit.Framework;

namespace Icons.Tests
{
    [TestFixture]
    public class WebResourceNameTests
    {
        [Test]
        public void The_conventional_name_is_prefix_folder_table()
        {
            Assert.That(WebResourceName.ForTable("new", "account"), Is.EqualTo("new_/icons/account.svg"));
        }

        [Test]
        public void A_prefix_that_already_ends_in_an_underscore_does_not_get_two()
        {
            Assert.That(WebResourceName.ForTable("new_", "account"), Is.EqualTo("new_/icons/account.svg"));
        }

        [Test]
        public void The_folder_can_be_changed_or_dropped()
        {
            Assert.That(WebResourceName.ForTable("con", "account", "brand/tables"),
                Is.EqualTo("con_/brand/tables/account.svg"));
            Assert.That(WebResourceName.ForTable("con", "account", ""), Is.EqualTo("con_/account.svg"));
        }

        [Test]
        public void The_same_table_always_gets_the_same_name()
        {
            // The point of a predictable name: a second run updates one web resource instead of
            // leaving a trail of near-duplicates with nothing pointing at them.
            Assert.That(WebResourceName.ForTable("new", "con_project"),
                Is.EqualTo(WebResourceName.ForTable("new", "con_project")));
        }

        [Test]
        public void A_good_name_passes()
        {
            Assert.That(WebResourceName.Validate("new_/icons/account.svg"), Is.Null);
            Assert.That(WebResourceName.Validate("con_/a.b_c/d.svg"), Is.Null);
        }

        [Test]
        public void Characters_Dataverse_rejects_are_named_in_the_message()
        {
            // The platform's own error does not say which character it disliked.
            var problem = WebResourceName.Validate("new_/icons/my icon.svg");

            Assert.That(problem, Does.Contain("space"));
        }

        [TestCase("", "needs a name")]
        [TestCase("   ", "needs a name")]
        [TestCase("/icons/a.svg", "prefix")]
        [TestCase("noprefix.svg", "publisher prefix")]
        [TestCase("new_/icons//a.svg", "double slash")]
        public void Bad_names_are_refused_with_a_reason(string name, string expected)
        {
            Assert.That(WebResourceName.Validate(name), Does.Contain(expected));
        }

        [Test]
        public void Over_long_names_are_refused()
        {
            var name = "new_/icons/" + new string('a', 300) + ".svg";

            Assert.That(WebResourceName.Validate(name), Does.Contain("256"));
        }

        [Test]
        public void Sanitizing_replaces_rather_than_drops()
        {
            // Dropping would collapse "my icon" and "myicon" onto one name.
            Assert.That(WebResourceName.Sanitize("My Icon!"), Is.EqualTo("my_icon_"));
            Assert.That(WebResourceName.Sanitize("con_project"), Is.EqualTo("con_project"));
        }

        [Test]
        public void A_table_name_with_awkward_characters_still_produces_a_valid_name()
        {
            var name = WebResourceName.ForTable("new", "My Table");

            Assert.That(WebResourceName.Validate(name), Is.Null);
        }
    }

    [TestFixture]
    public class PublishXmlTests
    {
        [Test]
        public void Both_components_go_in_one_request()
        {
            // Publishing only one of them leaves the app pointing at an icon that is not there
            // yet, or still showing the old one.
            var id = Guid.NewGuid();
            var xml = XDocument.Parse(PublishXml.For(new[] { id }, new[] { "account" }));

            Assert.That(xml.Root.Name.LocalName, Is.EqualTo("importexportxml"));
            Assert.That(xml.Descendants("entity").Single().Value, Is.EqualTo("account"));
            Assert.That(xml.Descendants("webresource").Single().Value, Is.EqualTo(id.ToString("D")));
        }

        [Test]
        public void Table_names_are_lowercased_and_deduplicated()
        {
            var xml = XDocument.Parse(PublishXml.For(new Guid[0], new[] { "Account", "account", "  contact " }));

            Assert.That(xml.Descendants("entity").Select(e => e.Value), Is.EqualTo(new[] { "account", "contact" }));
        }

        [Test]
        public void Repeated_web_resources_are_named_once()
        {
            var id = Guid.NewGuid();
            var xml = XDocument.Parse(PublishXml.For(new[] { id, id }, new[] { "account" }));

            Assert.That(xml.Descendants("webresource").Count(), Is.EqualTo(1));
        }

        [Test]
        public void Either_half_may_be_empty()
        {
            Assert.That(PublishXml.For(new[] { Guid.NewGuid() }, null), Does.Not.Contain("<entities>"));
            Assert.That(PublishXml.For(null, new[] { "account" }), Does.Not.Contain("<webresources>"));
        }

        [Test]
        public void Publishing_nothing_is_refused()
        {
            Assert.That(() => PublishXml.For(null, null), Throws.TypeOf<ArgumentException>());
            Assert.That(() => PublishXml.For(new Guid[0], new string[0]), Throws.TypeOf<ArgumentException>());
        }
    }
}
