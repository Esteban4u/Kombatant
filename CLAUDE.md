# CLAUDE.md

This file provides guidance to Claude Code when working in this repository.

## What this is

A **RebornBuddy botbase** (`Kombatant`) for Final Fantasy XIV — a combat-assist plugin that
runs inside RebornBuddy's bot framework (`ff14bot`), handling targeting, combat, movement,
loot rolling, duty automation, and mechanic avoidance. Not a standalone application; it only
runs loaded inside RebornBuddy.

This repo is a personal fork of the open-source Kombatant project (originally by Freiheit,
then modified by Akira0245 — see `README.md`'s Credits section, which documents upstream
authorship only, not this fork's own changes). Steve's own active development lives entirely
in git history — most heavily a from-scratch rewrite of the loot-rolling system (see
"Loot system" below), authored across ~20 commits from 2026-04-25 through 2026-07-14.

## Build prerequisites

This is a **closed-source-dependent** project — it cannot be built standalone. It references
`RebornBuddy.exe` and `GreyMagic.dll` via relative `HintPath`s (`Kombatant.csproj`):

```
..\..\GreyMagic.dll
..\..\RebornBuddy.exe
```

That means the repo must sit at `<RebornBuddy install root>\BotBases\Kombatant\` for the
build to find those references — matching the README's install instructions (clone into
`.\RebornBuddy\BotBases`). There is no NuGet restore path for these; RebornBuddy is licensed,
closed-source software you must own and have installed separately. Target framework is
.NET Framework 4.8 (`Kombatant.csproj`).

Build via Visual Studio / `msbuild Kombatant.sln` in the usual way; no custom build script.
No automated test suite — verification is "load it in RebornBuddy and watch it play," not
something Claude Code can do from a shell.

## Architecture

### Entry point and main loop (`Kombatant.cs`)

`Kombatant : AsyncBotBase` is the RebornBuddy-facing entry point. `Start()`/`Stop()` wire up
navigation (`Navigator.PlayerMover`/`NavigationProvider`), hotkeys, memory patches, and status
overlays. The actual per-tick logic lives in `AsyncRoot()` → `KombatantLogic()`, which runs a
**fixed-order chain of logic modules**, each returning `bool` — the first one that returns
`true` (meaning "I did something this tick") short-circuits the rest:

```
Loot → CommenceDuty → Mechanics → Convenience → Target → Avoidance → Movement → CombatLogic
```

Each module is a singleton implementing `LogicExecutor.ExecuteLogic()` (`Interfaces/ILogicExecutor.cs`
— despite the filename, it's an abstract class, not an interface; a `// TODO: Switch back to an
interface with C# v8` comment explains why). `LogicExecutor` also provides shared `ShouldExecuteX()`
gates (Pull/PullBuff/PreCombatBuff/Rest/Combat/CombatBuff/Heal/Death) that check both
`Settings.BotBase` toggles and whether the active combat routine (`RoutineManager.Current`)
actually implements that behavior.

### The `Logic/` modules

| File | Role |
|---|---|
| `Loot.cs` | Automated Need/Greed/Pass rolling — see dedicated section below, the most heavily reworked part of this fork. |
| `CommenceDuty.cs` | Duty Finder automation: auto-register/accept/commence duties, auto-leave after completion, auto-vote MVP (`VoteMvpAsync`), duty-ready sound notification. |
| `Mechanics.cs` | Gaze-attack avoidance (`ShouldAvertGaze`/`ExecuteAvertGaze`) and stand-still-debuff handling (`ShouldStandStill(OnZero)`/`ExecuteStandStill(OnZero)`). README's "Mechanics Warning" feature — noted there as buggy, only works with Auto Face Target disabled. |
| `Convenience.cs` (largest Logic file, ~555 lines) | Grab-bag of QoL automation: auto emote, ACT encounter auto-end, auto-accept quests/dialogue-skip, auto sprint, auto FATE-level-sync, cutscene skip, auto handover quest items, auto-accept revive, auto-select-yes, auto QTE, auto trade (`ExecuteAutoTrade`), auto mount/dismount. |
| `Target.cs` (2nd largest, ~724 lines) | Targeting strategies — `TargetAssistFixedCharacter/Leader/Tank/HighestLvlCharacter`, `TargetBestAoeEnemy`, `TargetNearestEnemy`, `TargetHighest/LowestHp(Percent)Enemy`, `TargetMostTargetedEnemy`, `TargetOnlyWhitelistedEnemy`. `ApplyPostFilters` narrows the candidate pool (range, FATE-relevance, etc.) before a strategy picks from it. |
| `Avoidance.cs` | Wraps RebornBuddy's `AvoidanceManager`; `PauseAvoidanceBecauseBoss()` gates it off during known dungeon bosses (README notes: ARR/HW/StB dungeon bosses only — no deep dungeon/raid/trial bosses). |
| `Movement.cs` | Auto-follow (leader/tank/target/fixed character), follow-triggered mount/dismount, flight takeoff (`PerformFlightTakeOff` — README flags this as still "dodgy"), auto sprint. |
| `CRLogic.cs` (`class CombatLogic`) | Thin wrapper invoking the active combat routine's Pull/Buff/Combat/Heal/Rest/Death behaviors via the `LogicExecutor` gates — the actual combat rotation logic is not this botbase's job, it delegates to whatever CR is loaded. |

### `Managers/`, `Memory/`, `Settings/`

- **`Managers/LootManager.cs`** — low-level loot memory access; see Loot system section.
- **`Managers/TargetManager.cs`** — thin wrapper over target-related memory writes (`ClearCurrentTarget`/`ClearFocusTarget`/`Focus`), reading `TargetOffsets` (from `Memory/Offsets.cs`) to poke the game's current/focus target pointers directly.
- **`Managers/DutyManager.cs`** — small duty-state helper used by `CommenceDuty`.
- **`Memory/Offsets.cs`** — resolves all raw memory addresses/function pointers this botbase needs (loot function, loot array address, agent vtables, target manager, trader trade stage) via `GreyMagic`'s `PatternFinder`, using byte-signature scans (`"Search 48 8D 05 ? ? ? ? ... TraceRelative"` style patterns) against the running game client. **These signatures are FFXIV-client-version-specific and will break on game patches** — a broken offset logs `"[Offset] <name> not found."` at startup (`init()` in `Kombatant.cs` catches this into `_memoFaliure`) rather than crashing, but loot/targeting/etc. relying on that offset then silently no-ops. Resolved once into a singleton (`Offsets.Instance`) — a prior version created a separate `PatternFinder` per call site, each copying the whole client memory space; consolidated into one shared instance (git history: "Change offset finding to use a singular pattern finder").
- **`Settings/BotBase.cs`** (~1770 lines, ~90 properties) — the persisted settings object. Extends RebornBuddy SDK's `JsonSettings`; every property setter calls `OnPropertyChanged()` which both raises `INotifyPropertyChanged` (for the WPF settings UI) and immediately `Save()`s to `<CharacterSettingsDirectory>\Kombatant\Settings.json` — settings are per-character, not global, and persist on every single change rather than requiring an explicit save action. `Settings/Fleeting.cs` and `Settings/Hotkeys.cs` are smaller sibling settings files (transient/session state and hotkey bindings respectively).
- **`Forms/`** — two parallel settings UIs: a WPF one (`SettingsForm`/`SettingsControl.xaml`, the default) and a legacy WinForms one (`ClassicSettingsForm`, opt-in via `UseWinFormsSettings`). `OverlayManager` drives the in-game status/focus overlays.

## Loot system (the core of this fork's own work)

The upstream/inherited loot code didn't reliably automate FFXIV's Need/Greed/Pass window —
git history (2026-04-25 through 2026-07-14, ~20 commits) shows this being rewritten from
scratch through direct trial-and-error against the game's memory layout and network
behavior. The current design, split across `Managers/LootManager.cs` and `Logic/Loot.cs`:

- **`LootItem` struct** (`LootManager.cs`) — a `[StructLayout(Explicit)]` overlay onto the
  game's own 0x44-byte loot array entries (`ObjectId`, `ItemId`, `RollState`, `RolledState`,
  `LeftRollTime`, `Index`). `Valid` filters out empty/garbage slots (`ItemId` capped at
  2,000,000 to admit HQ items, which use `baseId + 1,000,000`, while rejecting phantom
  memory reads — real FFXIV item IDs top out well below that). `RollDirect`
  (`LootManager.RollDirect`) calls the game's roll function directly via
  `Core.Memory.CallInjected64` against `Offsets.Instance.LootRollFunc` — the single call
  path used today; earlier history shows several abandoned approaches (`SendAction`
  guessing, a separate `LootFunc` two-pass scheme) before landing here.
- **`Loot.ExecuteLogic()`** (`Logic/Loot.cs`) scans all 16 loot slots (`LootSlots = 16`) once
  per tick, processing **one item action per tick** (returns after the first slot it acts on).
  Per item, it picks a roll option from a **fallback chain** driven by `BotBase.LootMode`
  (`NeedAndGreed` → Need→Greed→Pass or Greed→Pass depending on `RollState`; `GreedAll` →
  Greed→Pass; `PassAll` → always Pass), retrying each option up to `AttemptsPerOption` (3)
  times before advancing to the next — `_failCount` tracks attempts per item, `_attemptedItems`
  marks items given up on entirely.
- **Silent-rejection detection (`_verifyQueue`/`RunVerification`)** — the load-bearing fix in
  this system: `RollDirect` returning `true` does **not** guarantee the server actually
  accepted the roll (observed failure case: rolling on an already-owned unique item, or a
  contested relic weapon). After an apparently-successful roll, the item is watched for up
  to `VerifyChecks` (150) ticks (~5s) for `RolledState` to actually advance; if it never does,
  the roll is treated as a silent failure and the fallback chain is force-advanced past that
  option rather than retrying it forever.
- **Item identity key** — `(ObjectId, ItemId, Index)`, not just `(ObjectId, ItemId)`: two
  identical items from the same corpse (e.g. duplicate Triple Triad cards) share the first two
  fields and would otherwise collide, permanently blocking the second copy once the first is
  marked attempted.
- **`LootMode`** (`Enums/LootMode.cs`): `DontLoot`/`NeedAndGreed`/`GreedAll`/`PassAll`.
  **`RollState`** (`Enums/RollState.cs`, raw game values): `UpToNeed`/`UpToGreed`/`UpToPass`/
  `Rolled=17`/`NoLoot=26`. **`RollOption`** (`Enums/RollOption.cs`, raw game values):
  `Need=1`/`Greed=2`/`Pass=5`/`NotAvailable=7`.

If loot rolling breaks again after a game patch, the first things to check are (in this
order): whether `Offsets.Instance.LootRollFunc`/`LootsAddr` still resolve (a patch can shift
the byte signature), whether the `LootItem` struct's field offsets (`[FieldOffset(...)]`)
still match the game's actual struct layout (size assumed to stay `0x44`), and whether
`RollOption`/`RollState`'s raw numeric values are unchanged.

## Localization

`Localization/Localization.resx` (English) + `Localization.zh-CN.resx` (Chinese) — reflects
Akira0245's contribution and the userbase this fork inherited. `LocalizationInitializer`
picks the active resource set at startup.
