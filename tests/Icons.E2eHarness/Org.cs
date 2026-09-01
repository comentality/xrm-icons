using System;
using System.Collections.Generic;
using System.Linq;
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

        private readonly IOrganizationService _service;

        public Org(IOrganizationService service)
        {
            _service = service;
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
