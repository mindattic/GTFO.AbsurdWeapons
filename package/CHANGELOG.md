# Changelog

## v1

- Initial release with thirteen weapons:
  - **Glaive:** the IsoCo Stinger spear model, swung overhead with the sledgehammer's animations and
    sounds. Hammer-weight blows, the spear's long reach, and hits every enemy in the swing.
  - **Katana:** the Mastaba Fixed Blade with its blade stretched to ~2.2 m overall, wielded like the
    bat with the spear's 3.2 m reach. A fully charged swing dashes you up to 40 m, slashing every
    enemy you pass.
  - **Mjolnir:** the Santonian Mallet hammer model, wielded like the bat. Charged hits release an
    electric shock: 8 damage and a stagger to every other enemy within 5 m.
  - **Warhammer:** the Kovac Sledgehammer with a 3x head: 500-damage blow, x55 stagger, a shockwave
    on every hit that staggers everything within 6 m, and physical knockback that slides enemies
    across the floor.
  - **Shadow Dagger:** the Mastaba Fixed Blade as a knife with 2.5x-speed stabs; a fully charged
    hit on an enemy makes you unseen by enemies for 6 seconds.
  - **Plasma Pistol** (main, Raptus Steigro) and **Plasma Rifle** (special, Omneco LRG): modeled on
    Doom's plasma gun. Rapid blue plasma balls (5 × 1d8 damage) that fly straight, plus a small
    electric splash on impact.
  - **Hive Pistol** (main, Shelling S49) and **Hive Rifle** (special, Malatack HXC): modeled on
    Titanfall's Smart Pistol. They lock onto up to 6 enemies near the crosshair; every round curves
    out of the barrel onto one of them for 1 damage.
  - **Laser Pistol** (main, Raptus Treffen 2) and **Laser Rifle** (special, Drekker DEL P1): one
    continuous beam while the trigger is held, burning and sparking wherever it lands (24 / 40
    damage a second, 40 / 70 m).
  - **Tentacle Cannon** (special, Buckland XDist2): full-auto striker tentacles (~4.3 a second) in a
    wide, level, thrashing fan; each bites an enemy near its line for 10 damage and a heavy stagger,
    or grabs the nearest surface.
  - All eight guns: no recoil, infinite ammo.
- Config: per-weapon `BladeLength` (stretches the blade or head), `BladeLengthAxis` and `Reach`;
  `[Tentacles] PoolMultiplier` (4x by default) raises how many tentacles the game can draw at once.
