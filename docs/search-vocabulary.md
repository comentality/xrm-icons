# Search vocabulary: the words an icon answers to

Handoff note, 14 September 2026, and the record of what was done with it. Everything below is
measured. The first request is built. The second is a family in, five to go, and the reason to
stop and think is written down under **What the first family cost**.

Two requests are open, and they are the same shape:

1. ~~**Font Awesome's alias names, as search words.**~~ **Built, 14 September 2026.** The build
   drops 683 alias files because they are the same drawing under an older name, which is right —
   but the names went with them, so `vcard` and `contact-card` found nothing and the drawing
   they name sat in the bundle as `address-card`. The names now travel without the files, in
   `vocabulary.v1.tsv` beside the icons: 579 of them against 479 icons. See **What was decided**
   below for what a hit on one is worth and why.
2. **The words the sets already ship.** **Fluent's are in, 14 September 2026** — 12,482 of them
   against 2,630 of its drawings, scored at 60 where a name scores 100. The other five are not,
   and the measurement below is why that is now a decision rather than a formality.

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
| **Fluent** | ~~—~~ `assets/<Icon Name>/metadata.json` in the repo | ~~**ships none**~~ `metaphor` + `description` | 2,654 | 2,630 of 2,971 | 12,482 |

**The Fluent row was wrong, and it was the row everything else hung off.** Corrected
14 September 2026, after asking rather than assuming. See **Fluent does publish one** below.

Word counts are after splitting phrases and multiplying across the weights each name is
bundled in. The build already reads Font Awesome's `icon-families.json` and Solar's
`icons.json`, so two of the five need no new download.

## What was decided

Put to the person whose tool it is, 14 September 2026. Three of the four are settled and the
alias half is built on them; the fourth is the one still open, and it is the one that decides
whether the tag half can go in at all.

**An alias is worth exactly what a name is worth — 100, not the synonym band's 15.** Because it
*is* a name: one Font Awesome published for that drawing and later stopped leading with, not a
hint about what the drawing looks like. Scoring it lower would have put the icon somebody asked
for by name below every icon with the word in it somewhere. The consequence, taken knowingly:
an alias ties with an icon really called that word, and the tie breaks on the family order, the
same rule that already settles two icons of one name. `contact card` still leads with Fluent's
`contact_card`; Font Awesome's `address-card` is in the grid beside it, which it was not before.

**Tags will not seed.** `SeedFor` keeps its two-rule test — a word some icon is literally called,
or a curated synonym in `Synonyms`. The seeding fixture exists because a looser rule gave
confident wrong answers in a box nobody filled in, and tags are looser than the rule that was
already caught being wrong. Aliases are not exempted from this either: `WorthSeeding` was not
touched, and nothing in the alias half changed what any of the 162 standard tables seeds.

**But the whole-name path is not gated by `WorthSeeding`, and the descriptions went through it.**
`SeedFor` asks first whether a table's *whole* name answers, and only falls back to single words
— under the two-rule test — when it does not. Eleven of the 162 could not answer whole before
Fluent's descriptions and can now, so eleven boxes nobody typed into say something different
from what they said. This is past what was decided above and it was not noticed until a test
caught it. It was kept rather than gated because every one of the eleven got *more* specific,
not less: Recurring Appointment led with a video call and now leads with a repeating calendar,
Invoice Line led with a straight line and now leads with a receipt, Purchase Order led with a
reorder arrow and now leads with a cart. `DescriptionTests` names all eleven with the icon each
one leads with. To close it instead, `Answers` would need a form that counts only name and alias
hits for the whole-name question, and that test-case block comes out.

**Tabler's tags go in first**, when the tag half is built: 66,254 words, half the whole
vocabulary, and so the worst case for the latency problem below. If search keeps up with Tabler
in it, nothing left can surprise us.

**Fluent's asymmetry was researched rather than guessed at, and it turned out not to be one.**
The question asked first was whether a keyword vocabulary for Fluent already exists that an MIT
DLL can carry. It does, and it is Microsoft's own. See below.

## Fluent does publish one

Measured 14 September 2026. This is the correction that matters most in this document, because
every option in the section below was an answer to a premise that is false.

Microsoft ships a per-icon vocabulary in `microsoft/fluentui-system-icons`, one
`assets/<Icon Name>/metadata.json` per icon family:

```json
{
  "name": "Shield",
  "size": [24, 20, 16, 28, 32, 48],
  "style": ["Regular", "Filled"],
  "keyword": "fluent-icon",
  "description": "Used to represent security, safety & protection.",
  "metaphor": ["security", "protection", "safety"]
}
```

- **2,654 files** across 2,989 asset folders, 88.8%. Against the 2,971 distinct names this
  bundle draws, 2,630 came away with words — about nine icons in ten.
- **2,604** carry a non-empty `metaphor`; **2,601** carry at least one word that is not already
  in the icon's own name. 12,926 term instances upstream, 1,458 distinct, about 5 an icon;
  **12,482** bundled, the difference being the words an icon's own name already had.
- **MIT**, under the repository's own `LICENSE`, Copyright (c) 2020 Microsoft Corporation. The
  same licence the drawings already ship under, so it adds no obligation the bundle does not
  already carry.
- `Cloud` → storage, upload, download, save, sync. `Key` → security, lock, password, access,
  keychain. `Person` → human, patient, contact, guest, avatar, people. These are the Dataverse
  words the curated `Synonyms` map exists to supply, coming from the set that drew the icon.

Four things to know before building on it.

**No package ships it.** Not `@fluentui/svg-icons` — which carries 21,682 SVGs and no JSON at
all, by two deliberate exclusions in its `package.json` — nor `@fluentui/react-icons`, whose
only JSON is font codepoints. Issue #788 on the repo, opened by a Microsoft member in December
2024 and still open, asks for exactly this to be packaged and says of Microsoft's own catalogue
that it "can only currently search by exact name". So `build-catalog.ps1` cannot fetch this the
way it fetches everything else. A sparse clone is the route:

```
git clone --depth 1 --filter=blob:none --sparse https://github.com/microsoft/fluentui-system-icons.git
git sparse-checkout set --no-cone '/assets/*/metadata.json'
```

That is a second kind of source in a script whose whole shape is "one npm package per family",
and it is the first thing to think about rather than the last.

**It is December 2024 vintage.** Two bulk commits added it — PR #618 in July 2023 and PR #778 in
December 2024 — and nothing since. The 335 folders without it skew to icons added after that:
`agents`, `autopilot`, `auto_fit`, the `battery_charge_N` family.

**The data is uneven.** Case is inconsistent (`Access Time` → `["number", "24", "Circle"]`),
arrays hold duplicates (`Lock Closed` names `access` twice), and a minority of terms describe
the shape rather than the meaning. Fold case and deduplicate.

**`keyword` is not a search word.** It is which Microsoft product the icon belongs to —
`fluent-icon` on 2,604, then `teams` 227, `outlook` 177, `excel` 81. Do not read it as one.

One measurement worth keeping, because it says what *not* to do. Joining the other five sets'
tag files onto Fluent's names by exact name reaches only about 20% of them, however many sets
are pooled — and the words that never join are the structural modifiers Fluent's names are full
of: `arrow`, `right`, `off`, `left`, `multiple`, `down`, `up`, `checkmark`, `dismiss`, `align`,
`ltr`, `rtl`, `panel`. Only about a third of Fluent's names have every token covered. So
borrowing another set's words for Fluent was never going to work; Microsoft's own file is not
merely the better option, it is the only one that covers the set.

Two sources were checked and must not be used. RemixIcon's own licence (v1.0, January 2026)
defines the licensed work to include "related assets" — which takes in its `tags.json` — and
then forbids use "to create, distribute, or sell a competing icon library or icon set", which
is arguably this. And the academic icon datasets are share-alike or non-commercial:
`rico_semantics` is CC BY-SA 4.0, IconQA and Icon645 are CC BY-NC-SA. Google's Material tag
data is a maybe rather than a yes — it is not in the Apache-2.0 repository at all, only behind
an undocumented `fonts.google.com/metadata/icons` endpoint that carries no licence statement of
its own, and the only thing tying the two together is that an Apache-2.0 script in that repo
names the endpoint. That is the shape of mistake this project has made once already, with
Solar's licence read off a third party's wrapper package.

The other thing the search turned up, worth recording but not decided: `search-alias.ts` in
`antfu-collective/icones` is 52 synonym groups over 159 terms, MIT, applied to the *query*
rather than to an icon — the same shape as our own `Synonyms` and generic where ours is
Dataverse's. 39 of its 52 groups bridge a word to one Fluent already uses. Nobody has built a
per-icon keyword set for Fluent outside Microsoft: Iconify's `@iconify-json/fluent` carries 490
aliases and no tags, and its search index is built from name chunks, which is why asking it for
`trash` in Fluent returns nothing.

## The four things to decide before writing code

**Fluent has no vocabulary, and Fluent wins every tie.** *(The first half of this is false — see
**Fluent does publish one** above. The rest of the paragraph is still the right worry, and the
answer to it is now that Fluent goes in first rather than that the others are held back.)*
`IconSearch.Rank` breaks a tie on the
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
  *trio*, *numeral three*. Fine, but it is where the 20,374 comes from. Take them from
  `metadata/icons.yml` rather than from `metadata/icon-families.json`, which is where the build
  reads the alias names today. The two carry the same terms, but `icon-families.json` also
  embeds every icon's raw SVG path data, which puts the file astride Font Awesome's own licence
  split: MIT covers "all non-font and non-icon files", CC BY 4.0 covers the icons as SVG.
  `icons.yml` holds no path data at all and is unambiguously the MIT half. This does not reach
  back to the aliases already bundled — a name is not a drawing, and Font Awesome's icons are
  credited on the attribution page regardless — but for a file read only for its words there is
  no reason to read the one with the drawings in it.
- `4k-linear` is both a real Solar icon and, in its other weights, an alias of `four-k`.
  Upstream is inconsistent about it; the build already handles it and bundles `4k`.

## Where it lives

Built as `vocabulary.v1.tsv` in the archive, beside `ABOUT.txt` and `drawings.v2.tsv`.
`IconCatalog.Read` returns null for any path that is not three parts ending `.svg`, so it is
skipped by an older build rather than breaking it, and `drawings.v2.tsv` is the precedent for
the versioned name. Keyed by `<family>/<name>` and not by entry path, so the three weights
Phosphor draws one icon in share one list rather than hold a copy each.

One line per icon, tab separated: a kind, the key, then the words.

    alias⇥fontawesome/address-card⇥contact-card⇥vcard

The kind column is there for the tag half — tags are worth a different score from aliases and
have to be told apart, and a line of a kind the reader does not know is skipped rather than
refused. So is a line naming an icon the bundle does not hold, which is the opposite of what
the drawing index does with the same situation and deliberately so: a stale fingerprint is a
confident wrong answer, a stale name is a word nothing ever matches. The build writes no such
line and a test says so, because an alias the bundle cannot show is dead weight that reads
like coverage.

The entry is named by `IconCatalog.VocabularyEntryName` and the build script asks the reader
for it rather than spelling it again, the way `index-catalog.ps1` asks for the index's name.

## Tests that will have an opinion

`SearchTests` and `VocabularyTests` both pin ranking behaviour, and `VocabularyTests` asserts
that every word `Synonyms` maps to is one the bundle draws — a rule worth extending to tags,
since an alias naming nothing is dead weight that reads like coverage. `IdentityTests` holds
the shared-drawing bound at 355 and is not affected: this changes names, not drawings.

## Suggested order

Aliases first — it is small, it is the thing that was actually lost, and it puts the plumbing
in. Then one family's tags, measured for latency before the other four. Fluent's asymmetry is
worth settling before the full set goes in, not after.

## What the first family cost

Words an icon went from 2.24 to 3.38 — half as many words again — and a keystroke cost 8% to
13% more. Both libraries were loaded into one process and timed alternately, round by round, so
that whatever else the machine was doing fell on both of them equally:

| Query | Without Fluent's words | With | |
|---|---|---|---|
| `a` — one letter, most of the library | 16.7 ms | 18.0 ms | +7.9% |
| `person` — a whole word | 9.4 ms | 10.5 ms | +11.2% |
| `arrow right` — two words, every entry scored twice | 9.9 ms | 11.1 ms | +11.6% |
| `security` — a word Fluent mostly does not name an icon after | 11.4 ms | 12.2 ms | +7.5% |
| `zzzznotanicon` — a miss, which cannot stop early | 15.7 ms | 17.8 ms | +13.5% |

**Measure this the way it is measured above, and not any other way.** The first attempt ran
`SearchSpeedTests` inside the full suite before and alone after, and reported the keystroke as
having *doubled* — 300 other tests were competing for the machine in one run and not the other.
The same test run alone twice reported Fluent's words as having made search *faster*. Neither
number meant anything. One process, both libraries, alternating rounds, is the only shape of
this measurement that has answered the same way twice.

What the honest number says is that half as many words again costs a tenth of the time, because
the per-word comparison is not where the time goes — the loop over 21,698 entries is, along with
the allocation `Squashed` does on nearly every one of them and the sort at the end. The words
are close to free; the walk is not.

That is encouraging for the other five families and it is not a promise about them. They hold
about 129,000 words between them, six or so an icon against Fluent's 1.14, so on the same
marginal cost the walk stays the dominant term and a keystroke lands somewhere in the teens.
Somewhere in the teens is fine. But it is an extrapolation from one family, and Tabler is five
times Fluent on its own, so it gets measured rather than assumed — which is why `SearchSpeedTests`
is in the repository rather than in a session's scrollback.

If it does turn out to cost, none of these has been tried yet:

- `ScoreTerm` allocates on nearly every icon for every term — `Squashed` runs two `Replace`
  calls over the name whenever nothing has matched yet, which is the common case. Cache the
  squashed name on the entry, or drop the fallback to a cheaper test.
- Nothing is indexed. Every query walks all 21,698 entries and every word of each. A map from
  word to entries, built once at load, turns the common case from a scan into a lookup, and the
  vocabulary is exactly what makes that worth building.
- The words could be filtered harder at bundle time. Fluent's already drop anything the icon's
  own name says; Tabler's include phrases like `"visual studio"` that no Dataverse customiser
  will type.

## What is left

The plumbing is in: a sidecar in the archive carrying two kinds of word, a reader that skips
what it cannot place, words kept out of `IconEntry.Words`, `IconWords` holding both kinds so a
third is a field rather than a parameter, and `AliasTests`, `DescriptionTests` and
`SearchSpeedTests` holding it.

1. Tabler, being half the remaining vocabulary and so the worst case, measured against the real
   instrument — one process, both libraries, alternating rounds.
2. The other four, if that measurement allows it.
3. Only then, and only if it does not, the cheaper inner loop above.

Two things to carry into that work. Scoring is the whole of it — an alias earned 100 because it
is a name, a description earned 60 because it is true and is not the name, and a fourth kind
needs the same argument made for it. And `ScoreTerm` now runs a ladder over three arrays in
bands, so a fourth is one more call rather than another copy of the ladder.
