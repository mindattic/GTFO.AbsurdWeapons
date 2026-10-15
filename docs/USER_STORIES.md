---
codex: 1
project: GTFO.Weapons
code: GWP
layer: stories
status: living
updated: 2026-10-15
---
<!-- GENERATED from MindAttic.Workflow/data/workflow.db by `workflow db render`. Do not edit: this file is rewritten from the database; change stories and tickets with the workflow CLI. -->

# GTFO.Weapons — User Stories
> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned. Every ✅ cites the test.

## Priority backlog
_Nothing recorded yet._

## Tickets
> Permanent log of every change and fix — this repo's ticket system. Rows are append-only: never
> delete or reword one; only move its Status (open → closed / wont-do) and fill in Ref. Status is
> a word, not a glyph, so tickets don't count toward the story index. Keep this section last.

| ID | Date | Type | Status | Summary | Ref |
|---|---|---|---|---|---|
| GWP-T1 | 2026-10-15 | docs | closed | Start the permanent ticket log in the workflow database | — |
| GWP-T2 | 2026-10-15 | feature | closed | Glaive v1: new selectable melee weapon - IsoCo Stinger (Spear_02) model swung overhead with the sledgehammer's animation/SFX sets. GameDataInit.Initialize postfix registers MeleeArchetype/GearCategory/PlayerOfflineGear blocks at fixed id 1001 via AddBlock (cloned from Spear/MeleeSpear/Spear_02; hammer damage, stagger, hit width and third-person movement; spear reach and cleave; hold-thrust off). Compatible with GTFO.EZ (its melee tuning keys by name). Builds clean; loadout appearance, hand placement and hit detection still need an in-game check | src/GTFO.Glaive/GlaiveInjector.cs |
