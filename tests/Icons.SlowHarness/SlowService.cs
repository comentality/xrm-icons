using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace Icons.SlowHarness
{
    /// <summary>One round trip, as it happened.</summary>
    public class Call
    {
        /// <summary>Position among every call of the run, from 1.</summary>
        public int Index;

        /// <summary>Position among the calls of this label, from 1. What a scenario scripts against.</summary>
        public int Nth;

        /// <summary>
        /// What was asked: tables, solutions, webresource-find, webresource-create,
        /// webresource-update, table-read, table-update, publish.
        /// </summary>
        public string Label;

        public DateTime Started;
        public DateTime Ended;

        public override string ToString()
        {
            return "#" + Index + " " + Label + " " + (int)(Ended - Started).TotalMilliseconds + "ms";
        }
    }

    /// <summary>
    /// The environment, at a distance. Answers everything the control and the applier ask, out
    /// of an in-memory org a scenario is free to differ per instance, and takes as long over
    /// each answer as the scenario says it should.
    ///
    /// Every call is logged, and the log is the assertion surface for the questions latency
    /// actually raises: was the same thing asked twice, was anything asked after the user gave
    /// up, did the answer that landed last belong to the question asked last.
    /// </summary>
    public class SlowService : IOrganizationService
    {
        private readonly object _lock = new object();
        private readonly List<Call> _calls = new List<Call>();
        private int _index;
        private readonly Dictionary<string, int> _perLabel = new Dictionary<string, int>();

        public readonly List<EntityMetadata> Tables = new List<EntityMetadata>();
        public readonly List<Entity> Solutions = new List<Entity>();
        private readonly Dictionary<Guid, Entity> _webResources = new Dictionary<Guid, Entity>();

        /// <summary>How long this call should take. The whole point of the harness.</summary>
        public Func<Call, int> Latency = call => 0;

        /// <summary>What this call should throw instead of answering, or null to answer.</summary>
        public Func<Call, Exception> Fails = call => null;

        /// <summary>An org with a few tables and two solutions, named so shots read well.</summary>
        public static SlowService Sampled(string flavour = "Contoso")
        {
            var service = new SlowService();
            service.Tables.Add(Table(flavour.ToLowerInvariant() + "_account", flavour + " Account"));
            service.Tables.Add(Table(flavour.ToLowerInvariant() + "_case", flavour + " Case"));
            service.Tables.Add(Table(flavour.ToLowerInvariant() + "_frozen", flavour + " Frozen", customizable: false));
            service.AddSolution("Default", "Default Solution", "new");
            service.AddSolution(flavour, flavour + " Customisations", flavour.Substring(0, 3).ToLowerInvariant());
            return service;
        }

        public void AddSolution(string uniqueName, string friendlyName, string prefix)
        {
            var row = new Entity("solution", Guid.NewGuid());
            row["uniquename"] = uniqueName;
            row["friendlyname"] = friendlyName;
            row["p.customizationprefix"] = new AliasedValue("publisher", "customizationprefix", prefix);
            Solutions.Add(row);
        }

        public static EntityMetadata Table(string logicalName, string schemaName, bool customizable = true,
                                          string icon = null)
        {
            var metadata = new EntityMetadata { LogicalName = logicalName, SchemaName = schemaName };
            // The interesting properties have no public setters; they are set the way the
            // deserialiser would set them on the way in over the wire.
            Set(metadata, "IsCustomizable", new BooleanManagedProperty(customizable));
            // This one does have a setter - the applier writes it - so a table that already
            // carries an icon is made the same way the tool would leave it.
            if (icon != null) metadata.IconVectorName = icon;
            return metadata;
        }

        private static void Set(object target, string property, object value)
        {
            target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public)
                .SetValue(target, value, null);
        }

        // ===== the log =====

        public List<Call> Log()
        {
            lock (_lock) return _calls.ToList();
        }

        public List<Call> Log(string label)
        {
            return Log().Where(c => c.Label == label).ToList();
        }

        private Call Begin(string label)
        {
            var call = new Call { Label = label, Started = DateTime.UtcNow };
            lock (_lock)
            {
                call.Index = ++_index;
                int nth;
                _perLabel.TryGetValue(label, out nth);
                _perLabel[label] = call.Nth = nth + 1;
                _calls.Add(call);
            }

            return call;
        }

        /// <summary>
        /// The answer as it stood when the question arrived, and then the wait. That order is
        /// load bearing: a fake that slept first and read afterwards would hand a question asked
        /// three seconds ago today's data, which quietly resolves every staleness race in the
        /// harness's favour. A failure waits just as long as a success.
        /// </summary>
        private T Answer<T>(Call call, Func<T> answer)
        {
            var failure = Fails(call);
            var result = failure == null ? answer() : default(T);

            var delay = Latency(call);
            if (delay > 0) Thread.Sleep(delay);

            lock (_lock) call.Ended = DateTime.UtcNow;
            if (failure != null) throw failure;
            return result;
        }

        // ===== what the tool asks =====

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            if (request is RetrieveAllEntitiesRequest)
            {
                return Answer(Begin("tables"), () =>
                    (OrganizationResponse)new RetrieveAllEntitiesResponse
                    {
                        Results = { { "EntityMetadata", TableRows() } }
                    });
            }

            var unpublished = request as RetrieveUnpublishedMultipleRequest;
            if (unpublished != null)
            {
                var wanted = ((QueryExpression)unpublished.Query).Criteria.Conditions
                    .First(c => c.AttributeName == "name").Values[0] as string;

                return Answer(Begin("webresource-find"), () =>
                    (OrganizationResponse)new RetrieveUnpublishedMultipleResponse
                    {
                        Results = { { "EntityCollection", new EntityCollection(FindWebResources(wanted)) } }
                    });
            }

            var create = request as CreateRequest;
            if (create != null)
            {
                return Answer(Begin("webresource-create"), () =>
                {
                    var record = create.Target;
                    lock (_lock)
                    {
                        if (record.Id == Guid.Empty) record.Id = Guid.NewGuid();
                        _webResources[record.Id] = record;
                    }
                    return (OrganizationResponse)new CreateResponse { Results = { { "id", record.Id } } };
                });
            }

            var retrieveEntity = request as RetrieveEntityRequest;
            if (retrieveEntity != null)
            {
                return Answer(Begin("table-read"), () =>
                {
                    EntityMetadata metadata;
                    lock (_lock)
                    {
                        metadata = Tables.FirstOrDefault(t => t.LogicalName == retrieveEntity.LogicalName);
                    }

                    if (metadata == null)
                        throw new FaultException<OrganizationServiceFault>(
                            new OrganizationServiceFault { Message = "No table called " + retrieveEntity.LogicalName });

                    return (OrganizationResponse)new RetrieveEntityResponse
                    {
                        Results = { { "EntityMetadata", metadata } }
                    };
                });
            }

            if (request is UpdateEntityRequest)
            {
                return Answer(Begin("table-update"), () => (OrganizationResponse)new UpdateEntityResponse());
            }

            if (request is PublishXmlRequest)
            {
                return Answer(Begin("publish"), () => (OrganizationResponse)new PublishXmlResponse());
            }

            throw new NotSupportedException("Nothing in the tool executes " + request.GetType().Name + ".");
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            var q = query as QueryExpression;
            if (q == null || q.EntityName != "solution")
                throw new NotSupportedException("The tool only queries solutions this way.");

            return Answer(Begin("solutions"), () =>
            {
                lock (_lock) return new EntityCollection(Solutions.Select(Clone).ToList());
            });
        }

        public void Update(Entity entity)
        {
            Answer<object>(Begin("webresource-update"), () =>
            {
                lock (_lock)
                {
                    Entity existing;
                    if (_webResources.TryGetValue(entity.Id, out existing))
                    {
                        foreach (var attribute in entity.Attributes) existing[attribute.Key] = attribute.Value;
                    }
                }
                return null;
            });
        }

        private EntityMetadata[] TableRows()
        {
            lock (_lock) return Tables.ToArray();
        }

        private List<Entity> FindWebResources(string name)
        {
            lock (_lock)
            {
                return _webResources.Values
                    .Where(e => string.Equals(e.GetAttributeValue<string>("name"), name,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(Clone)
                    .ToList();
            }
        }

        private static Entity Clone(Entity source)
        {
            var copy = new Entity(source.LogicalName, source.Id);
            foreach (var attribute in source.Attributes) copy[attribute.Key] = attribute.Value;
            return copy;
        }

        public Guid Create(Entity entity) { throw new NotSupportedException("The tool creates through CreateRequest only."); }
        public void Delete(string entityName, Guid id) { throw new NotSupportedException("The tool never deletes."); }
        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) { throw new NotSupportedException("The tool never associates."); }
        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) { throw new NotSupportedException("The tool never disassociates."); }
        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) { throw new NotSupportedException("The tool retrieves in bulk only."); }
    }
}
