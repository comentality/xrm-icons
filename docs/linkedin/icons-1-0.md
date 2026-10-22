# LinkedIn post: Icons 1.0

Draft of 22 October 2026, the day 1.0 shipped. A LinkedIn post has no title field: the first
line is the title, and about the first 200 characters show before "see more". Three openers to
choose from, then the body. LinkedIn renders no markdown, so bare URLs and no bold. If this goes
out as a LinkedIn Article instead, the article title is the chosen opener.

## Opener (pick one)

1. Icons 1.0 is out -- an XrmToolBox tool that puts a decent icon on a Dataverse table. Type "account", get buildings. Pick one, and it writes the web resource, sets the table and publishes.

2. No icon set has ever drawn an icon called "account". That was the hard half of the tool I shipped today.

3. Every custom table I ever made spent its first month with the grey default icon. Shipped a tool today so that stops: Icons, in the XrmToolBox Tool Library.

## Body

Setting a table icon by hand -- find an SVG, check its licence, make a web resource with exactly the right name and type, paste the base64 in, find the table, type the name into the icon property, publish both. Every table, every time. Icons does all of it from one window inside XrmToolBox.

What is in it:

21,698 icons from six sets -- Fluent, Phosphor, Lucide, Tabler, Font Awesome Free and Solar. All bundled in the DLL, all MIT, ISC or CC BY 4.0. No API key, no account, no outbound host beyond Dataverse itself, so it works from behind a customer's proxy.

Search in Dynamics words. Icon sets name icons after what they depict, Dataverse names tables after what they mean. "account" finds buildings and briefcases, "contact" finds people, "case" finds headsets. Pick a table and it types the search for you.

Preview in an app's navigation, on light and on dark, at the size the app really draws it, before anything is written to the org.

One button stores the SVG, points the table at it and publishes both. Ctrl+Z puts the old icon back.

Every icon it stores is credited on an ATTRIBUTION.html beside it, so the licences travel with the drawings into the customer's org.

The search ate five weeks -- most of it reading result lists by hand. "email" used to return seven voicemail icons, "team" offered the Steam logo, and the Account table lost every usable icon the day I added Microsoft's own descriptions. The scoring and what went wrong on the way is written up here: https://comentality.com/blog/making-icons-findable

Install: open the XrmToolBox Tool Library and search for Icons. Source and releases: https://github.com/comentality/xrm-icons
More on the tool: https://comentality.com/tools/#icons

Next on the list -- a button that takes an icon off a table, and a three-way table filter. If a table of yours gets a wrong suggestion, tell me which one. I want to know.

#XrmToolBox #Dataverse #PowerPlatform #Dynamics365

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

Suggested caption for the first image: "account" finds buildings. No icon set has an icon by
that name.
