using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace Icons.E2eHarness
{
    /// <summary>
    /// The Dataverse side of the run: a table of our own to put icons on, and the queries that
    /// check what the tool actually left there. Everything here is scoped to names the run owns,
    /// so cleanup cannot reach anything that was in the environment before it.
    /// </summary>
    public class Org
    {
        public const string TableLogicalName = "new_e2eicontest";

        /// <summary>Where the run pretends this org has always kept its icons.</summary>
        public const string SeedFolder = "e2eguess";

        private readonly IOrganizationService _service;

        public Org(IOrganizationService service)
        {
            _service = service;
        }

        /// <summary>An unmanaged solution and its publisher prefix, as the tool sees them.</summary>
        public sealed class SolutionRef
        {
            public string UniqueName;
            public string Prefix;
        }

        /// <summary>
        /// The solution this run writes through, chosen the way the tool chooses one: a real
        /// unmanaged solution if the environment has any, Default only if it has nothing else.
        /// Its publisher prefix is what a web resource created here has to be named with, so
        /// guessing it rather than assuming "new" is what makes the run portable between orgs.
        /// </summary>
        public SolutionRef PickSolution()
        {
            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("uniquename"),
                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression("isvisible", ConditionOperator.Equal, true),
                        new ConditionExpression("ismanaged", ConditionOperator.Equal, false)
                    }
                },
                Orders = { new OrderExpression("friendlyname", OrderType.Ascending) }
            };
            var publisher = query.AddLink("publisher", "publisherid", "publisherid", JoinOperator.Inner);
            publisher.EntityAlias = "p";
            publisher.Columns = new ColumnSet("customizationprefix");

            var solutions = _service.RetrieveMultiple(query).Entities
                .Select(e => new SolutionRef
                {
                    UniqueName = e.GetAttributeValue<string>("uniquename"),
                    Prefix = e.GetAttributeValue<AliasedValue>("p.customizationprefix")?.Value as string
                })
                .Where(s => !string.IsNullOrWhiteSpace(s.UniqueName) && !string.IsNullOrWhiteSpace(s.Prefix))
                .ToList();

            var chosen = solutions.FirstOrDefault(s =>
                             !s.UniqueName.Equals("Default", StringComparison.OrdinalIgnoreCase) &&
                             !s.UniqueName.Equals("Active", StringComparison.OrdinalIgnoreCase))
                         ?? solutions.FirstOrDefault();

            if (chosen == null)
                throw new InvalidOperationException(
                    "This environment offers no unmanaged solution with a publisher prefix, and the "
                    + "tool now requires one. Nothing here can be run against it.");

            return chosen;
        }

        /// <summary>
        /// Gives the fixture table an icon somebody else could have set years ago, in a folder
        /// this tool would never have guessed. That is the whole fixture: the tool is supposed to
        /// offer the name the table already carries rather than a name of its own invention.
        /// </summary>
        public string SeedIcon(SolutionRef solution)
        {
            var name = solution.Prefix + "_/" + SeedFolder + "/seed.svg";
            const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\">"
                               + "<path d=\"M4 4h16v16H4z\" fill=\"currentColor\"/></svg>";
            var content = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));

            var existing = WebResourcesNamed(name).FirstOrDefault();
            if (existing != null)
            {
                _service.Update(new Entity("webresource", existing.Id) { ["content"] = content });
            }
            else
            {
                var create = new CreateRequest
                {
                    Target = new Entity("webresource")
                    {
                        ["name"] = name,
                        ["displayname"] = name,
                        ["webresourcetype"] = new OptionSetValue(11),
                        ["content"] = content
                    }
                };
                create.Parameters["SolutionUniqueName"] = solution.UniqueName;
                _service.Execute(create);
            }

            var metadata = TableIcons();
            metadata.IconVectorName = name;
            _service.Execute(new UpdateEntityRequest { Entity = metadata, MergeLabels = true });

            // Published, because the tool reads the org the way XrmToolBox sees it and an icon
            // nobody published is an icon the run would be asserting about on its own say-so.
            _service.Execute(new PublishXmlRequest
            {
                ParameterXml = "<importexportxml><entities><entity>" + TableLogicalName
                               + "</entity></entities></importexportxml>"
            });

            return name;
        }

        /// <summary>
        /// The unmanaged solutions a web resource has been filed in. The point of requiring a
        /// solution is that the thing lands somewhere shippable, and that is only checkable here.
        /// </summary>
        public List<string> SolutionsHolding(Guid webResourceId)
        {
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("solutionid"),
                Criteria =
                {
                    Conditions = { new ConditionExpression("objectid", ConditionOperator.Equal, webResourceId) }
                }
            };
            var solution = query.AddLink("solution", "solutionid", "solutionid", JoinOperator.Inner);
            solution.EntityAlias = "s";
            solution.Columns = new ColumnSet("uniquename");

            return _service.RetrieveMultiple(query).Entities
                .Select(e => e.GetAttributeValue<AliasedValue>("s.uniquename")?.Value as string)
                .Where(name => name != null)
                .Distinct()
                .ToList();
        }

        /// <summary>Creates the fixture table when it is not already there, published and ready.</summary>
        public void EnsureTable()
        {
            if (TableExists()) return;

            Console.WriteLine("        creating " + TableLogicalName + " (a real table takes a while)");
            _service.Execute(new CreateEntityRequest
            {
                Entity = new EntityMetadata
                {
                    SchemaName = TableLogicalName,
                    DisplayName = new Microsoft.Xrm.Sdk.Label("E2E icon test", 1033),
                    DisplayCollectionName = new Microsoft.Xrm.Sdk.Label("E2E icon tests", 1033),
                    Description = new Microsoft.Xrm.Sdk.Label(
                        "Created by tests\\e2e.ps1 to receive a test icon; deleted by the same run.", 1033),
                    OwnershipType = OwnershipTypes.UserOwned,
                    IsActivity = false
                },
                PrimaryAttribute = new StringAttributeMetadata
                {
                    SchemaName = "new_name",
                    MaxLength = 100,
                    FormatName = StringFormatName.Text,
                    RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.None),
                    DisplayName = new Microsoft.Xrm.Sdk.Label("Name", 1033)
                },
                HasNotes = false,
                HasActivities = false
            });

            // The tool reads published metadata, the way XrmToolBox connections see the org. A
            // table that exists but was never published would read as a table that is not there.
            _service.Execute(new PublishXmlRequest
            {
                ParameterXml = "<importexportxml><entities><entity>" + TableLogicalName
                               + "</entity></entities></importexportxml>"
            });
        }

        public bool TableExists()
        {
            try
            {
                _service.Execute(new RetrieveEntityRequest
                {
                    LogicalName = TableLogicalName,
                    EntityFilters = EntityFilters.Entity
                });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The table's icon properties as the org holds them right now, unpublished included.</summary>
        public EntityMetadata TableIcons()
        {
            var response = (RetrieveEntityResponse)_service.Execute(new RetrieveEntityRequest
            {
                LogicalName = TableLogicalName,
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            });
            return response.EntityMetadata;
        }

        /// <summary>Every web resource carrying the given name. One is right; zero or two is a finding.</summary>
        public List<Entity> WebResourcesNamed(string name)
        {
            var query = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("webresourceid", "name", "webresourcetype", "content", "ismanaged")
            };
            query.Criteria.AddCondition("name", ConditionOperator.Equal, name);
            return _service.RetrieveMultiple(query).Entities.ToList();
        }

        /// <summary>
        /// Removes everything the run created: the table first, because it is what references the
        /// web resources, and a referenced web resource refuses to be deleted.
        /// </summary>
        public List<string> Cleanup(IEnumerable<string> webResourceNames)
        {
            var trouble = new List<string>();

            if (TableExists())
            {
                try
                {
                    _service.Execute(new DeleteEntityRequest { LogicalName = TableLogicalName });
                }
                catch (Exception ex)
                {
                    trouble.Add("could not delete " + TableLogicalName + ": " + ex.Message);
                }
            }

            foreach (var name in webResourceNames.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct())
            {
                foreach (var resource in WebResourcesNamed(name))
                {
                    try
                    {
                        _service.Delete("webresource", resource.Id);
                    }
                    catch (Exception ex)
                    {
                        trouble.Add("could not delete web resource " + name + ": " + ex.Message);
                    }
                }
            }

            return trouble;
        }
    }
}
