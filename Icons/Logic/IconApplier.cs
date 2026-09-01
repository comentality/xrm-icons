using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace Icons.Logic
{
    /// <summary>Which of a table's icon properties an image is being stored in.</summary>
    public enum IconSlot
    {
        /// <summary>IconVectorName. The one modern apps use, and the one Microsoft recommends.</summary>
        Vector,

        /// <summary>IconSmallName, a 16x16 PNG. The classic "icon in web application".</summary>
        Small,

        /// <summary>IconMediumName, a 32x32 PNG. The classic "icon for entity forms".</summary>
        Medium,

        /// <summary>
        /// IconLargeName, a 66x48 PNG. Microsoft's SDK reference says of this one that "this
        /// size icon is not currently used by the application" - it is written so that a table
        /// asked for the classic set gets the whole classic set, not because anything reads it.
        /// </summary>
        Large
    }

    /// <summary>What to put on which table.</summary>
    public sealed class IconApplyRequest
    {
        public string TableLogicalName { get; set; }

        /// <summary>The finished SVG, already coloured. What goes in the web resource verbatim.</summary>
        public string Svg { get; set; }

        /// <summary>What to store it under. Must already have passed WebResourceName.Validate.</summary>
        public string WebResourceName { get; set; }

        /// <summary>Solution to add new web resources to, or null for the default solution.</summary>
        public string SolutionUniqueName { get; set; }

        /// <summary>
        /// Also generate the three PNG icons the classic properties take. Off by default:
        /// Microsoft's guidance is to use the vector property instead of these, and every extra
        /// slot is another web resource to keep in step.
        /// </summary>
        public bool IncludeRasterIcons { get; set; }

        /// <summary>The drawing, needed only when raster icons are being generated.</summary>
        public SvgImage Image { get; set; }

        /// <summary>
        /// What colour to rasterise in. PNGs cannot follow the theme, so a colour has to be
        /// chosen here even when the vector icon is left theme-coloured.
        /// </summary>
        public Color RasterColour { get; set; }
    }

    /// <summary>What actually happened, for the status line and the log.</summary>
    public sealed class IconApplyResult
    {
        public List<string> Steps { get; } = new List<string>();
        public List<Guid> WebResourceIds { get; } = new List<Guid>();
        public bool Published { get; set; }
        public string PreviousVectorIcon { get; set; }
    }

    /// <summary>
    /// Puts an icon on a table: store the drawing as a web resource, point the table at it,
    /// publish both.
    ///
    /// Every round trip is separated by a cancellation check, because on a slow link the wait
    /// is mostly the calls that have not been made yet - the one already on the wire cannot be
    /// recalled, but the four behind it can.
    /// </summary>
    public static class IconApplier
    {
        /// <summary>webresourcetype 11, "Vector format (SVG)".</summary>
        private const int WebResourceTypeSvg = 11;

        /// <summary>webresourcetype 5, "PNG format".</summary>
        private const int WebResourceTypePng = 5;

        public sealed class CancelledException : Exception
        {
            public CancelledException() : base("Cancelled before the change was finished.") { }
        }

        public static IconApplyResult Apply(
            IOrganizationService service,
            IconApplyRequest request,
            Func<bool> cancelled = null,
            Action<string> progress = null)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.TableLogicalName))
                throw new ArgumentException("No table to put the icon on.", nameof(request));

            var problem = WebResourceName.Validate(request.WebResourceName);
            if (problem != null) throw new ArgumentException(problem, nameof(request));

            cancelled = cancelled ?? (() => false);
            progress = progress ?? (_ => { });

            var result = new IconApplyResult();
            var slots = new Dictionary<IconSlot, string>();

            // 1. The drawing itself.
            Stop(cancelled);
            progress("Storing " + request.WebResourceName);
            var vectorId = UpsertWebResource(
                service,
                request.WebResourceName,
                WebResourceTypeSvg,
                SvgOutput.ToBase64(request.Svg),
                request.SolutionUniqueName,
                result);
            result.WebResourceIds.Add(vectorId);
            slots[IconSlot.Vector] = request.WebResourceName;

            // 2. The raster copies, when asked for.
            if (request.IncludeRasterIcons)
            {
                if (request.Image == null)
                    throw new ArgumentException("Raster icons were asked for but no drawing was supplied.", nameof(request));

                foreach (var raster in RasterIcons.Slots)
                {
                    Stop(cancelled);

                    var name = RasterIcons.NameFor(request.WebResourceName, raster.Key);
                    progress("Storing " + name);

                    var png = ToPng(request.Image, raster.Value.Width, raster.Value.Height, request.RasterColour);
                    var id = UpsertWebResource(
                        service, name, WebResourceTypePng, SvgOutput.ToBase64(png),
                        request.SolutionUniqueName, result);

                    result.WebResourceIds.Add(id);
                    slots[raster.Key] = name;
                }
            }

            // 3. Point the table at them.
            Stop(cancelled);
            progress("Reading the definition of " + request.TableLogicalName);
            var metadata = RetrieveTable(service, request.TableLogicalName);

            result.PreviousVectorIcon = metadata.IconVectorName;

            if (metadata.IsCustomizable != null && !metadata.IsCustomizable.Value)
            {
                throw new InvalidOperationException(
                    "The table " + request.TableLogicalName + " is locked against customisation, " +
                    "so its icon cannot be changed.");
            }

            foreach (var slot in slots)
            {
                switch (slot.Key)
                {
                    case IconSlot.Vector: metadata.IconVectorName = slot.Value; break;
                    case IconSlot.Small: metadata.IconSmallName = slot.Value; break;
                    case IconSlot.Medium: metadata.IconMediumName = slot.Value; break;
                    case IconSlot.Large: metadata.IconLargeName = slot.Value; break;
                }
            }

            Stop(cancelled);
            progress("Setting the icon on " + request.TableLogicalName);

            var update = new UpdateEntityRequest
            {
                Entity = metadata,
                // The definition came back with every label in it; without this, updating it
                // would overwrite translations in languages this connection cannot even see.
                MergeLabels = true
            };
            if (!string.IsNullOrWhiteSpace(request.SolutionUniqueName))
                update.SolutionUniqueName = request.SolutionUniqueName;

            service.Execute(update);
            result.Steps.Add("Set the icon on " + request.TableLogicalName);

            // 4. Both components, one publish. Either alone leaves the app looking at the old
            // icon or at an icon that is not there yet.
            Stop(cancelled);
            progress("Publishing");
            service.Execute(new PublishXmlRequest
            {
                ParameterXml = PublishXml.For(result.WebResourceIds, new[] { request.TableLogicalName })
            });

            result.Published = true;
            result.Steps.Add("Published the table and " +
                             result.WebResourceIds.Count + " web resource" +
                             (result.WebResourceIds.Count == 1 ? "" : "s"));

            return result;
        }

        private static byte[] ToPng(SvgImage image, int width, int height, Color colour)
        {
            using (var bitmap = image.Render(width, height, colour))
            using (var buffer = new MemoryStream())
            {
                bitmap.Save(buffer, ImageFormat.Png);
                return buffer.ToArray();
            }
        }

        /// <summary>
        /// Creates the web resource, or updates the one already using that name. Reusing the
        /// name is the point: running the tool twice on a table should change one icon, not
        /// leave two behind with nothing pointing at the first.
        /// </summary>
        private static Guid UpsertWebResource(
            IOrganizationService service,
            string name,
            int type,
            string contentBase64,
            string solutionUniqueName,
            IconApplyResult result)
        {
            var existing = FindWebResource(service, name);

            if (existing != null)
            {
                var existingType = existing.GetAttributeValue<OptionSetValue>("webresourcetype");
                if (existingType != null && existingType.Value != type)
                {
                    throw new InvalidOperationException(
                        "A web resource called " + name + " already exists and is not the right kind " +
                        "(it is type " + existingType.Value + ", this needs type " + type + "). " +
                        "Pick another name, or delete that one first.");
                }

                if (existing.GetAttributeValue<bool>("ismanaged"))
                {
                    throw new InvalidOperationException(
                        "The web resource " + name + " came from a managed solution and cannot be changed.");
                }

                var update = new Entity("webresource", existing.Id);
                update["content"] = contentBase64;
                service.Update(update);

                result.Steps.Add("Updated the web resource " + name);
                return existing.Id;
            }

            var record = new Entity("webresource")
            {
                ["name"] = name,
                ["displayname"] = name,
                ["webresourcetype"] = new OptionSetValue(type),
                ["content"] = contentBase64
            };

            var create = new CreateRequest { Target = record };
            if (!string.IsNullOrWhiteSpace(solutionUniqueName))
                create.Parameters["SolutionUniqueName"] = solutionUniqueName;

            var response = (CreateResponse)service.Execute(create);
            result.Steps.Add("Created the web resource " + name);
            return response.id;
        }

        private static Entity FindWebResource(IOrganizationService service, string name)
        {
            var query = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("webresourceid", "name", "webresourcetype", "ismanaged"),
                Criteria =
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, name) }
                },
                TopCount = 1
            };

            // A web resource that exists but has never been published is invisible to a plain
            // RetrieveMultiple, and creating a second one with the same name then fails on the
            // server. Ask for the unpublished view so the tool sees what the platform sees.
            var response = (RetrieveUnpublishedMultipleResponse)service.Execute(
                new RetrieveUnpublishedMultipleRequest { Query = query });

            return response.EntityCollection.Entities.FirstOrDefault();
        }

        private static EntityMetadata RetrieveTable(IOrganizationService service, string logicalName)
        {
            var response = (RetrieveEntityResponse)service.Execute(new RetrieveEntityRequest
            {
                LogicalName = logicalName,
                // Only the table itself. Asking for attributes as well turns a small call into
                // a large one, and nothing here reads them.
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            });

            return response.EntityMetadata;
        }

        private static void Stop(Func<bool> cancelled)
        {
            if (cancelled()) throw new CancelledException();
        }
    }
}
