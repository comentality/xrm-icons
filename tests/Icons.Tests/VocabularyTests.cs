using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Icons.Catalog;
using NUnit.Framework;

namespace Icons.Tests
{
    /// <summary>
    /// The library is Fluent's, but the words typed at it are Dataverse's, and the two
    /// vocabularies only partly overlap. This fixture is what keeps the bridge between them
    /// honest: every table below is one somebody really has, and every one of them has to
    /// arrive at the picker with a search already in the box.
    ///
    /// It exists because the gap was measured rather than assumed. On the first run a third of
    /// these tables produced no suggestion at all - Employee, Supplier, Budget, Milestone - and
    /// in every case the bundle held a perfectly good icon that the search simply could not
    /// reach from the table's own name. No amount of extra icons would have fixed that.
    /// </summary>
    [TestFixture]
    public class VocabularyTests
    {
        private IconCatalog _catalog;

        [OneTimeSetUp]
        public void LoadCatalog()
        {
            _catalog = IconCatalog.Load();
        }

        [OneTimeTearDown]
        public void DisposeCatalog()
        {
            _catalog?.Dispose();
        }

        /// <summary>
        /// Standard tables across sales, service, marketing, field service and the customisation
        /// surface, plus the business nouns custom tables are usually named after.
        /// </summary>
        private static readonly string[] StandardTables =
        {
            "Account", "Contact", "Lead", "Opportunity", "Quote", "Order", "Invoice", "Product",
            "Price List", "Price List Item", "Discount", "Discount List", "Unit", "Unit Group",
            "Currency", "Competitor", "Sales Literature", "Territory", "Site",
            "Case", "Incident", "Entitlement", "Entitlement Channel", "Service", "Service Activity",
            "Resource Group", "Facility", "Queue", "Queue Item", "Knowledge Article",
            "Knowledge Base Record", "Article", "Feedback", "Rating Model", "Characteristic",
            "Campaign", "Campaign Activity", "Campaign Response", "Marketing List", "List Member",
            "Quick Campaign",
            "Activity", "Appointment", "Email", "Phone Call", "Task", "Letter", "Fax",
            "Recurring Appointment", "Social Activity", "Social Profile",
            "Note", "Attachment", "Annotation", "Document Location", "Document Template",
            "Email Template", "Mailbox", "Email Signature",
            "User", "Team", "Business Unit", "Security Role", "Field Security Profile", "Position",
            "Hierarchy Rule",
            "Goal", "Goal Metric", "Rollup Query", "Report", "Dashboard", "Chart", "View",
            "Workflow", "Business Process Flow", "Plugin",
            "Solution", "Publisher", "Web Resource", "Environment Variable", "Connection",
            "Connection Role", "Connection Reference",
            "Duplicate Detection Rule", "Import", "Data Map", "Bulk Delete", "Audit", "Trace Log",
            "System Job", "Process Session",
            "Table", "Column", "Relationship", "Choice", "Form", "Site Map", "App",
            "Model-driven App", "Canvas App", "Flow",
            "Work Order", "Work Order Product", "Work Order Service", "Booking",
            "Bookable Resource", "Bookable Resource Booking", "Agreement", "Asset",
            "Customer Asset", "Inventory Adjustment", "Purchase Order", "Warehouse", "Time Entry",
            "Expense", "Project", "Project Task", "Project Team Member", "Time Off Request",
            "Approval", "Invoice Line", "Quote Line", "Order Line", "Contract", "Contract Line",
            "Contract Template", "Subject", "Sales Order", "Opportunity Product", "Payment",
            "Payment Term",
            "Vehicle", "Property", "Policy", "Claim", "Loan", "Application", "Student", "Course",
            "Enrollment", "Patient", "Appointment Slot", "Shipment", "Delivery", "Supplier",
            "Vendor", "Employee", "Department", "Branch", "Certificate", "License", "Permit",
            "Inspection", "Audit Finding", "Risk", "Issue", "Milestone", "Budget", "Forecast",
            "Commission", "Subscription", "Membership",
        };

        [Test]
        public void Every_standard_table_arrives_with_something_in_the_search_box()
        {
            var silent = StandardTables
                .Where(t => IconSearch.SeedFor(_catalog.Icons, t).Length == 0)
                .ToList();

            Assert.That(silent, Is.Empty,
                "these tables seed an empty search: " + string.Join(", ", silent) +
                ". The bundle almost certainly draws something for them - add the word to " +
                "IconSearch.Synonyms rather than reaching for another icon pack.");
        }

        [Test]
        public void A_table_the_library_really_says_nothing_about_still_seeds_nothing()
        {
            // The point above is coverage, not seeding something at any cost. A name the
            // library has no answer to must still leave the box alone.
            Assert.That(IconSearch.SeedFor(_catalog.Icons, "Zzzznotanicon"), Is.Empty);
        }

        /// <summary>
        /// What the measurement caught, by name. Each of these seeded a word the library only
        /// appeared to answer - a substring or a prefix of something else entirely - and each is
        /// a confident wrong answer in a box nobody filled in.
        /// </summary>
        [TestCase("Social Profile", "profile", "vehicle_car_profile is a car")]
        [TestCase("Field Security Profile", "profile", "and so is the one this table used to get")]
        [TestCase("Payment Term", "term", "terminal is a command prompt")]
        [TestCase("Hierarchy Rule", "rule", "ruler is a ruler")]
        [TestCase("Duplicate Detection Rule", "rule", "and so is this one's")]
        [TestCase("Time Entry", "entry", "brand-sentry is somebody's logo")]
        [TestCase("Business Unit", "unit", "shelving-unit is a bookcase")]
        [TestCase("Rating Model", "model", "box-model is a CSS diagram")]
        public void A_word_the_library_only_seems_to_answer_is_not_seeded(string table, string wrong, string why)
        {
            Assert.That(IconSearch.SeedFor(_catalog.Icons, table), Is.Not.EqualTo(wrong), why);
        }

        /// <summary>
        /// And what it falls back to instead. The rule is not "seed less": every one of these
        /// gets a better word than the one it lost, from the name it already had.
        /// </summary>
        [TestCase("Social Profile", "social")]
        [TestCase("Hierarchy Rule", "hierarchy")]
        [TestCase("Payment Term", "payment")]
        [TestCase("Queue Item", "queue")]
        [TestCase("Project Team Member", "team")]
        [TestCase("Sales Order", "order")]
        public void The_word_before_it_is_what_gets_seeded(string table, string expected)
        {
            Assert.That(IconSearch.SeedFor(_catalog.Icons, table), Is.EqualTo(expected));
        }

        [Test]
        public void A_word_some_icon_is_actually_called_is_seeded_on_its_own()
        {
            // Literal, so no curation is needed for it: the library really does draw the thing.
            Assert.That(IconSearch.SeedFor(_catalog.Icons, "Calendar"), Is.EqualTo("calendar"));
            Assert.That(IconSearch.SeedFor(_catalog.Icons, "Shipment Folder"), Is.EqualTo("folder"));
        }

        [Test]
        public void Every_word_the_vocabulary_maps_to_is_one_the_bundle_draws()
        {
            // An alias the bundle does not draw is dead weight that reads like coverage. Reached
            // by reflection rather than by widening the tool's surface for a test: the table is
            // an implementation detail of the search, and this is the only thing that reads it.
            var field = typeof(IconSearch).GetField("Synonyms", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, "IconSearch.Synonyms has been renamed; so has this test's grip on it");

            var vocabulary = (Dictionary<string, string[]>)field.GetValue(null);
            var dead = new List<string>();

            foreach (var entry in vocabulary)
                foreach (var alias in entry.Value)
                    if (!IconSearch.Rank(_catalog.Icons, alias).Any()) dead.Add(entry.Key + " -> " + alias);

            Assert.That(dead, Is.Empty, "these aliases name nothing the bundle draws: " + string.Join(", ", dead));
        }
    }
}
