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
        private const string ResourceName = "Icons.Catalog.icons.zip";

        private readonly ZipArchive _archive;
        private readonly Dictionary<string, ZipArchiveEntry> _entries;
        private readonly object _readLock = new object();
        private readonly object _indexLock = new object();
        private Dictionary<string, IconEntry> _byDrawing;

        private IconCatalog(ZipArchive archive, List<IconEntry> icons, string about)
        {
            _archive = archive;
            _entries = archive.Entries.ToDictionary(e => e.FullName, StringComparer.Ordinal);
            Icons = icons;
            About = about;
        }

        /// <summary>Every icon, sorted by family, then name, then weight.</summary>
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
                    ResourceName + "; run tools\\build-catalog.ps1 and rebuild.");
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
                var icon = Read(entry.FullName);
                if (icon != null) icons.Add(icon);
            }

            icons.Sort((a, b) =>
            {
                var byFamily = a.Family.CompareTo(b.Family);
                if (byFamily != 0) return byFamily;

                var byName = string.CompareOrdinal(a.Name, b.Name);
                return byName != 0 ? byName : string.CompareOrdinal(a.Weight, b.Weight);
            });

            var about = ReadTextEntry(archive, "ABOUT.txt") ?? "Fluent, Phosphor, Lucide and Tabler icons.";
            return new IconCatalog(archive, icons, about);
        }

        /// <summary>
        /// The icon an archive path describes, or null when the path is not one — the ABOUT
        /// file, or a folder this build of the tool knows nothing about. Unknown is skipped
        /// rather than refused, so an archive built by a later script still opens.
        /// </summary>
        private static IconEntry Read(string path)
        {
            if (!path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) return null;

            var parts = path.Split('/');
            if (parts.Length != 3) return null;

            IconFamily family;
            if (!TryFamily(parts[0], out family)) return null;

            var weight = parts[1];
            if (weight.Length == 0) return null;

            var name = parts[2].Substring(0, parts[2].Length - 4);
            if (name.Length == 0) return null;

            return new IconEntry(family, name, weight, StyleOf(weight));
        }

        private static bool TryFamily(string folder, out IconFamily family)
        {
            foreach (IconFamily candidate in Enum.GetValues(typeof(IconFamily)))
            {
                if (string.Equals(IconEntry.FolderFor(candidate), folder, StringComparison.OrdinalIgnoreCase))
                {
                    family = candidate;
                    return true;
                }
            }

            family = IconFamily.Fluent;
            return false;
        }

        /// <summary>
        /// Which class of weight a family's word for it belongs to. Only the solid weights need
        /// naming: Fluent's filled, Phosphor's fill, Tabler's filled. Everything else — regular,
        /// bold, outline — is an outline, and a weight added later is treated as one until
        /// somebody says otherwise, which is the safer way round for a filter.
        /// </summary>
        private static IconStyle StyleOf(string weight)
        {
            return weight.StartsWith("fill", StringComparison.OrdinalIgnoreCase)
                ? IconStyle.Filled
                : IconStyle.Regular;
        }

        /// <summary>
        /// The icon in this bundle with that family, weight and name, or null when the bundle
        /// has no such icon. An icon an older version of this tool stored can name a drawing
        /// that a later bundle no longer carries, and that is not an error.
        /// </summary>
        public IconEntry Find(string family, string weight, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            return Icons.FirstOrDefault(i =>
                string.Equals(i.Family.ToString(), family, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(i.Weight, weight, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Every weight of one named icon in one family. Empty when there is no such name.</summary>
        public IReadOnlyList<IconEntry> FindByName(IconFamily family, string name)
        {
            if (string.IsNullOrEmpty(name)) return new IconEntry[0];

            return Icons
                .Where(i => i.Family == family && string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>Whether the drawings have been indexed yet, and so whether <see cref="Drawn"/> can answer.</summary>
        public bool Indexed
        {
            get { lock (_indexLock) return _byDrawing != null; }
        }

        /// <summary>
        /// The bundled icon that is this exact drawing, or null when no bundled icon is - and
        /// also null when two of them are, or when the index has not been built yet.
        ///
        /// Two entries sharing a fingerprint is one drawing shipped under two names rather than
        /// an error, but this cannot say which of the two somebody stored, and picking one would
        /// be a coin toss presented as an answer.
        /// </summary>
        public IconEntry Drawn(string fingerprint)
        {
            if (string.IsNullOrEmpty(fingerprint)) return null;

            lock (_indexLock)
            {
                IconEntry found;
                return _byDrawing != null && _byDrawing.TryGetValue(fingerprint, out found) ? found : null;
            }
        }

        /// <summary>
        /// Reads every icon in the bundle and fingerprints it. Around ten megabytes of SVG, so
        /// this belongs on a worker: the window is usable without it, and the tier of
        /// identification it serves simply says nothing until it lands.
        ///
        /// Safe to call twice, and safe to call while the grid is drawing thumbnails - the
        /// archive lock is taken one entry at a time rather than held across the whole pass.
        /// </summary>
        public void Index()
        {
            lock (_indexLock) if (_byDrawing != null) return;

            var drawings = new Dictionary<string, IconEntry>(StringComparer.Ordinal);
            var shared = new HashSet<string>(StringComparer.Ordinal);

            foreach (var icon in Icons)
            {
                string fingerprint;
                try
                {
                    fingerprint = IconFingerprint.Of(ReadSvg(icon));
                }
                catch (IOException)
                {
                    continue;
                }

                if (fingerprint == null) continue;

                if (drawings.ContainsKey(fingerprint)) shared.Add(fingerprint);
                else drawings[fingerprint] = icon;
            }

            foreach (var fingerprint in shared) drawings.Remove(fingerprint);

            lock (_indexLock) if (_byDrawing == null) _byDrawing = drawings;
        }

        /// <summary>The icon's SVG, exactly as the bundle holds it.</summary>
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
