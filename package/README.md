# GTFO.Weapons

*(Listed on Thunderstore as `GtfoWeapons` — package names there are restricted to letters, numbers,
and underscores, so they can't contain a period.)*

A BepInEx plugin for GTFO that adds **thirteen new weapons**: five melee weapons that put one
weapon's model on another's moves, and eight guns: plasma, smart-homing and laser-beam guns, each
in both a pistol (main slot) and a rifle (special slot), and a cannon that fires the strikers'
tentacles. No game files are shipped or edited. Every weapon is built at load time from the game's
own data, so they pick up vanilla balance changes, and nothing vanilla is modified.

They show up in the loadout screen next to the vanilla weapons, in their normal slots.

## The weapons

| Weapon | Slot | Model | Handles like | In one line |
|---|---|---|---|---|
| **Glaive** | Melee | IsoCo Stinger (spear) | Sledgehammer | A spear blade brought down overhead; long reach, cleaves |
| **Katana** | Melee | Mastaba Fixed Blade (knife), blade stretched to ~2.2 m overall | Bat | A long blade at spear reach; a fully charged swing dashes you 40 m, slashing everything you pass |
| **Mjolnir** | Melee | Santonian Mallet (hammer) | Bat | A short war hammer; charged hits release an electric shock |
| **Warhammer** | Melee | Kovac Sledgehammer, 3x head | Sledgehammer | 500-damage blow; every hit sends out a stunning shockwave and physically knocks enemies back |
| **Shadow Dagger** | Melee | Mastaba Fixed Blade (knife) | Knife, faster | Lightning-fast stabs; a fully charged hit makes you unseen for 6 seconds |
| **Plasma Pistol** | Main | Raptus Steigro (high-tech HEL pistol, no sight) | Steigro, full-auto | Doom's plasma gun: rapid blue plasma balls, straight; a small shock on impact |
| **Plasma Rifle** | Special | Omneco LRG (gauss rifle) | LRG, full-auto | The same plasma, faster |
| **Hive Pistol** | Main | Shelling S49 (plain pistol, no sight) | S49, full-auto | Titanfall's Smart Pistol: rounds curve out of the barrel onto locked enemies |
| **Hive Rifle** | Special | Malatack HXC (heavy auto rifle) | HXC, full-auto | A smart rifle: a faster stream of curving rounds |
| **Laser Pistol** | Main | Raptus Treffen 2 (full-auto pistol, no sight) | Treffen 2, held low | One continuous red beam while the trigger is held; it burns and sparks wherever it lands |
| **Laser Rifle** | Special | Drekker DEL P1 (precision rifle) | DEL P1 | A thicker, harder-burning beam out to 70 m |
| **Tentacle Cannon** | Special | Buckland XDist2 (long-range shotgun, iron sights) | XDist2, full-auto | A wild, level fan of striker tentacles that bite whatever they reach |

Plasma, Hive and Laser each come as a pistol (main slot) and a rifle (special slot), so you can
carry either one twice, or one of each. The Tentacle Cannon is a special weapon too.

## Melee weapons

"Handles like" means everything except the model comes from that weapon: animations, sounds,
damage, reach, stamina costs, and the swing teammates see. The tables compare each one with the
vanilla weapons it borrows from.

### Glaive

A long razor-edged blade on a spear shaft, brought down overhead like a splitting maul.

| | Spear | Sledgehammer | **Glaive** |
|---|---|---|---|
| Model | MACO Drillhead / IsoCo Stinger | Kovac Sledgehammer etc. | **IsoCo Stinger** |
| Swing | forward thrust | overhead | **overhead** |
| Light / charged damage | 2 / 17.5 | 3 / 20 | **3 / 20** |
| Reach | 3.2 m | 1.8 m | **3.2 m** |
| Hit width | 0.15 | 0.3 | **0.3** |
| Hits several enemies per swing | yes | no | **yes** |

Every other stat (stamina costs, precision bonuses, etc.) comes from the spear. The spear's
hold-to-thrust charge is off because the hammer's animations have no motion for it.

### Katana

A long blade swung in wide two-handed arcs. The model is the Mastaba Fixed Blade with only the
blade stretched (7.5x, to about 2 m), so the grip stays knife-sized and the whole weapon is about
2.2 m long. A spear is about that long, so the Katana gets the spear's 3.2 m reach.

**Dash-slash:** release a fully charged swing and you dash up to **40 m** forward along your aim
(kept level with the ground) in 0.6 s. Every enemy you pass within 1.2 m takes the charged swing's
damage with a x3 stagger, once each per dash. The dash is your own movement, with collision, so
walls and props stop it short and teammates see you move.

| | Knife | Bat | **Katana** |
|---|---|---|---|
| Model | Mastaba Fixed Blade / Wox Compact | Kovac Peacekeeper / Attroc Titanium | **Mastaba Fixed Blade, 7.5x blade (~2.2 m overall)** |
| Swing | quick stabs | wide side swings | **wide side swings** |
| Light / charged damage | 2 / 5.5 | 3 / 12 | **3 / 12** |
| Reach | 1.75 m | 1.75 m | **3.2 m** |
| Fully charged swing | - | - | **+ 40 m dash, slashing everything passed (12 damage, stagger x3)** |

### Mjolnir

A short-hafted war hammer swung in fast, wide arcs: the Santonian Mallet on the bat's moveset.

**Thunderclap:** a charged hit that lands releases an electric shock with a blue flash. It hits
every other enemy within 5 m for up to 8 damage, falling off to 40% at the edge, and staggers them
3x as hard as a bullet would. The shock doesn't pass through walls. Quick taps don't trigger it.

| | Sledgehammer | Bat | **Mjolnir** |
|---|---|---|---|
| Model | Kovac Sledgehammer / Santonian Mallet etc. | Kovac Peacekeeper / Attroc Titanium | **Santonian Mallet** |
| Swing | overhead | wide side swings | **wide side swings** |
| Light / charged damage | 3 / 20 | 3 / 12 | **3 / 12** |
| Full charge time | 1.8 s | 1.0 s | **1.0 s** |
| Charged hit | - | - | **+ shock: 8 damage (falloff to 40%), stagger x3, 5 m around impact** |

### Warhammer

A sledgehammer with a head the size of a crate (3x). It charges like the sledgehammer, and every hit
sends enemies reeling: x55 stagger on a charged hit and x11 on a tap.

**Shockwave:** every hit that lands, tap or charged, sends out a shockwave with a warm flash. It
staggers every enemy within 6 m that it can reach, including the one you hit. The shockwave only
does 1 damage but carries 100x stagger, enough to knock back anything short of the game's
stagger-immune heavies. Walls block it.

**Knockback:** enemies are physically shoved across the floor, fast then easing off. The enemy you
hit slides 1.5 m along your swing (4 m on a charged hit), and everyone else in the shockwave slides
2 m straight out from the impact. Walls and ledges stop the slide, so nothing gets pushed through
the level or off it.

| | Sledgehammer | **Warhammer** |
|---|---|---|
| Model | Kovac Sledgehammer | **Kovac Sledgehammer, 3x head** |
| Charged damage | 20 | **500** |
| Light (tap) damage | 3 | **6** |
| Stagger on a charged hit | x1.3 | **x55** |
| Stagger on a light hit | x1 | **x11** |
| Damage to doors and locks | x3 | **x10** |
| Reach | 1.8 m | **2.4 m** |
| Hit width | 0.3 | **0.6** |
| Hits several enemies per swing | no | **yes** |
| Move speed while charged | 100% | **50%** |
| On any hit | - | **+ shockwave: stagger everything within 6 m** |
| Knockback | - | **struck enemy 1.5 m (charged 4 m), others 2 m** |

### Shadow Dagger

A plain-looking knife with two tricks. Its **quick stabs play 2.5x as fast** as the knife's, and
**a fully charged hit that lands on an enemy makes you unseen for 6 seconds**. Quick stabs and
partly charged hits don't trigger it. Another full-charge hit while you're hidden restarts the
count. While hidden:

- Enemies can't detect you, by sight or by noise.
- They won't lock on to you, and other enemies can't alert them to you.
- Enemies already chasing you lose you.
- You **shimmer**: each part of your body flickers 10 times a second on its own timing, so you look
  broken up and see-through to your teammates. Your own arms and the dagger shimmer the same way.
- Over the last second the shimmer fills back in to solid, as your warning.

Otherwise it handles like the knife: the Mastaba Fixed Blade, 2 / 5.5 damage, and the normal
charge-up. Hidden or not, you can still be hit by something already in the air, like a projectile
fired before you vanished.

## Guns

Every gun has **infinite ammo** (the clip refills after every shot, so they never reload or use
your ammo reserve) and **no recoil**. They damage only enemies, never you or your teammates. The
plasma and laser guns eject no shell casings. They're tuned to be fun first; balance comes later.

### Plasma Pistol (main) and Plasma Rifle (special)

Modeled on the plasma gun from Doom. Both fire a rapid stream of blue plasma balls (real enemy
projectiles, tinted blue) that fly dead straight where you aim and can't pass through walls or
doors. Each ball does Doom's rolled damage, **5 × 1d8 (5–40)**. The one addition is a small
electric splash on impact, like Mjolnir's shock: a small blue flash that hurts and staggers
everything within 1.25 m.

| | Doom's plasma gun | **Plasma Pistol** | **Plasma Rifle** |
|---|---|---|---|
| Model | - | Raptus Steigro (high-tech HEL pistol, no sight) | Omneco LRG (gauss rifle) |
| Slot | - | main | special |
| Fire rate | 1 every 3 tics (~11.7/s) | **~11.7/s** | **~17.5/s** |
| Damage per ball | 5 × 1d8 | **5 × 1d8 (5–40)** | **5 × 1d8 (5–40)** |
| Projectile speed | 25 units/tic | **28.6 m/s** | **28.6 m/s** |
| Impact splash | - | **6 damage (falloff to 40%), stagger x3, 1.25 m** | same |

### Hive Pistol (main) and Hive Rifle (special)

Modeled on Titanfall's Smart Pistol. Each locks onto up to 6 enemies within 35° of your crosshair
(60 m range). Every round leaves the barrel along your aim and bends smoothly onto one of the
locked enemies, taking turns between them, and tracks its target as it moves. A round fired with
nothing locked, or whose target dies mid-flight, hunts down the nearest enemy instead. Every round
lands but only does **1 damage**, so it takes a long stream of them to bring anything down.

| | **Hive Pistol** | **Hive Rifle** |
|---|---|---|
| Model | Shelling S49 (plain pistol, no sight) | Malatack HXC (heavy auto rifle) |
| Slot | main | special |
| Damage per round | **1** | **1** |
| Fire rate | **~8/s** | **~11/s** |
| Lock-on | **up to 6 enemies, 35° cone, 60 m** | same |
| Round speed / lifetime | **45 m/s, 3 s** | same |

### Laser Pistol (main) and Laser Rifle (special)

A continuous red beam, drawn with the trip mine's own laser, from the barrel to whatever your
crosshair is on. It comes on when you pull the trigger, follows your aim every frame and goes off
when you let go. While it's on something it **burns**: a flickering white-hot spot, an orange glow
lighting up the surroundings, a steady spray of sparks and the odd scorch mark. Enemies under the
beam take damage 20 times a second.

| | **Laser Pistol** | **Laser Rifle** |
|---|---|---|
| Model | Raptus Treffen 2 (full-auto pistol, no sight), held a little low so the beam shows | Drekker DEL P1 (precision rifle) |
| Slot | main | special |
| Damage | **1.2 per tick, 20 ticks/s (24/s)** | **2 per tick, 20 ticks/s (40/s)** |
| Range | **40 m** | **70 m** |
| Beam | thin | thicker |

### Tentacle Cannon (special weapon)

Fires the strikers' own tentacles, **full-auto**: every round is a striker tentacle, mouth and all,
about 4 a second while you hold the trigger. They burst out of the barrel in a **wide, level fan**
(up to 30° left or right of your aim, only 5° up or down) and thrash wildly side to side as they go.
Each one goes for an enemy near its own line (within 15°, out to 25 m), **bites for 10 damage with
a heavy stagger (x5)**, hangs a moment and snaps back. A tentacle with no enemy near its line grabs
the nearest thing in front of it instead: a crate, a railing, a wall, the floor.

The model is the Buckland XDist2, a long-range shotgun with plain iron sights, held a little lower
and further out than usual so the tentacles have room.

| | Buckland XDist2 | **Tentacle Cannon** |
|---|---|---|
| Fires | a shotgun blast, semi-auto | **1 striker tentacle per round, full-auto** |
| Damage | pellets | **10 per tentacle, stagger x5** |
| Fire rate | slow pump | **~4.3 a second** |
| Spread | shotgun cone | **wide level fan: 30° sideways, 5° up/down** |
| Targeting | - | **an enemy within 15° of each tentacle's line, 25 m** |

## Config

`BepInEx/config/MindAttic.GTFO.Weapons.cfg` is created on first launch, with one section per melee
weapon:

| Setting | Default | What it does |
|---|---|---|
| `BladeLength` | Glaive 1, Katana 7.5, Mjolnir 1, Warhammer 3, Shadow Dagger 1 | How much the blade (or hammer head) is stretched; 1 is the model's own size. The blade grows away from the grip. The game re-reads it whenever it rebuilds the weapon (loadout screen, re-equip), so you can tune it without restarting. `BepInEx/LogOutput.log` shows the length in meters before and after each change. |
| `BladeLengthAxis` | Y (Warhammer: All) | Which of the model's axes is "length". GTFO's melee parts all run along Y. Auto picks the part's longest dimension instead, which on a hammer head is its width. All scales it evenly, so a head gets bigger instead of longer. |
| `Reach` | 0 (built-in: Katana 3.2, Warhammer 2.4) | How far hits land, in meters. A longer blade only looks longer until you also raise this. Takes effect on restart. |

Blade length is cosmetic and per player. Reach applies to your own swings.

A `[Tentacles]` section has one more setting:

| Setting | Default | What it does |
|---|---|---|
| `PoolMultiplier` | 4 | How many times more tentacles the game can draw at once, for every tentacle type (enemies' included). The game's own limits are small, and when a pool is full new tentacles just don't appear, so a Tentacle Cannon stream next to striker attacks could lose some. Takes effect on restart. |

## Multiplayer

Everyone in the lobby needs this mod. Weapons sync between players by an id that only exists for
players who have it installed. The player who fires controls the shot and deals its damage through
the normal bullet path, so health, stagger and kills stay in sync. Teammates are sent every shot,
beam, tentacle, flash and stealth, and see the same thing. Warhammer knockback is done by the host,
who owns enemy positions, so everyone sees the same slide.

## Compatibility

- **GTFO.EZ:** compatible, in either load order. This mod builds its weapons after GTFO.EZ has
  applied its tuning, so each new weapon starts from EZ's version of the weapon it's copied from.
  For example, if you raise EZ's Hammer damage, the Glaive hits harder too. Stats this mod sets
  itself, like the Warhammer's 500 damage, stay fixed. EZ tunes only vanilla weapons, by name and
  id, so it never changes these weapons a second time. The guns have infinite ammo anyway.
- **Other mods that add weapons** at the same ids (1001-1006 melee, 1101-1107 guns): if one already
  uses an id, that weapon isn't added and `LogOutput.log` says so. The rest still load.

## Requirements

BepInExPack_GTFO, which includes GTFO-API (used to send shots, beams and effects to teammates).

## Changelog

### v1

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
