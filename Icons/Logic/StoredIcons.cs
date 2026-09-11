using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Icons.Logic
{
    /// <summary>
    /// The drawings behind web resource names: what an environment is actually holding, as
    /// opposed to what its tables say they point at.
    ///
    /// Two things want this. The preview needs the icons the app around ours is drawn with,
    /// and the window needs the icon the chosen table already carries so it can say which of
    /// the library's it is. Both are the same question, asked in one query rather than one
    /// each, and a name that comes back with nothing usable is simply absent from the answer.
    /// </summary>
    public static class StoredIcons
    {
        /// <summary>
        /// How many names go in one query. The platform takes a long IN list, but a URL-encoded
        /// one has a ceiling and an org with a thousand icons is not the place to find it.
        /// </summary>
        private const int Batch = 200;

        public static Dictionary<string, string> Read(IOrganizationService service, IEnumerable<string> names)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var wanted = (names ?? Enumerable.Empty<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var at = 0; at < wanted.Count; at += Batch)
            {
                var batch = wanted.Skip(at).Take(Batch).Cast<object>().ToArray();

                var records = service.RetrieveMultiple(new QueryExpression("webresource")
                {
                    ColumnSet = new ColumnSet("name", "content"),
                    Criteria = new FilterExpression
                    {
                        Conditions = { new ConditionExpression("name", ConditionOperator.In, batch) }
                    },
                    TopCount = Batch
                });

                foreach (var record in records.Entities)
                {
                    var name = record.GetAttributeValue<string>("name");
                    var content = record.GetAttributeValue<string>("content");
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(content)) continue;

                    try
                    {
                        found[name] = Encoding.UTF8.GetString(Convert.FromBase64String(content));
                    }
                    catch (FormatException)
                    {
                        // Not base64, so not an SVG anything here can read. Nothing to say about
                        // it that a caller could act on, and an icon missing from a preview or
                        // unnamed under a table is not an error.
                    }
                }
            }

            return found;
        }
    }
}
