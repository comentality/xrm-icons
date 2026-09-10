using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
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

        /// <summary>
        /// Whether this icon's licence asks for a credit line, rather than only that its notice
        /// travel with the software. False for every family bundled today, so the attribution
        /// page is a courtesy and a failure to write it does not stop the icon being applied.
        /// True makes the page the price of the icon: if it cannot be written, nothing is,
        /// because an attributed icon in a customer's environment with nothing crediting it is
        /// the one outcome this must not produce.
        /// </summary>
        public bool AttributionRequired { get; set; }
    }

    /// <summary>
    /// A web resource an apply wrote over, and what was in it before. Kept so that taking the
    /// apply back can put the drawing back as well as the name the table points at: applying
    /// twice to the same table reuses one name, so on the second run the old icon is not
    /// somewhere else under another name - it is gone unless this holds it.
    /// </summary>
    public sealed class OverwrittenResource
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        /// <summary>The base64 content as it stood before, or null if it held none.</summary>
        public string ContentBase64 { get; set; }
    }

    /// <summary>What actually happened, for the status line and the log.</summary>
    public sealed class IconApplyResult
    {
        public List<string> Steps { get; } = new List<string>();
        public List<Guid> WebResourceIds { get; } = new List<Guid>();
        public bool Published { get; set; }

        /// <summary>
        /// What the table's four icon properties held when this apply read them, straight from
        /// the org rather than from whatever the window remembered. This is what putting the
        /// change back writes.
        /// </summary>
        public string PreviousVectorIcon { get; set; }
        public string PreviousSmallIcon { get; set; }
        public string PreviousMediumIcon { get; set; }
        public string PreviousLargeIcon { get; set; }

        /// <summary>The web resources this apply overwrote, and what was in each before.</summary>
        public List<OverwrittenResource> Overwritten { get; } = new List<OverwrittenResource>();
    }

    /// <summary>What a table's icon was before an apply, and what that apply wrote over.</summary>
    public sealed class IconRevertRequest
    {
        public string TableLogicalName { get; set; }

        /// <summary>What IconVectorName held before. Empty puts the table back to having no icon.</summary>
        public string VectorName { get; set; }

        /// <summary>
        /// Whether the apply took the three classic slots. Only then are they put back: an apply
        /// that left them alone has nothing to undo about them, and writing what they happened
        /// to hold would be this tool changing something it never touched.
        /// </summary>
        public bool RestoreRasterIcons { get; set; }

        public string SmallName { get; set; }
        public string MediumName { get; set; }
        public string LargeName { get; set; }

        /// <summary>The web resources the apply wrote over, from its own result.</summary>
        public IList<OverwrittenResource> Overwritten { get; set; }

        /// <summary>Solution the table's update belongs to, or null for none.</summary>
        public string SolutionUniqueName { get; set; }
    }

    /// <summary>What putting it back actually did.</summary>
    public sealed class IconRevertResult
    {
        public List<string> Steps { get; } = new List<string>();
        public bool Published { get; set; }
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

            // 3. The page that says where these drawings came from, rebuilt from the folder as
            // it now stands. Before the table update rather than after it, so that a licence
            // which demands the credit can stop the apply while the icon is still a file
            // nothing points at.
            Stop(cancelled);
            WriteAttributionPage(service, request, result, cancelled, progress);

            // 4. Point the table at them.
            Stop(cancelled);
            progress("Reading the definition of " + request.TableLogicalName);
            var metadata = RetrieveTable(service, request.TableLogicalName);

            result.PreviousVectorIcon = metadata.IconVectorName;
            result.PreviousSmallIcon = metadata.IconSmallName;
            result.PreviousMediumIcon = metadata.IconMediumName;
            result.PreviousLargeIcon = metadata.IconLargeName;

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

            // 5. Both components, one publish. Either alone leaves the app looking at the old
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

        /// <summary>
        /// Puts a table back the way an apply found it: the drawings that were written over
        /// restored, the icon properties pointed back at the names they held before, and both
        /// published. The same four steps as Apply, in the same order, with the old values - and
        /// the same cancellation checks between them, because it is the same slow link.
        ///
        /// What it does not do is delete. A web resource this tool created is left where it is
        /// with nothing pointing at it: deleting one is not the reverse of writing a file, it is
        /// its own decision, and a solution the customer ships is no place for this tool to make
        /// that decision uninvited.
        ///
        /// It is safe to run twice. Every step writes a value rather than a difference, so a
        /// revert that failed halfway can simply be asked again.
        /// </summary>
        public static IconRevertResult Revert(
            IOrganizationService service,
            IconRevertRequest request,
            Func<bool> cancelled = null,
            Action<string> progress = null)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.TableLogicalName))
                throw new ArgumentException("No table to put back.", nameof(request));

            cancelled = cancelled ?? (() => false);
            progress = progress ?? (_ => { });

            var result = new IconRevertResult();
            var restored = new List<Guid>();

            // 1. The drawings that were written over, back as they were.
            foreach (var resource in request.Overwritten ?? new OverwrittenResource[0])
            {
                Stop(cancelled);
                progress("Putting back " + resource.Name);

                var content = new Entity("webresource", resource.Id);
                content["content"] = resource.ContentBase64;
                service.Update(content);

                restored.Add(resource.Id);
                result.Steps.Add("Put the web resource " + resource.Name + " back as it was");
            }

            // 2. Point the table back.
            Stop(cancelled);
            progress("Reading the definition of " + request.TableLogicalName);
            var metadata = RetrieveTable(service, request.TableLogicalName);

            if (metadata.IsCustomizable != null && !metadata.IsCustomizable.Value)
            {
                throw new InvalidOperationException(
                    "The table " + request.TableLogicalName + " is locked against customisation, " +
                    "so its icon cannot be changed back.");
            }

            metadata.IconVectorName = OrNothing(request.VectorName);
            if (request.RestoreRasterIcons)
            {
                metadata.IconSmallName = OrNothing(request.SmallName);
                metadata.IconMediumName = OrNothing(request.MediumName);
                metadata.IconLargeName = OrNothing(request.LargeName);
            }

            Stop(cancelled);
            progress("Setting the icon on " + request.TableLogicalName);

            var update = new UpdateEntityRequest { Entity = metadata, MergeLabels = true };
            if (!string.IsNullOrWhiteSpace(request.SolutionUniqueName))
                update.SolutionUniqueName = request.SolutionUniqueName;

            service.Execute(update);
            result.Steps.Add("Put the icon on " + request.TableLogicalName + " back to " +
                             (OrNothing(request.VectorName) ?? "none"));

            // 3. One publish, for the same reason the apply had one: a table pointing at a
            // drawing that is still the new one is the change only half taken back.
            Stop(cancelled);
            progress("Publishing");
            service.Execute(new PublishXmlRequest
            {
                ParameterXml = PublishXml.For(restored, new[] { request.TableLogicalName })
            });

            result.Published = true;
            result.Steps.Add(restored.Count == 0
                ? "Published the table"
                : "Published the table and " + restored.Count + " web resource" +
                  (restored.Count == 1 ? "" : "s"));

            return result;
        }

        /// <summary>
        /// What to write into an icon property that held nothing before.
        ///
        /// An empty string, and not null, which is the opposite of what it looks like it should
        /// be. A null in one of these properties is read as "this one is not part of the update"
        /// and the icon stays exactly where it was - so an undo of the first icon a table ever
        /// had would publish, report success, and change nothing. Measured against a real org on
        /// 10 September 2026: tests/e2e.ps1 asks the platform this question directly, because it
        /// is the platform's answer and not this tool's.
        /// </summary>
        private static string OrNothing(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? "" : name.Trim();
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
        /// Rewrites the attribution page beside the icon just stored, from every icon in that
        /// folder that says where it came from.
        ///
        /// Read back from the environment rather than assembled from what this session
        /// remembers: the folder holds icons written by other people, other machines and older
        /// versions of this tool, and a page that credited only the one icon in front of us
        /// would be worse than none. An icon carrying no provenance is left out rather than
        /// guessed at.
        /// </summary>
        private static void WriteAttributionPage(
            IOrganizationService service,
            IconApplyRequest request,
            IconApplyResult result,
            Func<bool> cancelled,
            Action<string> progress)
        {
            var name = AttributionPage.NameFor(request.WebResourceName);

            try
            {
                progress("Reading what is stored in " + AttributionPage.FolderOf(request.WebResourceName));
                var stored = AttributionPage.Read(ReadIconSvgs(service, request.WebResourceName));

                Stop(cancelled);
                progress("Writing " + name);

                var html = AttributionPage.Build(stored, DateTime.Now);
                var id = UpsertWebResource(
                    service, name, AttributionPage.WebResourceTypeHtml,
                    SvgOutput.ToBase64(html), request.SolutionUniqueName, result);

                result.WebResourceIds.Add(id);
            }
            catch (CancelledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (request.AttributionRequired)
                {
                    throw new InvalidOperationException(
                        "This icon's licence asks for a credit line, and the page carrying it (" + name +
                        ") could not be written: " + ex.Message + " The icon has not been put on the table.", ex);
                }

                // A courtesy that failed is not a reason to leave the table without its icon.
                result.Steps.Add("Could not update " + name + " (" + ex.Message + ")");
            }
        }

        /// <summary>
        /// Every SVG web resource in the same folder as the icon being stored, by name and
        /// text.
        ///
        /// Unpublished, because the icon this apply just wrote is unpublished until the publish
        /// two steps from here - and a page rebuilt from the published view would be a page
        /// missing the very icon it was rebuilt for.
        /// </summary>
        private static IEnumerable<KeyValuePair<string, string>> ReadIconSvgs(
            IOrganizationService service, string iconWebResourceName)
        {
            var folder = AttributionPage.FolderOf(iconWebResourceName);

            var query = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("name", "content"),
                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression("webresourcetype", ConditionOperator.Equal, WebResourceTypeSvg),
                        new ConditionExpression("name", ConditionOperator.BeginsWith, folder)
                    }
                },
                // Paged, and the pages followed. A folder with more icons in it than one page
                // holds is unlikely; a page that quietly credited the first five thousand and
                // dropped the rest is the one way this can be wrong without anything saying so.
                Orders = { new OrderExpression("name", OrderType.Ascending) },
                PageInfo = new PagingInfo { Count = 500, PageNumber = 1 }
            };

            var records = new List<Entity>();
            while (true)
            {
                var response = (RetrieveUnpublishedMultipleResponse)service.Execute(
                    new RetrieveUnpublishedMultipleRequest { Query = query });

                records.AddRange(response.EntityCollection.Entities);
                if (!response.EntityCollection.MoreRecords) break;

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = response.EntityCollection.PagingCookie;
            }

            var found = new List<KeyValuePair<string, string>>();
            foreach (var record in records)
            {
                var name = record.GetAttributeValue<string>("name");
                var content = record.GetAttributeValue<string>("content");
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(content)) continue;

                string svg;
                try
                {
                    svg = new UTF8Encoding(false).GetString(Convert.FromBase64String(content));
                }
                catch (FormatException)
                {
                    // Not base64 at all. Somebody else's web resource, and not this tool's
                    // business to complain about.
                    continue;
                }

                found.Add(new KeyValuePair<string, string>(name, svg));
            }

            return found;
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

                result.Overwritten.Add(new OverwrittenResource
                {
                    Id = existing.Id,
                    Name = name,
                    ContentBase64 = existing.GetAttributeValue<string>("content")
                });

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
                // Content as well as the rest of it: what is in there now is the only copy of
                // the drawing this write is about to replace, and after the update there is
                // nowhere left to read it from.
                ColumnSet = new ColumnSet("webresourceid", "name", "webresourcetype", "ismanaged", "content"),
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
