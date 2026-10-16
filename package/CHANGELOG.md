# Changelog

## v1

- Initial release with eleven weapons:
  - **Glaive:** the IsoCo Stinger spear model, swung overhead with the sledgehammer's animations and
    sounds. Hammer-weight blows, the spear's long reach, and hits every enemy in the swing.
  - **Katana:** the Mastaba Fixed Blade with its blade stretched to ~2.2 m overall, wielded like the bat with the
    spear's 3.2 m reach.
  - **Mjolnir:** the Santonian Mallet hammer model, wielded like the bat. Charged hits release an
    electric shock: 8 damage and a stagger to every other enemy within 5 m.
  - **Rapier:** the Mastaba Fixed Blade knife model with its blade stretched to Katana size,
    wielded exactly like the spear.
  - **Warhammer:** the Kovac Sledgehammer with a 3x head: 500-damage blow, x55 knockback, and a
    shockwave on every hit that staggers everything within 6 m.
  - **Shadow Dagger:** the Mastaba Fixed Blade as a knife with 2.5x-speed stabs; a fully charged
    hit on an enemy makes you unseen by enemies for 6 seconds.
  - **Plasma Pistol** (main, Raptus Steigro) and **Plasma Rifle** (special, Omneco LRG): modeled on
    Doom's plasma gun. Rapid blue plasma balls (5 × 1d8 damage) that fly straight, plus a small
    electric splash on impact.
  - **Hive Pistol** (main, Shelling S49) and **Hive Rifle** (special, Malatack HXC): modeled on
    Titanfall's Smart Pistol. They lock onto up to 6 enemies near the crosshair; every round curves
    out of the barrel onto one of them.
  - All four: full-auto, no recoil, infinite ammo.
  - **Tentacle Cannon** (special): the Shelling Arid 5 forced full-auto; every round is a striker
    tentacle (~8 a second) that lashes at an enemy near the line of fire, 10 damage and a heavy
    stagger each; no recoil, infinite ammo.
- Config: per-weapon `BladeLength` (stretches the blade or head), `BladeLengthAxis` and `Reach`;
  `[Tentacles] PoolMultiplier` (4x by default) raises how many tentacles the game can draw at once.
