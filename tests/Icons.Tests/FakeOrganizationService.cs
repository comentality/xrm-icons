using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace Icons.Tests
{
    /// <summary>
    /// A Dataverse that answers from a dictionary, logs every call, and can be told to take its
    /// time. The latency is what makes the cancellation and ordering tests mean anything: with
    /// an instant fake, code that never checks for cancellation passes.
    ///
    /// The answer is settled when the question arrives and only then does the call sleep. A fake
    /// that sleeps first and reads its state afterwards would hand a three-second-old question
    /// today's data, and quietly decide every ordering test in the tool's favour.
    /// </summary>
    public sealed class FakeOrganizationService : IOrganizationService
    {
        private readonly Dictionary<Guid, Entity> _webResources = new Dictionary<Guid, Entity>();

        public List<string> Calls { get; } = new List<string>();

        /// <summary>How long every call takes. Zero by default.</summary>
        public TimeSpan Latency { get; set; } = TimeSpan.Zero;

        /// <summary>The most rows this org will hand back in one page, whatever was asked for.</summary>
        public int PageCap { get; set; } = int.MaxValue;

        /// <summary>Table definitions this org will admit to, by logical name.</summary>
        public Dictionary<string, EntityMetadata> Tables { get; } =
            new Dictionary<string, EntityMetadata>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The definition as it stood after the last UpdateEntityRequest.</summary>
        public EntityMetadata LastUpdatedTable { get; private set; }

        public bool LastUpdateMergedLabels { get; private set; }

        /// <summary>The ParameterXml of the last publish, or null if nothing was published.</summary>
        public string LastPublishXml { get; private set; }

        /// <summary>Thrown by the next call of this name, once.</summary>
        public Dictionary<string, Exception> FailOnce { get; } = new Dictionary<string, Exception>();

        /// <summary>Runs just before each call returns, to change the world mid-sequence.</summary>
        public Action<string> OnCall { get; set; }

        public Entity AddWebResource(string name, int type, bool managed = false)
        {
            var record = new Entity("webresource", Guid.NewGuid())
            {
                ["name"] = name,
                ["webresourcetype"] = new OptionSetValue(type),
                ["ismanaged"] = managed,
                ["content"] = ""
            };
            _webResources[record.Id] = record;
            return record;
        }

        public Entity WebResource(string name)
        {
            return _webResources.Values.FirstOrDefault(
                e => string.Equals(e.GetAttributeValue<string>("name"), name, StringComparison.OrdinalIgnoreCase));
        }

        public int WebResourceCount
        {
            get { return _webResources.Count; }
        }

        public static EntityMetadata Table(string logicalName, bool customizable = true, string existingVectorIcon = null)
        {
            var metadata = new EntityMetadata { LogicalName = logicalName };

            // EntityMetadata's interesting properties have no public setters, which is the whole
            // reason XrmToolBox tools are awkward to test. The platform sets them on the way in
            // over the wire; here they are set the same way the deserialiser would.
            Set(metadata, "IsCustomizable", new BooleanManagedProperty(customizable));
            if (existingVectorIcon != null) metadata.IconVectorName = existingVectorIcon;

            return metadata;
        }

        private static void Set(object target, string property, object value)
        {
            var info = target.GetType().GetProperty(property);
            info.SetValue(target, value, null);
        }

        /// <summary>
        /// The three conditions the tool actually asks with: a name equal to one, a name
        /// beginning with a folder, and a web resource type. Anything else is a question this
        /// fake has never been asked, and it says so rather than answering wrongly.
        /// </summary>
        private static bool Matches(Entity record, ConditionExpression condition)
        {
            if (condition.AttributeName == "webresourcetype")
            {
                var type = record.GetAttributeValue<OptionSetValue>("webresourcetype");
                return type != null && type.Value == Convert.ToInt32(condition.Values[0]);
            }

            if (condition.AttributeName != "name")
                throw new NotSupportedException("This fake knows nothing about " + condition.AttributeName + ".");

            var name = record.GetAttributeValue<string>("name") ?? "";
            var wanted = condition.Values[0] as string ?? "";

            switch (condition.Operator)
            {
                case ConditionOperator.Equal:
                    return string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase);
                case ConditionOperator.BeginsWith:
                    return name.StartsWith(wanted, StringComparison.OrdinalIgnoreCase);
                default:
                    throw new NotSupportedException("This fake cannot answer a " + condition.Operator + " on a name.");
            }
        }

        private void Record(string call)
        {
            Calls.Add(call);

            Exception failure;
            if (FailOnce.TryGetValue(call, out failure))
            {
                FailOnce.Remove(call);
                if (Latency > TimeSpan.Zero) Thread.Sleep(Latency);
                throw failure;
            }
        }

        private void Settle(string call)
        {
            if (Latency > TimeSpan.Zero) Thread.Sleep(Latency);
            OnCall?.Invoke(call);
        }

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            var name = request.RequestName ?? request.GetType().Name;
            Record(name);

            var unpublished = request as RetrieveUnpublishedMultipleRequest;
            if (unpublished != null)
            {
                var query = (QueryExpression)unpublished.Query;

                var matches = _webResources.Values
                    .Where(e => query.Criteria.Conditions.All(c => Matches(e, c)))
                    .OrderBy(e => e.GetAttributeValue<string>("name"), StringComparer.OrdinalIgnoreCase)
                    .Select(e => e.Clone())
                    .ToList();

                // A page no bigger than the caller asked for, and no bigger than this fake was
                // told to hand out - which is how paging gets tested without five thousand rows.
                var size = Math.Min(PageCap, query.PageInfo?.Count > 0 ? query.PageInfo.Count : int.MaxValue);
                var skip = ((query.PageInfo?.PageNumber ?? 1) - 1) * size;
                var page = matches.Skip(skip).Take(size).ToList();

                Settle(name);
                var collection = new EntityCollection(page)
                {
                    MoreRecords = skip + page.Count < matches.Count,
                    PagingCookie = "page " + (query.PageInfo?.PageNumber ?? 1)
                };

                return new RetrieveUnpublishedMultipleResponse
                {
                    Results = { { "EntityCollection", collection } }
                };
            }

            var create = request as CreateRequest;
            if (create != null)
            {
                var record = create.Target;
                if (record.Id == Guid.Empty) record.Id = Guid.NewGuid();
                _webResources[record.Id] = record;

                Settle(name);
                return new CreateResponse { Results = { { "id", record.Id } } };
            }

            var retrieveEntity = request as RetrieveEntityRequest;
            if (retrieveEntity != null)
            {
                EntityMetadata metadata;
                if (!Tables.TryGetValue(retrieveEntity.LogicalName, out metadata))
                    throw new FaultException<OrganizationServiceFault>(
                        new OrganizationServiceFault { Message = "Could not find the table " + retrieveEntity.LogicalName });

                Settle(name);
                return new RetrieveEntityResponse { Results = { { "EntityMetadata", metadata } } };
            }

            var updateEntity = request as UpdateEntityRequest;
            if (updateEntity != null)
            {
                LastUpdatedTable = updateEntity.Entity;
                LastUpdateMergedLabels = updateEntity.MergeLabels;

                Settle(name);
                return new UpdateEntityResponse();
            }

            var publish = request as PublishXmlRequest;
            if (publish != null)
            {
                LastPublishXml = publish.ParameterXml;

                Settle(name);
                return new PublishXmlResponse();
            }

            Settle(name);
            return new OrganizationResponse();
        }

        public Guid Create(Entity entity)
        {
            Record("Create");
            if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
            _webResources[entity.Id] = entity;
            Settle("Create");
            return entity.Id;
        }

        public void Update(Entity entity)
        {
            Record("Update");

            Entity existing;
            if (_webResources.TryGetValue(entity.Id, out existing))
            {
                foreach (var attribute in entity.Attributes)
                    existing[attribute.Key] = attribute.Value;
            }

            Settle("Update");
        }

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            Record("Retrieve");
            Entity found;
            _webResources.TryGetValue(id, out found);
            Settle("Retrieve");
            return found?.Clone();
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            Record("RetrieveMultiple");
            Settle("RetrieveMultiple");
            return new EntityCollection(_webResources.Values.Select(e => e.Clone()).ToList());
        }

        public void Delete(string entityName, Guid id)
        {
            Record("Delete");
            _webResources.Remove(id);
            Settle("Delete");
        }

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            Record("Associate");
            Settle("Associate");
        }

        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            Record("Disassociate");
            Settle("Disassociate");
        }
    }

    internal static class EntityExtensions
    {
        /// <summary>
        /// A copy, so a test that mutates what a query returned does not reach back into the
        /// fake's own store - which a real service could never allow.
        /// </summary>
        public static Entity Clone(this Entity source)
        {
            var copy = new Entity(source.LogicalName, source.Id);
            foreach (var attribute in source.Attributes) copy[attribute.Key] = attribute.Value;
            return copy;
        }
    }
}
