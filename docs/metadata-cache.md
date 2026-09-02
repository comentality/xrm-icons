# Why this tool asks the org rather than reading XrmToolBox's metadata cache

XrmToolBox keeps a metadata cache per connection — `ConnectionDetail.MetadataCache`, an
`EntityMetadata[]` kept current against a `ClientVersionStamp`, persisted as a gzipped JSON file
per connection id. It is the obvious thing for a tool that reads every table on every load to
use instead of `RetrieveAllEntities`, and this tool does not use it. Here is the measurement
that decided it, so nobody has to decide it twice.

Run it again with `.\tests\e2e.ps1 -BenchCache`.

## What it costs, measured

Against a real environment of 880 tables, 1 September 2026:

| What was asked | How long | Count |
| --- | --- | --- |
| `RetrieveAllEntities`, entity filter — what a load costs today | 1717 ms, then **497 ms, 468 ms** | 880 |
| ...on a client that has not spoken to the org yet | **568 ms** | 880 |
| `MetadataCache`, held in memory, org unchanged (stamp check) | **61 ms, 31 ms, 31 ms** | 880 |
| ...turning that into the tool's table list | 1 ms, 0 ms, 0 ms | 646 |
| `MetadataCache`, held in memory, **org changed** | **13 625 ms** | 881 |
| `MetadataCache` from its file — XrmToolBox starting up | 10 887 ms, 12 665 ms | 646 |
| `MetadataCache`, cold, first ever load | 13 640 ms | 880 |

The cache is a full metadata cache: every attribute of every table, 8 MB gzipped and about
130 MB of JSON on this environment. This tool wants nine fields per table.

## What that means

The cache wins in exactly one state — already loaded in memory, and nothing changed in the org
since it was loaded — where it answers in about 40 ms against 470 ms for asking the org. That is
a third of a second saved.

In every other state it is twenty to thirty times **worse**, because a version stamp that has
moved means a full download rather than an incremental one. The worst case is the case that
matters most:

> You added a table, and went straight to the tool to give it an icon.

That is the moved-stamp case. Reading the cache costs **13.6 seconds**; asking the org costs
**468 ms**. Both see the new table — freshness was never the problem, and a table created and
published a minute earlier showed up in both, as did an icon written a minute earlier.

## So

The tool asks the org. It is half a second, it is the same half a second whether or not anybody
has touched the environment today, and it does not depend on XrmToolBox having loaded something
first. Half a second also turned out to be cheap enough to spend without being asked, so a
connection now loads the environment by itself and **Load tables** is a refresh — but that is a
conclusion about the direct call, not about the cache.

Two smaller findings from the same run, worth knowing if you go near this:

- Outside XrmToolBox the cache folder is not the one under the roaming profile — the library
  resolves it relative to the running executable. The bench watches both.
- The version stamp is kept in an NTFS alternate data stream beside the cache file, and reading
  it fails on this machine — in the bench and in XrmToolBox's own log. When it fails, checking
  whether the cache is still valid means reading the whole file, which is most of why the
  startup load costs eleven seconds rather than one.
