using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Icons.Logic
{
    /// <summary>
    /// The ParameterXml that PublishXmlRequest takes.
    ///
    /// Setting a table icon touches two components and both have to be published, in the same
    /// call: the web resource holding the drawing, and the table pointing at it. Publish only
    /// the table and the app asks for an icon that is still unpublished; publish only the web
    /// resource and the table still points at whatever it did before.
    /// </summary>
    public static class PublishXml
    {
        public static string For(IEnumerable<Guid> webResourceIds, IEnumerable<string> tableLogicalNames)
        {
            var resources = (webResourceIds ?? Enumerable.Empty<Guid>()).Distinct().ToList();
            var tables = (tableLogicalNames ?? Enumerable.Empty<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            if (resources.Count == 0 && tables.Count == 0)
                throw new ArgumentException("Publishing nothing is not a request worth sending.");

            var root = new XElement("importexportxml");

            if (tables.Count > 0)
            {
                root.Add(new XElement("entities",
                    tables.Select(name => new XElement("entity", name))));
            }

            if (resources.Count > 0)
            {
                root.Add(new XElement("webresources",
                    resources.Select(id => new XElement("webresource", id.ToString("D")))));
            }

            return root.ToString(SaveOptions.DisableFormatting);
        }
    }
}
