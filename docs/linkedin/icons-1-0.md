# LinkedIn post: Icons 1.0

Draft of 22 October 2026, the day 1.0 shipped. A LinkedIn post has no title field: the first
line is the title, and about the first 200 characters show before "see more". Three openers to
choose from, then the body. LinkedIn renders no markdown, so bare URLs and no bold. If this goes
out as a LinkedIn Article instead, the article title is the chosen opener.

## Opener (pick one)

Icons 1.0 -- Just released new XrmToolBox tool to quickly set table icons, working offline, with realistic previews, and great search.

## Body

I don't think anyone is setting icons by hand no more, but it is still a tedious process. Find Fluent-compatible outline icon set, figure out the metaphor, get blocked by license in the end.

Our Icons tool does much better:

* 21,698 icons from six sets -- Fluent, Phosphor, Lucide, Tabler, Font Awesome Free and Solar. All bundled with the tool.
* MIT, ISC or CC BY 4.0. No API key, no outbound host beyond Dataverse itself, so it works from behind a customer's proxy.
* Search in Dynamics words. Icon sets name icons after what they depict, Dataverse names tables after what they mean. "account" finds buildings and briefcases, "contact" finds people, "appointment" finds calendars, "case" finds headsets. Pick a table and it types the search for you.
* To have some fun in the work routine -- realistic preview that looks like a tear away from Model Driven App.

## Images (in this folder)

Post them in this order; LinkedIn shows the first one largest.

1. `01-library-account.png` -- the library searched for "account", Person account chosen, the
   stand-in navigation on the right. Built from `docs/images/overview.png` with the title bar and
   the status line cropped off: that screenshot is from 10 September and its status line says
   18,637 icons, before Solar and Font Awesome were added. A fresh full-window shot of 1.0 would be
   better; it needs the tool open on a desktop.
2. `02-search-in-dynamics-words.png` -- "invoice" and "keychain", two slices from the release
   notes at 2x.
3. `03-preview-light-dark-in-app.png` -- the light and dark preview beside the icon drawn into an
   app's navigation, at 3x.
4. `04-one-button-apply.png` -- the confirm dialog listing what an apply will do, at 2x.

Suggested caption for the first image: "account" finds buildings. None of the 21,698 is called
that.
