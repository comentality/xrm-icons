# What has been measured, and what has not

## 9 September 2026 - the site map, light

Measured with `navChrome()` from `probe.js`, in **Power Pages Management** on the **Web Pages**
list, in a demo environment. `devicePixelRatio` 1, browser zoom 100%. Twenty-six navigation
rows and three group headers, all agreeing: every distinct value below came back as the only
value across all of them.

| What | Measured | Was guessed |
| --- | --- | --- |
| Nav width, expanded | 200 (incl. its 1px right border) | 224 |
| Row height | 35 | 40 |
| Icon box | 16 | 20 |
| Left padding to the icon | 14.5 | 16 |
| Gap, icon to label | 14 (label at x 44.5) | 12 |
| Label | Segoe UI 14px, weight 400, #242424 | 14px - the one size that held |
| Group header | Segoe UI 14px, weight **600**, #242424, 43px block | 30px block, 12px |
| Rail width | 44 | 48 |
| Rail icon box | 16, still at x 14.5 - it does not re-centre | 20 |
| Nav background | #F0F0F0 | #FAF9F8 |
| Selected row | #FFFFFF - **lighter** than the pane | #EDEBE9, darker |
| Selection bar | #0F6CBD, 4 wide, 21 tall, 4 in from the left | #0F6CBD, 3 wide |
| Page beside the nav | #FFFFFF | #FFFFFF |
| Hairline at the nav's edge | #D1D1D1 | #E1DFDD |
| Icon colour (`currentColor`) | **#424242** - not the label's #242424 | #242424 |

Three of these are not roundings, they are the mock having been wrong:

- **Selection is lighter, not darker.** The selected row is painted the page's own white on a
  grey pane. Collapsed to the rail there is no fill at all - only the accent bar.
- **The selected label is not bolded.** The bar and the fill carry the selection; the text
  stays at weight 400. The mock was drawing it bold.
- **A group header is a row-sized label at weight 600**, not a smaller one in a lighter grey.
  There is no second grey in the navigation.

Also worth knowing, from the same pass: the app **inlines the SVG web resource into the page**,
`<style>` block and all, and renders a `24 24` viewBox at 16 CSS pixels. That is why the icon
takes its colour from `currentColor` and why the style block matters.

**The app does not overrule a colour the file states.** The nav puts its own class on the
inlined `<svg>`, and the Power Pages icons - which declare no fill of their own - come out
#424242 because of it. That could have meant the app painting over anything, which would make
the tool's Fixed mode a promise it cannot keep. It does not: a clone of the same icon with
`fill="#CC0000"` on the root computed to `rgb(204, 0, 0)` on both the `<svg>` and its `<path>`,
inside the nav, with the app's classes still on it. So Theme mode lands on #424242 and Fixed
mode lands on the colour that was picked, which is what the preview draws.

## What has not been measured

- **Dark.** There is no dark theme in that environment to measure. Settings > Personal
  Settings > Set Personal Options, General, offers "Support high contrast settings" and no
  theme or appearance option at all, so there was no dark app to point the probe at. Every
  colour in `NavTheme.Dark` is still marked GUESS, and stays that way: a dark palette derived
  from a light one would be a guess wearing a measurement's clothes.
- **Everything except the navigation.** The grid, the form header, the lookup, the app
  switcher: `iconProbe` will find them, but the preview does not draw them, so they were not
  walked. If the preview ever grows past the navigation, walk them then.
- **A table iconed by this tool.** The demo org had none, so the pass used the Power Pages
  tables, whose icons are SVG web resources drawn in exactly the same slot. The chrome is the
  chrome whoever wrote the icon.
