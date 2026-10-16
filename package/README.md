# GTFO.AbsurdWeapons

*(Listed on Thunderstore as `GtfoAbsurdWeapons` — package names there are restricted to letters, numbers,
and underscores, so they can't contain a period.)*

A BepInEx plugin for GTFO that adds **twenty new weapons**: nine melee weapons, ten guns and a
tool. No game files are shipped or edited. Every weapon is built at load time from the game's own
data, so they pick up vanilla balance changes, and nothing vanilla is modified.

They show up in the loadout screen next to the vanilla weapons, in their normal slots. Every one of
them has **(!)** after its name, so you can tell them from vanilla gear at a glance. In a level you
can swap between them without going back to the lobby (see [Swapping weapons in a level](#swapping-weapons-in-a-level)).

## The weapons

### Melee

| Weapon | Model | Handles like | In one line |
|---|---|---|---|
| **Yari** | IsoCo Stinger (spear) | Sledgehammer | A spear blade brought down overhead; long reach, cleaves |
| **Tanto** | Mastaba Fixed Blade, real tanto size | Knife, faster | The fastest, shortest sword |
| **Wakizashi** | Mastaba Fixed Blade, real wakizashi size | Knife, a bit faster | A short sword with a little more reach and weight |
| **Katana** | Mastaba Fixed Blade, real katana size | Bat | Wide cuts through everything in the arc; a full charge dashes you 40 m |
| **Odachi** | Mastaba Fixed Blade, Norimitsu Odachi size | Sledgehammer, slower | Huge overhead cuts; a full charge kills anything it reaches |
| **Mjolnir** | Santonian Mallet | Bat | Every hit releases a thunderclap; a full charge throws it, crackling chain lightning |
| **Warhammer** | Kovac Sledgehammer, 3x head | Sledgehammer | 500-damage blow; every hit flings enemies, and walls hurt them |
| **Gravity Hammer** | Omneco Maul | Sledgehammer | Hold the charge forever to drag enemies in; release to blast them out |
| **Shadow Dagger** | Mastaba Fixed Blade | Knife, faster | A fully charged hit makes you unseen for 6 seconds |

### Guns

All guns have **infinite ammo** (the clip refills after every shot, so they never reload or use
your reserve). They damage only enemies, never you or your teammates. They're tuned to be fun
first; balance comes later.

| Weapon | Slot | Model | In one line |
|---|---|---|---|
| **Plasma Pistol** | Main | Raptus Steigro | Doom's plasma gun: rapid blue plasma balls with a small shock on impact |
| **Plasma Rifle** | Special | Omneco LRG | The same plasma, faster |
| **Hive Pistol** | Main | Shelling S49 | Titanfall's Smart Pistol: rounds curve onto locked enemies, 1 damage each |
| **Hive Rifle** | Special | Malatack HXC | A faster stream of curving smart rounds |
| **Laser Pistol** | Main | Raptus Treffen 2 | One continuous beam while the trigger is held; burns and sparks where it lands |
| **Laser Rifle** | Special | Drekker DEL P1 | A thicker, harder-burning beam out to 70 m |
| **Tentacle Pistol** | Main | Bataldo 3RB | A stream of striker tentacles that bite whatever they reach |
| **Tentacle Rifle** | Special | Buckland XDist2 | A faster, harder-biting tentacle stream in a wide, level fan |
| **Railgun** | Special | Köning PR 11 | A hitscan slug with infinite penetration, wrapped in a double helix |
| **Chain Lightning Gun** | Special | Omneco exp1 | Lightning that leaps from enemy to enemy, up to five jumps |

### Tool

| Weapon | Model | In one line |
|---|---|---|
| **Grenade Launcher** | Stalwart Flow G2 (C-Foam launcher) | Lobs lock-melter grenades: instant on an enemy, else bounce and blow after 3 s; infinite ammo |

## Melee weapons

"Handles like" means everything except the model comes from that weapon: animations, sounds,
reach, stamina costs, and the swing teammates see, unless a stat below says otherwise.

### The Japanese swords: Tanto, Wakizashi, Katana, Odachi

All four are the Mastaba Fixed Blade with the blade sized to the real sword (the grip stays
knife-sized). They're a ladder: each step up is slower, hits harder and reaches further.

| | **Tanto** | **Wakizashi** | **Katana** | **Odachi** |
|---|---|---|---|---|
| Real blade length | ~25 cm | ~50 cm | ~70 cm | 2.26 m (the Norimitsu Odachi) |
| Moves | knife | knife | bat | sledgehammer |
| Light-attack speed | x1.6 | x1.25 | x1 | x0.85 |
| Light / charged damage | 2 / 6 | 3 / 9 | 4 / 14 | 6 / **9999** |
| Reach | 1.6 m | 2.0 m | 2.3 m | 3.5 m |
| Hits | one enemy | one enemy | every enemy in the arc | every enemy in the arc |
| Special | - | - | full-charge dash | a full charge kills anything |

**Katana dash-slash:** release a fully charged swing and you dash up to **40 m** forward along your
aim (kept level) in 0.6 s. Every enemy you pass within 1.2 m takes the charged swing's damage with
a x3 stagger, once each. Walls and props stop the dash short.

**Odachi:** charge damage scales with how long you hold, so even a partly charged cut is
devastating.

### Yari

A Japanese spear's long blade brought down overhead like a splitting maul: the spear's 3.2 m reach
and cleave with the sledgehammer's swing, sounds and 3 / 20 damage.

### Mjolnir

A short-hafted war hammer on the bat's moveset.

- **Thunderclap:** every hit that lands releases an electric shock with a blue flash, hitting every
  other enemy within 5 m for up to 8 damage (falling to 40% at the edge) with a x3 stagger. Walls
  block it.
- **Throw:** release a full charge and a Mjolnir flies from your hand, tumbling end over end, out
  to 25 m (or the first wall), then comes back to you (one throw at a time; the one in your hand
  stays put). It hits every enemy it passes through (20
  damage, x4 stagger, once each way) and every 0.12 s throws a lightning pulse at the nearest enemy
  within 6 m, which jumps on to two more (6 damage, 75% per jump).

### Warhammer

A sledgehammer with a head the size of a crate. Charged 500 / light 6 damage, x55 / x11 stagger,
x10 to doors and locks on a charged hit, 2.4 m reach, hits several enemies. **Shockwave:** every hit that lands
staggers everything within 6 m (1 damage, x100 stagger). **Knockback:** enemies are flung across
the floor: the one you hit 4 m (**12 m** on a charged hit), the rest 6 m out from the impact. Walls
and ledges stop the slide. **Wall slam:** an enemy that hits a wall with fling still to go takes
"sideways fall damage": 5 per metre it didn't get to travel (the first metre is free), with a
stagger and a puff of dust. A charged hit into a nearby wall does about 50.

### Gravity Hammer

The Omneco Maul on the sledgehammer's swing, with a charge that **never swings by itself**: hold it
as long as you like. While you hold it:

- Every enemy within 12 m that it can see is dragged toward you, 1.2 m at a time, five times a
  second, stopping 2.5 m away.
- A violet glow pulses in front of you, brighter as the charge builds. You move at 60% speed.

**Release a full charge** and it lets go 2.5 m in front of you (or just short of a wall):
everything within 8 m is staggered (x100, 15 damage falling to half at the edge) and thrown 5 m
back out, taking 3 per metre
of wall slam. The swing itself still hits (30 damage, cleaves). An
early release is just a normal swing.

### Shadow Dagger

Quick stabs play **2.5x as fast** as the knife's, and **a fully charged hit on an enemy makes you
unseen for 6 seconds**: enemies can't see or hear you, won't lock on, and lose you if they were
chasing. You shimmer while hidden, filling back in over the last second as a warning. Another
full-charge hit restarts the count. **Put the dagger away and it ends at once:** wielding any other item makes you
visible again.

- **Kills while hidden** sharpen the dagger: each adds **+25% damage**, up to **+100%**, for the
  rest of that stealth.
- **A fully wound-up kill while hidden** adds the wind-up's time to your stealth, so a chain of
  charged kills keeps you hidden.

## Guns

### Plasma Pistol (main) and Plasma Rifle (special)

Doom's plasma gun: a rapid stream of blue plasma balls (real enemy projectiles, tinted) that fly
straight and stop at walls and doors. Each does **5 × 1d8 (5–40)** and a small 1.25 m electric
splash (6 damage falling to 40% at the edge, x3 stagger). Pistol ~11.7 a second, Rifle ~17.5. No recoil, no brass.

### Hive Pistol (main) and Hive Rifle (special)

Titanfall's Smart Pistol: each locks onto up to 6 enemies within 35° of your crosshair (60 m), and
every round bends out of the barrel onto one of them, taking turns. Every round lands but does only
**1 damage**. Pistol ~8 a second, Rifle ~11. No recoil.

### Laser Pistol (main) and Laser Rifle (special)

One continuous red beam from the barrel to whatever your crosshair is on, for as long as you hold
the trigger. Where it lands it **burns**: a white-hot spot, an orange glow, sparks and scorch marks.
Pistol 24 damage a second out to 40 m; Rifle 40 a second out to 70 m. No recoil, no brass.

### Tentacle Pistol (main) and Tentacle Rifle (special)

Every round is a striker's own tentacle, mouth and all. They burst out in a **wide, level fan** (up
to 30° left or right, 5° up or down) thrashing side to side. **While any enemy is in front of you**
(within 60° of your aim, 25 m) every tentacle goes for one; only with no enemy around do they grab
the nearest crate, wall or floor. Each bite staggers hard (x5).

| | **Tentacle Pistol** | **Tentacle Rifle** |
|---|---|---|
| Model | Bataldo 3RB (HEL revolver) | Buckland XDist2 (shotgun, iron sights) |
| Rate | ~2.9 a second | ~4.3 a second |
| Damage per bite | 8 | 11 |

### Railgun (special)

The Köning PR 11, semi-auto, one shot a second. Each shot is an instant slug with **infinite
penetration**: it goes through every enemy and every wall out to 200 m, doing **60 to each enemy**
on the line (headshots still count). It's drawn as a white-hot beam wrapped in a spinning double
helix, with sparks wherever it punches through a wall. Keeps the sniper's scope and kick.

### Chain Lightning Gun (special)

The Omneco exp1, full-auto at ~6.7 bolts a second. Each bolt strikes the enemy under your crosshair
(or the nearest within 6° if you just miss), then **jumps** to the nearest enemy within 8 m it
hasn't hit, up to 5 jumps. Damage is 10 to the first, then 75% of the last on each jump. Jumps need
a clear line. No recoil, no brass.

## Tool

### Grenade Launcher

The C-Foam launcher, but every glob it would fire is a **lock melter rigged to blow**. One grenade
per trigger pull, with **infinite tool ammo**.

- **Hits an enemy:** explodes instantly.
- **Misses:** bounces off walls and floors, losing speed, and explodes **3 seconds** after firing.
- While it's out it **flashes red faster and faster** (with a beep each flash) as the fuse burns.
- The blast does 40 damage at the center, falling to 25% at 5 m, with a heavy stagger (x5). It
  never hurts players.

## Swapping weapons in a level

Hold a main, special, tool or melee weapon and press **Page Up** for the next of this mod's weapons
for that slot, or **Page Down** for the previous one. A one-second message names what you switched
to. Only this mod's weapons are in the cycle, never vanilla gear. Teammates get the swap too. The
keys are configurable (below).

## Config

`BepInEx/config/MindAttic.GTFO.AbsurdWeapons.cfg` is created on first launch.

One section per melee weapon:

| Setting | Default | What it does |
|---|---|---|
| `BladeLength` | Tanto 0.93, Wakizashi 1.87, Katana 2.6, Odachi 8.4, Warhammer 3, others 1 | How much the blade (or hammer head) is stretched; 1 is the model's own size. Re-read whenever the weapon is rebuilt (loadout screen, re-equip), so you can tune it without restarting. |
| `BladeLengthAxis` | Y (Warhammer: All) | Which of the model's axes is "length". All scales it evenly, so a head gets bigger instead of longer. |
| `Reach` | 0 (built-in: Yari 3.2, Tanto 1.6, Wakizashi 2.0, Katana 2.3, Odachi 3.5, Warhammer 2.4, Gravity Hammer 2.2) | How far hits land, in meters. Takes effect on restart. |

`[Gear Cycling]`:

| Setting | Default | What it does |
|---|---|---|
| `NextWeaponKey` | PageUp | Swap to the next of this mod's weapons for the slot you're holding. |
| `PreviousWeaponKey` | PageDown | Swap to the previous one. |

`[Tentacles]`:

| Setting | Default | What it does |
|---|---|---|
| `PoolMultiplier` | 4 | How many times more tentacles the game can draw at once, for every tentacle type (enemies' included). Takes effect on restart. |

## Multiplayer

Everyone in the lobby needs this mod. Weapons sync between players by an id that only exists for
players who have it installed. The player who fires deals the damage through the normal bullet
path, so health, stagger and kills stay in sync. Teammates are sent every shot, beam, tentacle,
bolt, grenade, flash and stealth, and see the same thing. Knockback and the Gravity Hammer's pull
are done by the host, who owns enemy positions.

## Compatibility

- **GTFO.EZ:** compatible, in either load order. Weapons are built after EZ's tuning, so each
  starts from EZ's version of the weapon it's copied from. Stats this mod sets itself stay fixed.
- **Other mods that add weapons** at the same ids (1001-1010 melee, 1101-1110 guns, 1201 tool): if
  one already uses an id, that weapon isn't added and `LogOutput.log` says so. The rest still load.
  The mod also adds melee animation sets 1006-1010, recoil profiles 1101-1110 and first-person hold
  1103; a clash there drops that weapon (animation sets) or leaves it its model's recoil.

## Requirements

BepInExPack_GTFO, which includes GTFO-API (used to send shots and effects to teammates).

## Changelog

See `CHANGELOG.md`.
