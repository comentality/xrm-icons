# The clean-room rule

This tool reimplements the *functionality* of [IconEngine](https://www.xrmtoolbox.com/plugins/PLIO.IconEngine.XrmToolBox/)
(`PLIO.IconEngine.XrmToolBox`, by Rolf Stifter), which is closed source. Functionality is not
copyrightable; a particular implementation is. The rule below is what keeps this project on the
right side of that line, and it applies to anyone working on it, human or otherwise.

## Everything here must derive from

- IconEngine's public description — the [author's write-up](https://rolfstifter.be/xrmtoolbox/iconengine/)
  and its [store page](https://www.xrmtoolbox.com/plugins/PLIO.IconEngine.XrmToolBox/)
- the per-version **release notes the author wrote on NuGet**, at
  [nuget.org/packages/PLIO.IconEngine.XrmToolBox](https://www.nuget.org/packages/PLIO.IconEngine.XrmToolBox).
  The store page shows only the latest of these, so the version history there is richer than
  anything else published. The listing page only: unpacking the package, or browsing its
  contents, is the off-limits list below.
- the authors' own **public launch post**, at
  [linkedin.com/feed/update/urn:li:activity:7087689430026592257](https://www.linkedin.com/feed/update/urn:li:activity:7087689430026592257/),
  including the demo video in it. A demo the authors published is a description of the tool, in
  the same class as the store page.
- Microsoft's public Dataverse documentation
- our own design decisions

## Off limits

- Decompiling or disassembling the shipped `PLIO.IconEngine.XrmToolBox.dll`, by any tool.
- Downloading the NuGet package in order to inspect what is in it.
- **The binary-derived analysis of IconEngine in the sibling repo `xrm-librarian`** —
  specifically `reports/blast/PLIO.IconEngine.XrmToolBox.md` and the per-assembly rows for that
  package in `data/blast-radius.json` and `data/capabilities.json`. Those were produced by
  walking the assembly's call graph. They are exactly the kind of knowledge a clean-room
  implementation must not have, and they are the easiest thing to stumble into, because that
  repo is a sibling of this one and grep does not know about clean rooms.

Reading any of the above to decide how this tool should work defeats the point of having written
it. Reading it out of curiosity, having already shipped, does too — you cannot unread it.

## Where this implementation knowingly differs

Worth recording, because "we made different choices" is the substance of the claim:

- **The icon source.** IconEngine draws on Flaticon, which needs an API key and carries
  attribution terms. This tool bundles four sets inside the assembly — Microsoft's Fluent UI
  System Icons and Phosphor and Tabler under MIT, Lucide under ISC — so there is no key, no
  network, and no attribution obligation on what you store in a customer's org. The licences
  ask for their notice to travel with the software, which is what `ABOUT.txt` in the bundle is.
  A set that asked for more than that is a set this tool does not bundle: Microsoft's own
  codicons are CC-BY-4.0, and were measured and left out for that among other reasons (TODO.md).
- **Where a credit line would live.** Since the attribution page, that last sentence is about
  what is bundled rather than about what could be. Every icon written into an org carries its own
  provenance and the apply rebuilds `ATTRIBUTION.html` beside the icons from it, so a set whose
  whole demand is a notice now has somewhere to put one. Nothing shipping today asks for that —
  `IconLicence.RequiresNotice` is false for all four families and a test says so — but the day a
  CC BY set is bundled, the obligation lands on that page rather than on the customer, and the
  page is the price of the icon: it must not be applied without one.
- **Colour.** The default is `fill="currentColor"` so the app themes the icon, following
  Microsoft's own guidance for table icons, rather than baking a colour in.
- **Rendering.** No SVG library: the path grammar is parsed and rasterised in
  `Icons/Logic/SvgPath.cs`, and filled and stroked in `Icons/Logic/SvgImage.cs`, so the tool
  ships as one DLL.
