# Changelog

## v1

- Loot uses GTFO.EZ's kit loot, shared between the two mods: a few small kits of 1-2 charges
  adding about 20% to the zone's resource packs (`KitLootPercent`, `KitChargesMin/Max`), with Hand
  Grenades at 4 to the kits' 3 and Stim Packs alongside. `KitSize` and `SpawnsPerZoneMultiplier`
  are gone.
- Insert (infinite ammo) and Delete (infinite health) are the shared toggles every GTFO.* mod
  has; `[Ammo] InfiniteAmmo` and `InfiniteAmmoToggleKey` are gone.
- Artifacts stay: Hand Grenades now join artifacts instead of replacing them - artifacts are as rare
  as vanilla. Grenades are common instead: half the loot mix (`HandGrenadeWeight` 4) and half as
  much loot again per zone (`SpawnsPerZoneMultiplier` 1.5). The `ArtifactsBecomeHandGrenades` setting is
  gone.
- Loot: where it clashes with GTFO.EZ, this mod's loot wins - GTFO.EZ's kit-only tables (its
  *Consumables Are Kits*) get this mod's mix too, Hand Grenades and Stim Packs alongside kits at
  this mod's own kit odds and size.
- Initial release with nineteen weapons, each marked **(!)**:
  - **Melee:** Katana (the Mastaba blade at twice a real katana's length; a full charge dashes up to
    10 m with the swing held, cutting the moment you reach an enemy or a wall), Mjolnir (a thunderclap on
    every hit; a full charge throws it end over end, throwing chain lightning as it flies out and
    back), Warhammer (500-damage blow, shockwave, a crater of scattered debris on a full-charge slam,
    flings enemies up to 12 m, and walls hurt them), Shadow Dagger
    (a fully charged hit makes you unseen for 3 s; kills while hidden add +25% damage up to +100%,
    every kill while hidden also adds +0.33 s, up to a 3 s cap that drains 5% with every enemy hit, resetting when the stealth ends; putting it away ends it).
  - **Guns:** Plasma Pistol/Rifle (Doom plasma), Hive Pistol/Rifle (Titanfall smart
    rounds, with lock-on markers), Laser Pistol/Rifle (continuous burning beams), Tentacle Cannon (bursts of striker tentacles
    from spots around the edge of your view, biting a nearby enemy or reaching for walls and floors otherwise), Railgun (a slow, devastating hitscan slug with infinite penetration and a double-helix beam),
    Chain Lightning Gun (bolts that jump between up to six enemies), Grenade Pistol/Rifle (lobbed lock-melter grenades that blow on
    an enemy or bounce and blow after 2.5 s, flashing red faster and faster), Rocket Launcher (Quake 1's
    rocket: dead straight, no drop, trailing sparks and dust, blowing up the first thing it touches - and
    nearby players too), Shrink Beam (a continuous ray that shrinks whatever it's held on until it gibs
    at a third of its size; once one's half-size or smaller, look down at it within 1 m to step on it).
  - **Tool:** Victory
    Banner (plant a flag with a glowing healing aura, pick it back up; heals everyone standing in it).
  - **Consumable:** Hand Grenade (a lock melter thrown like a glowstick, lobbed twice as hard, that bounces and blows after
    3 s; nobody starts with any; each one found is 1 more, up to 6) and Stim Pack (an overheal
    like Doom's: +50% health, up to 200%, draining back to 100%).
- Guns use ammo like vanilla guns; each starts full the first time it's equipped in a level and keeps
  its own ammo after that (`[Ammo] InfiniteAmmo` brings back bottomless guns).
- Loot consumables are only Hand Grenades and health, tool and ammo kits (`[Loot]`), plus Stim
  Packs when switched on - the Stim Pack is off by default for now.
  Artifacts stay as rare as vanilla.
- Swap weapons in a level with Page Up / Page Down (configurable): the gear you dropped in with joins
  the cycle the first time you swap, and Home puts it all back.
- Config: every weapon has its own on/off switch (`<Weapon> Enabled`, pistol and rifle versions
  separately), and 200+ settings retune them - damage, rate of fire, ranges, blasts, the Katana's
  dash, Mjolnir's throw, the Warhammer's shockwave and slam, the Shadow Dagger's stealth, Chain
  Lightning's jump count, every sound, and more. `[Gear Cycling]` keys; `[Tentacles] PoolMultiplier`
  raises how many tentacles the game can draw at once. Full reference in the README.
