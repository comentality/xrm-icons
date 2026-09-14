# Search vocabulary: the words an icon answers to

Handoff note, 14 September 2026. Nothing below is built. Everything below is measured.

Two requests are open, and they are the same shape:

1. **Font Awesome's alias names, as search words.** The build drops 683 alias files because
   they are the same drawing under an older name, which is right — but the names went with
   them. `vcard` and `contact-card` find nothing today, and the drawing they name is in the
   bundle as `address-card`.
2. **The words the other five sets already ship.** Four of the six publish per-icon tags, and
   the build throws all of them away. This is the question behind "did we drop synonyms for
   some other sets?" — and the answer is that we never picked them up.

## What is already there, and is not this

`IconSearch.Synonyms` is a curated map of about 120 entries, and it goes the other way. It
takes a word a Dynamics customiser types — `employee`, `milestone`, `entitlement` — and names
the words the *library* draws it under. It was built by measuring 162 standard tables and
finding a third of them seeded nothing at all. It is deliberately one-way, deliberately short,
and a synonym hit scores 15 against an exact name's 100 so it can never outrank the icon
actually called that.

Upstream tags are the opposite direction: extra words attached to one icon by the people who
drew it. `address-book-tabs` answering to *roledex*. The two do not overlap and neither
replaces the other.

## What each set ships

Verified against the pinned versions on 14 September 2026. All five paths exist and parse.

| Family | Where | Shape | Names | Bundled names it covers | Words |
|---|---|---|---|---|---|
| Font Awesome | `metadata/icon-families.json` | `search.terms` + `aliases.names` | 1,992 | 1,422 of 1,422 | 20,374 |
| Tabler | `icons.json` | `tags` + `category` | 5,130 | 5,130 of 5,130 | 66,254 |
| Phosphor | `dist/index.mjs` | `tags:` in a JS array literal | 1,512 | 1,512 of 1,512 | 25,512 |
| Lucide | `tags.json` | `{name: [tags]}` | 1,834 | 1,834 of 1,834 | 15,656 |
| Solar | `icons.json` aliases, `metadata.json` categories | alias→parent, category→names | 1,268 | 1,267 of 1,288 | 1,603 |
| **Fluent** | — | **ships none** | — | — | **0** |

Word counts are after splitting phrases and multiplying across the weights each name is
bundled in. The build already reads Font Awesome's `icon-families.json` and Solar's
`icons.json`, so two of the five need no new download.

## The four things to decide before writing code

**Fluent has no vocabulary, and Fluent wins every tie.** `IconSearch.Rank` breaks a tie on the
family enum ordinal, and Fluent is first — deliberately, so that Fluent leads each band of
equal relevance rather than losing its own grid to whichever family writes shortest names.
Give the other five families 129,399 extra words and Fluent stops leading, because the others
stop tying: they start *scoring* on queries Fluent cannot reach. This is the single biggest
consequence and it is a judgement call, not a bug. Options are to score a tag hit low enough
that it never displaces a name hit (the Synonyms precedent, 15 against 100), to write Fluent a
vocabulary of its own, or to accept the shift.

**Extra words must not go into `IconEntry.Words`.** That array feeds two other things:
`DisplayName` is built by joining it, so tags would end up in the grid label; and `Score`
penalises an icon for words the query did not account for —
`unmatched = icon.Words.Length - terms.Count`, up to −20. Appending tags would make every
tagged icon rank *worse* on its own name. A separate property, read by `ScoreTerm` only.

**Search runs on every keystroke, over the whole library.** Today: 21,698 icons, 2.18 words
each, 7 ms for a normal query and 12 ms for a single letter. The vocabulary is 5.96 more words
per icon — 3.7× the inner loop, so 26–45 ms per keystroke on the same machine. That is the
difference between a grid that keeps up with typing and one that does not. Store the words per
*name* rather than per entry if that helps: Phosphor draws 1,512 names in three weights and
would otherwise hold each tag three times.

**Tags should probably not seed.** `SeedFor` puts a word in the search box for a table nobody
typed into, and `WorthSeeding` accepts only two things: a word some icon is literally called,
or a curated synonym. The fixture exists because the looser rule gave confident wrong answers —
Payment Term found `terminal`, Time Entry found a brand called Sentry. Tags are looser still.
Ask before letting them in.

## Shape of the data, once you look at it

- **Tabler** tags contain phrases, not words: `"adding up"`, `"visual studio"`. Split them.
- **Phosphor** marks new icons with a literal `"*new*"` tag. Drop anything starting with `*`.
- **Solar** is the thin one and the messy one. Its 203 aliases collapse to 56 distinct pairs,
  and some are typo corrections where the *alias* is the misspelling — `bell-bing` for
  `bell-ring`, `card-recive` for `card-receive`. Harmless as search words, useless as them.
  The genuine ones are worth having: `accumulator` for `car-battery`. Its categories arrive
  once per weight, so deduplicate, and they are broad enough (`arrows`, `messages`) to be
  close to noise.
- **Font Awesome** `search.terms` include the spelled-out digits — `3` answers to *triad*,
  *trio*, *numeral three*. Fine, but it is where the 20,374 comes from.
- `4k-linear` is both a real Solar icon and, in its other weights, an alias of `four-k`.
  Upstream is inconsistent about it; the build already handles it and bundles `4k`.

## Where it would live

The archive is `<family>/<weight>/<name>.svg` plus `ABOUT.txt` plus `drawings.v2.tsv`.
`IconCatalog.Read` returns null for any path that is not three parts ending `.svg`, so a new
sidecar is skipped by an older build rather than breaking it — `drawings.v2.tsv` is the
precedent for the versioned name. Keyed by `<family>/<name>`, not by entry path, for the
reason above.

## Tests that will have an opinion

`SearchTests` and `VocabularyTests` both pin ranking behaviour, and `VocabularyTests` asserts
that every word `Synonyms` maps to is one the bundle draws — a rule worth extending to tags,
since an alias naming nothing is dead weight that reads like coverage. `IdentityTests` holds
the shared-drawing bound at 355 and is not affected: this changes names, not drawings.

## Suggested order

Aliases first — it is small, it is the thing that was actually lost, and it puts the plumbing
in. Then one family's tags, measured for latency before the other four. Fluent's asymmetry is
worth settling before the full set goes in, not after.
