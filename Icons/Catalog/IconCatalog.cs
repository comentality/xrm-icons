using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Icons.Catalog
{
    /// <summary>
    /// The icon library, read from the archive compiled into this assembly.
    ///
    /// Bundled rather than fetched: an XrmToolBox tool is often run against a customer tenant
    /// from a laptop behind a proxy that allows Dataverse and nothing else, and a picker that
    /// needs its own outbound host — or an API key — is a picker that does not open. The cost
    /// is a fixed catalogue, which the build script can refresh.
    /// </summary>
    public sealed class IconCatalog : IDisposable
    {
        private const string ResourceName = "Icons.Catalog.fluent-icons.zip";

        private readonly ZipArchive _archive;
        private readonly Dictionary<string, ZipArchiveEntry> _entries;
        private readonly object _readLock = new object();

        private IconCatalog(ZipArchive archive, List<IconEntry> icons, string about)
        {
            _archive = archive;
            _entries = archive.Entries.ToDictionary(e => e.FullName, StringComparer.Ordinal);
            Icons = icons;
            About = about;
        }

        /// <summary>Every icon, sorted by name then style. Regular precedes Filled.</summary>
        public IReadOnlyList<IconEntry> Icons { get; }

        /// <summary>Where the icons came from and under what licence. Shown in the About box.</summary>
        public string About { get; }

        public static IconCatalog Load()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream(ResourceName);
            if (stream == null)
            {
                throw new InvalidOperationException(
                    "The icon library is missing from this build. Expected an embedded resource named " +
                    ResourceName + "; run tools\build-catalog.ps1 and rebuild.");
            }

            // Into memory first. ZipArchive over a manifest resource stream works, but that
            // stream is read-only forward-seeking on some runtimes and every preview seeks.
            byte[] bytes;
            using (stream)
            using (var buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                bytes = buffer.ToArray();
            }

            var archive = new ZipArchive(new MemoryStream(bytes, writable: false), ZipArchiveMode.Read);

            var icons = new List<IconEntry>(archive.Entries.Count);
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) continue;

                var slash = entry.FullName.IndexOf('/');
                if (slash <= 0) continue;

                var folder = entry.FullName.Substring(0, slash);
                var name = entry.FullName.Substring(slash + 1, entry.FullName.Length - slash - 1 - 4);

                IconStyle style;
                if (folder.Equals("regular", StringComparison.OrdinalIgnoreCase)) style = IconStyle.Regular;
                else if (folder.Equals("filled", StringComparison.OrdinalIgnoreCase)) style = IconStyle.Filled;
                else continue;

                icons.Add(new IconEntry(name, style));
            }

            icons.Sort((a, b) =>
            {
                var byName = string.CompareOrdinal(a.Name, b.Name);
                return byName != 0 ? byName : a.Style.CompareTo(b.Style);
            });

            var about = ReadTextEntry(archive, "ABOUT.txt") ?? "Microsoft Fluent UI System Icons, MIT licensed.";
            return new IconCatalog(archive, icons, about);
        }

        /// <summary>The icon's SVG, exactly as Fluent ships it.</summary>
        public string ReadSvg(IconEntry icon)
        {
            if (icon == null) throw new ArgumentNullException(nameof(icon));

            ZipArchiveEntry entry;
            if (!_entries.TryGetValue(icon.EntryPath, out entry))
                throw new FileNotFoundException("No icon at " + icon.EntryPath + " in the bundled library.");

            // ZipArchive is not thread safe and previews are rendered from a worker while the
            // grid asks for more on the UI thread.
            lock (_readLock)
            {
                using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }

        private static string ReadTextEntry(ZipArchive archive, string name)
        {
            var entry = archive.GetEntry(name);
            if (entry == null) return null;
            using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                return reader.ReadToEnd();
        }

        public void Dispose()
        {
            _archive.Dispose();
        }
    }
}
