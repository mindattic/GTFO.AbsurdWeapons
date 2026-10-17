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
| G.AWPN-T14 | 2026-10-16 | feature | closed | Victory Banner: a plantable tool (Grenade Launcher flight, stops instead of bouncing) that pulses an expanding green ring every few seconds out to 8 m, healing every teammate inside the radius on each pulse (OverlapSphere scan like ImpactBursts but against players), with a limited lifetime and one active banner per player | — |
| G.AWPN-T15 | 2026-10-16 | chore | closed | Flamethrower and Flak Cannon: expose every tunable number (gun stats included - damage, rate of fire, clip, pellet count/spread) as its own BepInEx config entry, same as the per-melee-weapon config already in WeaponSettings | — |
| G.AWPN-T16 | 2026-10-16 | feature | open | Split-slot pairs for the new weapons: Flak Pistol (main) + Flak Rifle (special) sharing the Flak Cannon's stats/behavior, and the same main+special pairing for the Flamethrower, matching the Plasma/Hive/Laser/Tentacle pistol-rifle pattern already in GunInjector | — |
| G.AWPN-T17 | 2026-10-16 | chore | closed | Melee weapon stats (light/charged damage, stagger multipliers, attack sphere radius, charge-speed penalty), the sword-tempo speeds, Shadow Dagger's stealth numbers and the Katana's dash all become BepInEx config entries, extending the existing WeaponSettings (Reach/BladeLength) pattern | — |
| G.AWPN-T18 | 2026-10-16 | chore | closed | Gravity Hammer's pull, Mjolnir's throw, the hammer-family impact bursts (Thunderclap/Shockwave/GravityBlast radius/damage/stagger/knockback) and Knockback's slam damage all become BepInEx config entries | — |
| G.AWPN-T19 | 2026-10-16 | chore | closed | Every existing gun's archetype stats in GunInjector (Plasma, Hive, Laser, Tentacle Pistol/Rifle, Railgun, Chain Lightning Gun: damage, rate of fire, clip size, pellet/spread) become BepInEx config entries, same treatment as the Flamethrower/Flak Cannon | — |
| G.AWPN-T20 | 2026-10-16 | chore | closed | Gun runtime subsystems - SeekerGuns' Plasma/Hive kind definitions, LaserGuns' range/tick damage, Railgun's damage/range, Chain Lightning's jumps/falloff/range, Tentacle Cannon's fan/reach/damage - become BepInEx config entries | — |
| G.AWPN-T21 | 2026-10-16 | chore | closed | Grenade Launcher's speed, fuse, blast radius/damage/stagger become BepInEx config entries | — |
| G.AWPN-T22 | 2026-10-16 | fix | closed | Bug hunt: fix static-initializer-order NullReferenceExceptions that crashed the mod on every load (ImpactBurst/LaserKind/SeekerKind reading ConfigEntry.Value before Bind() populated them), a Flak Cannon bounce off-by-one and falloff-distance miscount, Flamethrower afterburn double-stacking per tick, Victory Banner's kill-confirmation timing and empty network-message struct, and a stale Victory Banner loadout description | tests/GTFO.AbsurdWeapons.Tests (39 passing) |
| G.AWPN-T23 | 2026-10-16 | feature | closed | Add the C-Foam Pistol and Rifle: the C-Foam Launcher's own hardening foam sprayed full-auto instead of lobbed - barely any direct damage, but enough stacked hits fully encase an enemy, held in place by a heavy stagger pulse on its own clock until the foam's duration runs out | tests/GTFO.AbsurdWeapons.Tests (39 passing) |
| G.AWPN-T24 | 2026-10-16 | feature | closed | C-Foam Pistol/Rifle: the foam is now visible - a tinted blob grows on the enemy with each stack - and its color is configurable (White/Tan/Orange/Blue/Green/Purple, or Random for a fresh hue each time) | tests/GTFO.AbsurdWeapons.Tests (39 passing) |
| G.AWPN-T25 | 2026-10-17 | feature | closed | Katana dash distance drops from 40 m to 10 m (KatanaDash DashDistance default) | G.AWPN-T82 |
| G.AWPN-T26 | 2026-10-17 | feature | closed | Katana dash stops the moment it intersects an enemy instead of carrying on through them | G.AWPN-T82 |
| G.AWPN-T27 | 2026-10-17 | chore | closed | Remove the Tanto (melee 1009): datablocks, settings, tempo and docs | G.AWPN-T82 |
| G.AWPN-T28 | 2026-10-17 | chore | closed | Remove the Wakizashi (melee 1007): datablocks, settings, tempo and docs | G.AWPN-T82 |
| G.AWPN-T29 | 2026-10-17 | chore | closed | Remove the Odachi (melee 1008): datablocks, settings, tempo and docs | G.AWPN-T82 |
| G.AWPN-T30 | 2026-10-17 | fix | closed | Tentacle guns spew tentacles way off to the sides (30 deg flat fan, 60 deg engage cone, 35 deg grab fan, big sideways thrash): keep them in a tight cone in front of the gun | G.AWPN-T82 |
| G.AWPN-T31 | 2026-10-17 | feature | closed | Grenade Launcher: drop the C-Foam launcher model and glue hook - a plain semi-auto gun whose every shot launches one lock-melter grenade | G.AWPN-T82 |
| G.AWPN-T32 | 2026-10-17 | feature | closed | Hand Grenade: every player is given a stack of 99 straight into their consumable slot at the start of a level (no pickup at their feet), and the stack stays infinite | G.AWPN-T82 |
| G.AWPN-T33 | 2026-10-17 | chore | closed | Remove the Gravity Hammer (melee 1010): datablocks, its animation set, pull, release blast, settings and docs | G.AWPN-T82 |
| G.AWPN-T34 | 2026-10-17 | fix | closed | Mjolnir throw tumbles sideways: orient the copied hammer model so its handle spins end over end, around its middle | G.AWPN-T82 |
| G.AWPN-T35 | 2026-10-17 | feature | closed | Thrown Mjolnir casts its chain lightning when it gets within 3 m of an enemy (pulse range 6 m -> 3 m) | G.AWPN-T82 |
| G.AWPN-T36 | 2026-10-17 | chore | closed | Remove the C-Foam Rifle (gun 1114); the C-Foam Pistol stays | G.AWPN-T82 |
| G.AWPN-T37 | 2026-10-17 | chore | closed | Remove the Flak Cannon (gun 1112): its datablocks, shards, ball and docs | G.AWPN-T82 |
| G.AWPN-T38 | 2026-10-17 | fix | closed | Laser Pistol/Rifle still read as shooting bullets: make them a constant beam whose end is what damages enemies, with no bullet effects | G.AWPN-T82 |
| G.AWPN-T39 | 2026-10-17 | feature | closed | Railgun: start its beam further out from the player, and halve its recoil | G.AWPN-T82 |
| G.AWPN-T40 | 2026-10-17 | chore | closed | Remove the Flamethrower (gun 1111): its cone, afterburn, fire spread, fuel bomb and docs | G.AWPN-T82 |
| G.AWPN-T41 | 2026-10-17 | chore | closed | Deploy the package icon (and manifest) next to the plugin DLL on every build, so the mod manager shows the mod's icon for the local install | G.AWPN-T82 |
| G.AWPN-T42 | 2026-10-17 | feature | closed | Chain Lightning Gun: one continuous stream of lightning while the trigger is held instead of individual shots (same total damage per second) | G.AWPN-T82 |
| G.AWPN-T43 | 2026-10-17 | chore | open | Spike: reorganize into a shared AbsurdWeapons.Core plus one small mod per weapon (registry, single-patch dispatchers, per-weapon config/package); prove it with Railgun and Katana | docs/spikes/weapon-mods.md |
| G.AWPN-T44 | 2026-10-17 | feature | closed | Railgun: halve the double helix's spirals (a full turn every 1.2 m instead of 0.6 m) | G.AWPN-T82 |
| G.AWPN-T45 | 2026-10-17 | feature | closed | Pistol/Rifle pairs look right per model: grenades, plasma balls and laser beams leave each model's own barrel (as drawn on screen) and each grenade gun kicks like its own model, while what a shot does stays identical | G.AWPN-T82 |
| G.AWPN-T46 | 2026-10-17 | feature | closed | Railgun has no kickback at all (kickless recoil instead of T39's half kick), like the laser and lightning guns | G.AWPN-T82 |
| G.AWPN-T47 | 2026-10-17 | feature | closed | Sound pass: every weapon's sounds match what it is and does - gauss crack for the Railgun, flare-gun thump for the grenade guns, glue shots for C-Foam, snatcher-tentacle shoot/hit, a burning-torch loop for the laser beam, a crackling electric loop for Chain Lightning, thunder for Mjolnir, frag throw/bounce for the Hand Grenade; all configurable by event name | G.AWPN-T82 |
| G.AWPN-T48 | 2026-10-17 | feature | closed | Railgun reads as Quake's rail: slow and devastating - 150 damage a shot, 1.5 s between shots (was 60 / 1.0 s); penetration stays infinite | G.AWPN-T82 |
| G.AWPN-T49 | 2026-10-17 | feature | closed | Plasma Pistol/Rifle fire at Doom's real rate: one ball every 3 tics (3/35 s, ~11.7/s; was 2/35 s, 17.5/s) | G.AWPN-T82 |
| G.AWPN-T50 | 2026-10-17 | feature | closed | Hive Pistol/Rifle work like Titanfall 1's Smart Pistol: semi-auto 12-round mag; locks build up over time on enemies near the crosshair (tough ones need up to 3 pips) and show on them; only locked rounds home and never miss, unlocked rounds fly straight | G.AWPN-T82 |
| G.AWPN-T51 | 2026-10-17 | feature | closed | Grenade Pistol/Rifle fly like Quake 1's grenade launcher: launched at 19.6 m/s with a 6.5 m/s upward lob under Quake gravity (26 m/s2), bounce 0.5, 2.5 s fuse (instant on an enemy), 75 damage over 5.2 m falling to a third at the edge | G.AWPN-T82 |
| G.AWPN-T52 | 2026-10-17 | fix | closed | Victory Banner doesn't read as planting a flag with a healing aura: plant a real flag (pole and waving banner) in front of you and show a constant healing aura on the ground around it, pulsing as it heals; log each plant/pickup | G.AWPN-T82 |
| G.AWPN-T53 | 2026-10-17 | chore | closed | Remove the C-Foam Pistol (gun 1113): its foam stacking/hold, blob and sounds | G.AWPN-T82 |
| G.AWPN-T54 | 2026-10-17 | chore | closed | Remove the Tentacle Pistol (gun 1110); the Tentacle Rifle stays | G.AWPN-T82 |
| G.AWPN-T55 | 2026-10-17 | chore | closed | Rename the Tentacle Rifle to the Tentacle Cannon (display name only; id 1103 unchanged) | G.AWPN-T82 |
| G.AWPN-T56 | 2026-10-17 | feature | closed | Katana's blade twice as long (BladeLength 2.6 -> 5.2, ~1.4 m) with its reach stretched to match (2.3 -> 3.0 m) | G.AWPN-T82 |
| G.AWPN-T57 | 2026-10-17 | feature | closed | Katana dash holds its swing: a full charge released dashes you forward with the attack held, and the swing releases the moment you make contact with an enemy or a wall (or the dash ends) | G.AWPN-T82 |
| G.AWPN-T58 | 2026-10-17 | chore | closed | Remove the Yari (melee 1001) | G.AWPN-T82 |
| G.AWPN-T59 | 2026-10-17 | feature | closed | Hive Pistol/Rifle: rapid-fire full-auto (0.08 s, 30-round clip, 10 damage a round) instead of semi-auto, keeping the Titanfall lock-on | G.AWPN-T82 |
| G.AWPN-T60 | 2026-10-17 | feature | closed | Grenade Pistol/Rifle fire farther: faster launch (30 m/s, 5 m/s lift) under lighter gravity (18 m/s2) - about three times the reach | G.AWPN-T82 |
| G.AWPN-T61 | 2026-10-17 | fix | closed | Katana dash stops on its first frame (log: 'contact with a wall after 0.0 m'): the wall check read the player's position before the game updated it; look ahead for walls with a sphere cast instead | G.AWPN-T82 |
| G.AWPN-T62 | 2026-10-17 | fix | closed | Thrown Mjolnir is yawed partly to the left: orient it from its meshes' own axes - handle along the spin plane's up, head facing the flight direction - instead of a model-space bounding box that only fixed the handle | G.AWPN-T82 |
| G.AWPN-T63 | 2026-10-17 | fix | closed | Laser guns (and Chain Lightning) still play the gun's rapid-fire audio when the trigger is held: suppress the vanilla fire audio for the trigger-driven guns too, leaving only the beam/lightning loop | G.AWPN-T82 |
| G.AWPN-T64 | 2026-10-17 | feature | closed | Hive Pistol/Rifle back to the original behaviour (reverting T50/T59's firing): full-auto, instant locks on up to 6 enemies within 35 deg / 60 m that every round curves onto in turn, unlocked rounds hunt the nearest enemy; 1 damage, 0.09 s, 30 rounds - keeping T50's lock-on markers as the visual | G.AWPN-T82 |
| G.AWPN-T65 | 2026-10-17 | fix | closed | Laser Pistol/Rifle don't raise to the hip-fire pose when fired: let the gun fire again (its rays, sound, tracer, brass and kick stay suppressed, the muzzle flash is switched off) so the game raises it; the beam's damage stays trigger-driven | G.AWPN-T82 |
| G.AWPN-T66 | 2026-10-17 | fix | closed | Plasma Pistol/Rifle shot sounds like a continuous burning noise: use a punchy energy shot (the gauss gun's body layer, GAUSSPISTOLBODYA3D) instead of the shooter projectile's launch | G.AWPN-T82 |
| G.AWPN-T67 | 2026-10-17 | fix | closed | Tentacle Cannon tentacles bend off to floors and walls: with no enemy in reach a tentacle goes straight out along its own line (tight 8 deg cone) to full reach or the first surface on that line, instead of latching onto the nearest surface any of 10 probes found; it only curves onto an enemy in range | G.AWPN-T82 |
| G.AWPN-T68 | 2026-10-17 | feature | closed | Plasma Pistol/Rifle: extremely tight grouping (no hip-fire or aim spread - every ball goes exactly where the crosshair is) and twice the ball speed (28.6 -> 57.2 m/s) | G.AWPN-T82 |
| G.AWPN-T69 | 2026-10-17 | feature | closed | Victory Banner: held as the flag itself (the C-Foam launcher's model hidden), and planted (or picked up) the instant fire is pressed - no pressure charge-up | G.AWPN-T82 |
| G.AWPN-T70 | 2026-10-17 | feature | closed | Warhammer: a fully charged swing always slams the ground in front of you at its hit moment - shockwave, knockback, dust and flash - even when it hits no enemy (a hit's own shockwave counts as the slam, no double) | G.AWPN-T82 |
| G.AWPN-T71 | 2026-10-17 | fix | closed | Laser Pistol/Rifle kick when fired (T65 let them fire for the hip raise): give them a hold that never relaxes (always at hip height) and plays no recoil animation, and stop them firing again - no kickback at all | G.AWPN-T82 |
| G.AWPN-T72 | 2026-10-17 | fix | closed | Tentacle Cannon tentacles start too far back (5 cm behind the muzzle, inside the gun): start them out past the barrel's end (0.3 m, configurable) so they visibly come out of it | G.AWPN-T82 |
| G.AWPN-T73 | 2026-10-17 | feature | closed | Victory Banner places like a sentry: fire puts it down (and you switch to your main weapon), and you pick it back up by walking up to it and pressing Use with an on-screen prompt - not by firing again from anywhere | G.AWPN-T82 |
| G.AWPN-T74 | 2026-10-17 | feature | closed | Katana's swing at the end of its dash plays twice as fast (charged-attack timings halved in its own animation set, animation sped up to match) | G.AWPN-T82 |
| G.AWPN-T75 | 2026-10-17 | fix | closed | Grenade Pistol is in the special slot and Grenade Rifle in the main slot (the Arid 5 is a special-slot gun, the J 300 a main-slot one): put the Grenade Pistol on the Bataldo 3RB revolver (main) and the Grenade Rifle on the TechMan Veruta XII (special) | G.AWPN-T82 |
| G.AWPN-T76 | 2026-10-17 | feature | closed | Tentacle Cannon held very low like a minigun, and with no enemy in reach its tentacles wriggle tightly a few metres out in the narrow cone; only an enemy in reach makes them reach out (and curve) to it | G.AWPN-T82 |
| G.AWPN-T77 | 2026-10-17 | feature | closed | Tentacle Cannon bites do twice the damage (11 -> 22 a tentacle); enemies' own tentacles unchanged | G.AWPN-T82 |
| G.AWPN-T78 | 2026-10-17 | feature | closed | Katana's blade 33% longer (BladeLength 5.2 -> 6.9, ~1.85 m) with reach to match (3.0 -> 3.5 m) | G.AWPN-T82 |
| G.AWPN-T79 | 2026-10-17 | fix | closed | Laser guns still kick: the weapon archetype's own trigger logic (Update / OnFireShot) runs the gun's firing, fire animation and recoil before Fire - skip it entirely for the lasers, whose beam is trigger-driven anyway | G.AWPN-T82 |
| G.AWPN-T80 | 2026-10-17 | feature | closed | Laser Rifle on a model without animated parts: the Accrat ND6 instead of the Drekker DEL P1 | G.AWPN-T82 |
| G.AWPN-T81 | 2026-10-17 | fix | closed | Laser guns have no audible continuous sound (the torch loop played at the beam's far end): a continuous beam loop - the freeze thrower's emitter loop, FREEZERLOOP / FREEZERLOOPEND - played at the gun while the trigger is held | G.AWPN-T82 |
| G.AWPN-T82 | 2026-10-17 | feature | closed | Weapon rework session: bug-hunt fixes (C-Foam stagger pulse, afterburn crash/stacking, banner buff TryCast, Hive MaxLocks 0); Hand Grenade consumable; removed Tanto, Wakizashi, Odachi, Yari, Gravity Hammer, C-Foam guns, Flak Cannon, Flamethrower, Tentacle Pistol; Grenade Pistol/Rifle guns; trigger-driven laser and chain-lightning streams; per-weapon sounds; design pass (Railgun, Plasma, grenades); Katana dash/hold/length, Mjolnir throw, Warhammer slam, Victory Banner flag/aura/sentry placement, Tentacle Cannon rework (T25-T81, minus the T43 spike) | docs/design/weapon-references.md |
| G.AWPN-T83 | 2026-10-17 | fix | closed | Laser Rifle was in the main slot (Accrat ND6/SMGHeavyAuto is a main-slot SMG in vanilla): force its gear category's BaseItem to the special slot's item so it lands in the special slot | G.AWPN-T82 |
| G.AWPN-T84 | 2026-10-17 | fix | closed | Chain Lightning bolt spawn point pushed further forward (was starting inside the gun model) and its archetype trigger logic now skipped the same way the laser guns' is, so hip fire actually activates the stream | G.AWPN-T82 |
| G.AWPN-T85 | 2026-10-17 | feature | closed | Railgun beam: fewer helix coils (TurnLength 1.2 -> 2.4), and the core/strands green-tinted and semi-transparent (lower base alpha, not just the time fade) | G.AWPN-T82 |
