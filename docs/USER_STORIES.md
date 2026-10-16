---
codex: 1
project: GTFO.AbsurdWeapons
code: G.AWPN
layer: stories
status: living
updated: 2026-10-15
---
<!-- GENERATED from MindAttic.Workflow/data/workflow.db by `workflow db render`. Do not edit: this file is rewritten from the database; change stories and tickets with the workflow CLI. -->

# GTFO.AbsurdWeapons — User Stories
> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned. Every ✅ cites the test.

## Priority backlog
_Nothing recorded yet._


## Epic X — Experiments and spikes
- **G.AWPN-US-X1 ⬜** Spike: as a player, I see Mjölnir spelled with its ö in the loadout screen, inventory and HUD - test whether GTFO's TextMeshPro fonts render non-ASCII weapon names (or show a missing-glyph box), and whether a non-ASCII name survives gear JSON, datablock lookup and multiplayer sync. If it works, rename Mjolnir to Mjölnir (display names only; keep config section and ids ASCII).

## Tickets
> Permanent log of every change and fix — this repo's ticket system. Rows are append-only: never
> delete or reword one; only move its Status (open → closed / wont-do) and fill in Ref. Status is
> a word, not a glyph, so tickets don't count toward the story index. Keep this section last.

| ID | Date | Type | Status | Summary | Ref |
|---|---|---|---|---|---|
| G.AWPN-T1 | 2026-10-15 | docs | closed | Start the permanent ticket log in the workflow database | — |
| G.AWPN-T2 | 2026-10-15 | feature | closed | Glaive v1: new selectable melee weapon - IsoCo Stinger (Spear_02) model swung overhead with the sledgehammer's animation/SFX sets. GameDataInit.Initialize postfix registers MeleeArchetype/GearCategory/PlayerOfflineGear blocks at fixed id 1001 via AddBlock (cloned from Spear/MeleeSpear/Spear_02; hammer damage, stagger, hit width and third-person movement; spear reach and cleave; hold-thrust off). Compatible with GTFO.EZ (its melee tuning keys by name). Builds clean; loadout appearance, hand placement and hit detection still need an in-game check | src/GTFO.Glaive/GlaiveInjector.cs |
| G.AWPN-T3 | 2026-10-15 | feature | closed | Add twelve weapons beyond the Glaive (Katana with 40 m dash, Mjolnir, Warhammer, Shadow Dagger, plasma, hive, laser guns, Tentacle Cannon); drop the broken Rapier | in-game manual test |
| G.AWPN-T4 | 2026-10-15 | docs | closed | Docs cover all thirteen weapons: lasers, Katana 40 m dash, Warhammer knockback, new Tentacle Cannon; Rapier removed | package/README.md |
| G.AWPN-T5 | 2026-10-16 | feature | closed | Rename the mod to GTFO.AbsurdWeapons and grow it to twenty weapons (Yari, Tanto/Wakizashi/Katana/Odachi, Gravity Hammer, Mjolnir throw, Railgun, Chain Lightning, Grenade Launcher, Tentacle Pistol/Rifle) with PageUp/PageDown mid-level gear cycling and a unit-test project | tests/GTFO.AbsurdWeapons.Tests |
| G.AWPN-T6 | 2026-10-16 | feature | closed | Absurd weapons as a toggle on a serious run: Home restores the starting loadout and ammo, guns have infinite ammo and swap in loaded, pistol/rifle pairs share identical stats, tentacles reach the strikers' own length and are drawn barrel-to-target, swap messages last 5 s and replace each other | in-game manual test |
| G.AWPN-T7 | 2026-10-16 | chore | closed | install.ps1 adds this repo's 15 .prose/commands stubs, AGENTS.md pointer for the shared global commands, so any LLM client finds them | mindattic-agent-standard/install.ps1 run 2026-10-16 |
| G.AWPN-T8 | 2026-10-16 | chore | open | Bug hunt: run an exhaustive /bughunt over the whole repo and fix every real bug found (each fix under its own fix ticket), then hunt again; close this only when a full pass finds nothing | user request 2026-10-16: approved repo sweep |
| G.AWPN-T9 | 2026-10-16 | chore | open | Run the full unit test suite and fix every failure (each under its own fix ticket) until it passes; put the passing run in --ref. No test project: close this wont-do saying so | user request 2026-10-16: approved repo sweep |
| G.AWPN-T10 | 2026-10-16 | chore | open | Clean up code and docs: remove dead code, stale comments and outdated or duplicated docs, and simplify where behaviour doesn't change (/simplify); tests stay green | user request 2026-10-16: approved repo sweep |
| G.AWPN-T11 | 2026-10-16 | docs | open | Bring README.md up to date with the code as it is now (what it is, setup, usage, commands, current state), following codex-standard/README-STYLE.md; README.htm regenerated if the repo has one | user request 2026-10-16: approved repo sweep |
| G.AWPN-T12 | 2026-10-16 | feature | closed | Flamethrower: short cone stream with range falloff, stacking afterburn DoT that outlives the stream, fire that spreads enemy-to-enemy and across scorched floor patches, a stagger pulse per tick, and an incendiary alt-fire grenade (reusing Grenade Launcher flight/bounce) that leaves a lingering fire pool on detonation instead of one instant blast | — |
| G.AWPN-T13 | 2026-10-16 | feature | closed | Flak Cannon (UT99-style): primary fires a cone of bouncing physical shrapnel shards (reusing Tentacle guns' per-pellet shotgun plumbing and the Grenade Launcher's bounce-off-world flight) with heavy range falloff; secondary/ADS fires a single slow compressed ball (Grenade Launcher flight, no fuse) that detonates on any contact into a radial burst of secondary shrapnel rays layered over the main blast | — |
| G.AWPN-T14 | 2026-10-16 | feature | open | Victory Banner: a plantable tool (Grenade Launcher flight, stops instead of bouncing) that pulses an expanding green ring every few seconds out to 8 m, healing every teammate inside the radius on each pulse (OverlapSphere scan like ImpactBursts but against players), with a limited lifetime and one active banner per player | — |
| G.AWPN-T15 | 2026-10-16 | chore | closed | Flamethrower and Flak Cannon: expose every tunable number (gun stats included - damage, rate of fire, clip, pellet count/spread) as its own BepInEx config entry, same as the per-melee-weapon config already in WeaponSettings | — |
