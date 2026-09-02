# Measuring the real UI

The in-context preview is only worth building if its numbers are real. A site map entry drawn
four pixels off is not a preview, it is a confident lie about what the user is about to ship —
the same failure the seeding TODO is about. So the sizes come from a running model-driven app,
measured, with the page and the date they came from written down beside them.

Nothing here guesses at *which* places a table icon appears either. The icon is an SVG web
resource, so every element rendering it carries the web resource name in its URL. `probe.js`
finds them. Whatever it lists is the list — no more, no fewer.

## Before you start

- Pick a table you have already given an icon to with this tool. Standard tables draw
  Microsoft's own icons from an internal set, not from web resources, so they will not answer
  the probe. Note the web resource name from the tool's Web resource box, e.g. `new_/icons/account.svg`.
- Browser zoom at **100%** (Ctrl+0). The probe prints `devicePixelRatio` so we can tell later
  whether it was.
- Light theme first. If the org has dark mode, do a second pass in it — the preview draws both.

## Running it

1. F12, Console tab.
2. Paste all of `probe.js`, Enter. (Edge/Chrome may ask you to type `allow pasting` first.)
3. Run it with the distinctive tail of your web resource name:

   ```js
   iconProbe('icons/account.svg')
   ```

   It prints a table, and copies the full JSON to the clipboard.
4. Paste the clipboard into a file here named for the page: `sitemap.json`, `grid.json`,
   `form.json`, and so on.
5. Take one **uncropped** screenshot of the page at 100% and drop it in beside the JSON.
   The numbers say how big; the screenshot says what it sits in.

If the probe finds nothing on a page, that is a real answer too — write it in `notes.md`. It
means the icon is not shown there, and the mockup should not pretend it is.

## Pages worth walking

Run the probe on each. The ones it answers on are the contexts the mockup will have.

- The app's landing page or default dashboard
- A view / grid page — and open the view selector while the probe is loaded
- The left navigation, expanded; then collapsed to the rail
- The navigation's Recent and Pinned flyouts, opened
- A record form — header, and the related-records tabs
- A lookup field, dropdown open on some results
- A quick create panel
- Global search results
- Advanced find results
- A timeline / activity feed on a record
- The app switcher or app list

## What comes back

Per element the probe reports the icon's rendered box, the height of the row it sits in, how
far in from the row's left edge it starts, the gap to the label, and the label's font, size,
weight and colour — plus the first opaque background colour behind it. That is everything the
mockup needs to draw the chrome, not just the icon.
