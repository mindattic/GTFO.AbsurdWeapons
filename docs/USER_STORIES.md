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
