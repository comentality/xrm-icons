using System;
using System.Collections.Generic;

namespace Icons.Catalog
{
    /// <summary>
    /// Who drew a family, under what licence, and what this tool did to the drawings before
    /// they got here.
    ///
    /// These facts are the ones the attribution page is written from, and they are also the
    /// ones <c>ABOUT.txt</c> in the bundle states - the notice that travels with the software.
    /// Two copies of the same fact is one too many, so <c>LicenceTests</c> reads ABOUT.txt out
    /// of the archive and checks this table against it. Change one and the other has to follow.
    /// </summary>
    public sealed class IconLicence
    {
        /// <summary>
        /// Internal rather than private so that a test can build a licence this bundle does not
        /// contain. Everything here asks only that the notice travel with the software; the code
        /// that handles a licence demanding a credit line has nothing real to run against until
        /// such a set is bundled, and untested is how that code would stay.
        /// </summary>
        internal IconLicence(
            IconFamily family, string setName, string holder, string licence,
            string licenceUrl, string sourceUrl, string bundleChange, bool requiresNotice)
        {
            Family = family;
            SetName = setName;
            Holder = holder;
            Licence = licence;
            LicenceUrl = licenceUrl;
            SourceUrl = sourceUrl;
            BundleChange = bundleChange;
            RequiresNotice = requiresNotice;
        }

        public IconFamily Family { get; }

        /// <summary>The set's own name for itself, as its authors write it.</summary>
        public string SetName { get; }

        /// <summary>Whose copyright the notice names.</summary>
        public string Holder { get; }

        /// <summary>The licence, by its SPDX identifier.</summary>
        public string Licence { get; }

        /// <summary>
        /// Where the licence text is. SPDX rather than the set's own repository: the repository
        /// moves its LICENSE file around and SPDX does not, and a dead link in an attribution
        /// page is worse than no link. Whose copyright it is, is what <see cref="Holder"/> says.
        /// </summary>
        public string LicenceUrl { get; }

        /// <summary>Where the icons come from, for a reader who wants the original.</summary>
        public string SourceUrl { get; }

        /// <summary>
        /// What <c>tools\build-catalog.ps1</c> changed inside the files themselves, or null if
        /// it changed nothing. Choosing which of an icon's optical sizes to keep, and renaming
        /// a file, are not changes to a drawing; removing an attribute from it is.
        /// </summary>
        public string BundleChange { get; }

        /// <summary>
        /// Whether this licence's demand is a credit line - CC BY and its relatives - rather
        /// than only that the notice travel with the software. False for the four sets under
        /// MIT and ISC, whose notice is <c>ABOUT.txt</c> and for whom the attribution page is
        /// a courtesy. True for Font Awesome, where the page is the price of the icon rather
        /// than a kindness. When it is true, an icon of that family must not be applied unless the
        /// page goes with it.
        /// </summary>
        public bool RequiresNotice { get; }

        private static readonly Dictionary<IconFamily, IconLicence> Known =
            new Dictionary<IconFamily, IconLicence>
        {
            {
                IconFamily.Fluent,
                new IconLicence(
                    IconFamily.Fluent,
                    "Microsoft Fluent UI System Icons",
                    "Microsoft Corporation",
                    "MIT",
                    "https://spdx.org/licenses/MIT.html",
                    "https://github.com/microsoft/fluentui-system-icons",
                    null,
                    false)
            },
            {
                IconFamily.Phosphor,
                new IconLicence(
                    IconFamily.Phosphor,
                    "Phosphor Icons",
                    "Phosphor Icons",
                    "MIT",
                    "https://spdx.org/licenses/MIT.html",
                    "https://github.com/phosphor-icons/core",
                    null,
                    false)
            },
            {
                IconFamily.Lucide,
                new IconLicence(
                    IconFamily.Lucide,
                    "Lucide",
                    "Lucide Icons and Contributors, and Cole Bemis",
                    "ISC",
                    "https://spdx.org/licenses/ISC.html",
                    "https://lucide.dev",
                    "the class attribute was removed from every element",
                    false)
            },
            {
                IconFamily.Tabler,
                new IconLicence(
                    IconFamily.Tabler,
                    "Tabler Icons",
                    "Paweł Kuna",
                    "MIT",
                    "https://spdx.org/licenses/MIT.html",
                    "https://tabler.io/icons",
                    "the class attribute and the leading full-canvas path were removed",
                    false)
            },
            {
                IconFamily.FontAwesome,
                new IconLicence(
                    IconFamily.FontAwesome,
                    "Font Awesome Free",
                    "Fonticons, Inc.",
                    "CC-BY-4.0",
                    "https://spdx.org/licenses/CC-BY-4.0.html",
                    "https://fontawesome.com",
                    null,

                    // The one set bundled here that asks for more than that its notice travel
                    // with the software. The package it comes from holds icons, fonts and code
                    // under three licences; only the drawings are taken, and CC BY 4.0 is what
                    // the drawings are under. It asks for a credit line and for changes to be
                    // indicated, which is what the attribution page carries - so an icon of
                    // this family is not put on a table unless that page goes with it.
                    true)
            },
        };

        /// <summary>What is known about a family.</summary>
        public static IconLicence For(IconFamily family)
        {
            IconLicence licence;
            if (!Known.TryGetValue(family, out licence))
                throw new ArgumentOutOfRangeException(nameof(family), "No licence is recorded for " + family + ".");

            return licence;
        }

        /// <summary>Every family, in the order the enum declares them.</summary>
        public static IEnumerable<IconLicence> All
        {
            get { return Known.Values; }
        }
    }
}
