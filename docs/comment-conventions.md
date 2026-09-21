# Comment conventions

House style for comments in this repo, derived from the DivineBeastTitle.cs pass. Applies to `.cs`, `.py` and `.s` alike.

## The rules

**Comments describe CURRENT behaviour.** Not how the code got there. Git holds the history.

**Lessons worth keeping go in a tracked feature doc** — `docs/<feature>.md`, beside `character-clone-footprints.md` and
friends. Findings behind a decision that are not obvious from the code belong there, not inline.

**Why-reasoning is kept to the minimum that justifies the code as written.** A sentence, not a paragraph. If it needs a
paragraph, the paragraph belongs in the feature doc and the comment points at the mechanism.

**Explanation lives with the FUNCTION, not the constant.** A constant gets a one-line description of its value:

```csharp
private const float CapeWindLift = 2.6f;         // how far the hem flies off the back
```

The mechanism the constants feed is documented on the function that implements it. Before this pass the cape's wind,
spring and colour essays all sat above constants while `BreezeCape`, `StiffenCape` and `TintCape` had no summary at all.

**Related definitions live together.** Authored data in one region, runtime state below it. A value that is overwritten
at runtime is not a definition and should not read like one.

## Remove

- **Dated attributions** — `(user 2026-09-12)`, `(2026-09-15)`, `(log 2026-09-15 14:35)`.
- **Incident narratives** — "it was a force for a long time", "the first attempt was caught by", "a longer linger was
  tried and dropped", build timings that are no longer true.
- **History by implication** — `no longer`, `used to`, `previously`, `formerly`, `became`, `since the wings`, and `now`
  where it means "it changed". "Those three glows are no longer textures of their own" only parses if you know what
  they used to be.
- **References to untracked paths.** `game_data/` is gitignored in its entirety, so a citation there points at a file
  nobody else has. Tool code may name a `game_data/` path as *data* (an input or output); comments must not cite it as
  a source.

## Keep

- **Measured constants, addresses, offsets, frame numbers, struct layouts.** Current facts, not history.
- **Cross-references to other code in this repo** — `MirageSceneGateFlag`, `Draw__10CCharacter`,
  `CharacterClone.CopyCloth`, `MotionProc2`. These are the hardest thing to rediscover by reading and they describe how
  the system is wired now.
- **Rules and warnings, in the present tense.** Turn "the caves used to live in 0x228BB0, which turned out to be live
  code" into "⚠ 0x228BB0–0x22A210 is NOT free: it is the live dungeon character-change screen, however dead it looks."
- **Live contrasts between options that both exist.** "It goes into the REST SHAPE, not the velocity array" is a real
  choice being explained. Only contrasts with a *former version of this code* are history.

## Verifying a comment pass

**Prove no statement changed.** Strip everything from `//` (or `#`) to end-of-line on both `git show HEAD:<file>` and
the working copy, drop blank lines, compare the lists. Identical means comment-only. A green build does not show this,
and a 300-line diff cannot be eyeballed for it.

**For `.s` stubs, prove no byte changed.** Re-run `tools/stubs/build_ee_stubs.py`: every stub must report `ok`, never
`WROTE`, and no `.bin` may appear in `git status`.

**Check for stacked summaries** after every batch: a `</summary>` followed by another `/// <summary>` before any
declaration means one doc block is dead. Anchoring an insertion on a declaration line without reading what sits above
it produces these silently.

**Check for orphaned punctuation** after any mechanical strip: a comment whose text now begins with `. , ; : )` had its
opening clause removed.

## Traps

- **Anchor on whole wrapped sentences, never one line of a flowing paragraph.** Single-line anchors caused four
  defects in one pass: orphaned punctuation, a duplicated clause, a sentence broken mid-phrase, and a mis-scoped
  replacement.
- **A stripped date can hide a removed symbol.** The doc block above `ApplyFlightTime` documented `ApplyTargetKind`,
  deleted long before. Cross-check that every `<see cref>` and every documented member still exists.
- **Scan hits are ~5% real.** Known false positives: a line starting with a file extension (`.mot`, `.bin`), a
  continuation naming a symbol (`.CapeTint`), `STOPPED` as a current action, and `no longer −1` describing a runtime
  state transition. Triage by eye; never fix the list.
- **Word counts are not the goal.** Placement is. Moving essays onto functions and adding missing summaries can raise
  the comment count while making the file far more readable.
