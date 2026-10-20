# Weapon design references

Each weapon is modelled on a weapon from another game, so it behaves the way players already
expect. This records the reference and the numbers taken from it, converted to GTFO's scale
(Quake/Doom's 56-unit player = GTFO's 1.83 m, so 1 unit ≈ 0.0327 m; enemy health is compared
against GTFO's: striker 20, shooter 30, scout 42, big striker 120).

| Weapon | Reference | What it takes from it |
|---|---|---|
| Laser Pistol/Rifle | Trip-mine / sci-fi cutting beams | One continuous red beam, no shots, no kick; whatever its end touches burns (40 damage/s, 70 m). The gun never fires - the trigger drives the beam. |
| Railgun | Quake 2/3 railgun | Slow and devastating: kills **every** enemy on the line outright (9999 damage, past Quake's 100-150) through **every** wall out to 1000 m, 1.5 s between shots (Quake's 1.5 s). No kick. |
| Plasma Pistol/Rifle | Doom (1993) plasma rifle | Balls at twice Doom's 25 units/tic (≈57.2 m/s, by request), no spread at all, 5 × 1d8 damage, one every 3 tics (≈11.7/s), dead straight, no recoil. One addition: a small electric splash (Doom 2016's plasma splashes). |
| Hive Pistol/Rifle | Titanfall 1 Smart Pistol (loosely) | Kept as the original design by choice (a Titanfall-faithful lock-up version was tried and reverted, keeping its lock markers): full-auto ~11/s, every round curves onto one of up to 6 enemies near the crosshair (35°, 60 m), taking turns; 1 damage a round, a stream that never misses. |
| Grenade Pistol/Rifle | Quake 1 grenade launcher (stretched range) | Quake's lob and bounce, but flung farther for GTFO's distances: 30 m/s along the aim plus 5 m/s up under 18 m/s² gravity (Quake's own 19.6 m/s / 26 m/s² arced too short); bounce keeps half the speed; explodes on touching an enemy, else after 2.5 s; 0.6 s between shots; blast 160 u (5.2 m), falling to a third at the edge. |
| Rocket Launcher | Quake 1 rocket launcher (`W_FireRocket`/`T_MissileTouch`/`T_RadiusDamage`), Doom's gravity-free straight flight (`MT_ROCKET`'s `MF_NOGRAVITY`) | 1000 u/s (32.7 m/s) dead straight, no drop, no bounce - explodes on the first enemy or wall it touches. `T_RadiusDamage`'s own 120-damage, half-distance falloff, searched out to 160 units (5.2 m, same as the Grenade Launcher's): 110 at the center, a third left at the edge. 0.8 s between shots (Quake 1's own refire). |
| Chain Lightning Gun | Return to Castle Wolfenstein's Tesla gun / Diablo's chain lightning | A continuous stream while held, arcing from the first target to the nearest unstruck enemy, up to five jumps at 75% each. |
| Tentacle Cannon | (original) | Striker tentacles spewed in bursts from fixed spots around the edge of view, biting a nearby enemy or reaching for surfaces otherwise. No outside reference. |
| Shrink Beam | (original - the classic sci-fi shrink ray) | A continuous ray that shrinks whatever it's held on - smaller and slower together - until it pops outright; small enough, and just getting close enough pops it too, like stepping on it. No outside reference. |
| Victory Banner | TF2's Buff Banner / Battlefield's medic area heal | A planted flag with a visible healing aura: everyone inside heals a share of max health each pulse. |
| Katana dash | Overwatch's Swift Strike (shortened) | A 10 m dash with the swing held, released on contact with an enemy or a wall. |
| Mjolnir throw | God of War / Thor | Thrown end over end, returns to hand, calls lightning on enemies it passes within 3 m of. |
