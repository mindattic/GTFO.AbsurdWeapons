# GTFO.AbsurdWeapons

*(Listed on Thunderstore as `GtfoAbsurdWeapons` — package names there are restricted to letters, numbers,
and underscores, so they can't contain a period.)*

A BepInEx plugin for GTFO that adds **nineteen new weapons**: four melee weapons, thirteen guns,
a tool and a consumable. No game files are shipped or edited. Every weapon is built at load time from the game's own
data, so they pick up vanilla balance changes, and nothing vanilla is modified.

They show up in the loadout screen next to the vanilla weapons, in their normal slots. Every one of
them has **(!)** after its name, so you can tell them from vanilla gear at a glance. In a level you
can swap between them without going back to the lobby (see [Swapping weapons in a level](#swapping-weapons-in-a-level)).

## The weapons

### Melee

| Weapon | Model | Handles like | In one line |
|---|---|---|---|
| **Katana** | Mastaba Fixed Blade, real katana size | Bat | Wide cuts through everything in the arc; a full charge dashes you up to 10 m with the swing held - it cuts the moment you reach an enemy or a wall |
| **Mjolnir** | Santonian Mallet | Bat | Every hit releases a thunderclap; a full charge throws it, crackling chain lightning |
| **Warhammer** | Kovac Sledgehammer, 3x head | Sledgehammer | 500-damage blow; every hit flings enemies, and walls hurt them |
| **Shadow Dagger** | Mastaba Fixed Blade | Knife, faster | A fully charged hit makes you unseen for 2 seconds |

### Guns

All guns have **infinite ammo** (the clip refills after every shot and the reserve stays full, so
they never reload, and a gun you switch to starts fully loaded). They damage only enemies, never you or your teammates. They're tuned to be fun
first; balance comes later.

| Weapon | Slot | Model | In one line |
|---|---|---|---|
| **Plasma Pistol** | Main | Raptus Steigro | Doom's plasma gun: rapid blue plasma balls with a small shock on impact |
| **Plasma Rifle** | Special | Omneco LRG | The same plasma |
| **Hive Pistol** | Main | Shelling S49 | Titanfall's Smart Pistol: rounds curve onto locked enemies, 1 damage each |
| **Hive Rifle** | Special | Malatack HXC | The same curving smart rounds |
| **Laser Pistol** | Main | Raptus Treffen 2 | One continuous beam out to 70 m while the trigger is held; burns and sparks where it lands |
| **Laser Rifle** | Special | Accrat ND6 | The same beam |
| **Tentacle Cannon** | Special | Buckland XDist2 | A stream of striker tentacles that bite whatever they reach |
| **Grenade Pistol** | Main | Bataldo 3RB | Quake-style lobbed lock-melter grenades: instant on an enemy, else bounce and blow after 2.5 s |
| **Grenade Rifle** | Special | TechMan Veruta XII | The same grenades, from its own barrel with its own kick |
| **Railgun** | Special | Köning PR 11 | A hitscan slug with infinite penetration, wrapped in a double helix |
| **Chain Lightning Gun** | Special | Omneco exp1 | A continuous stream of lightning that leaps from enemy to enemy, up to five jumps |
| **Rocket Launcher** | Special | Buckland XDist2 | Quake 1's rocket: flies dead straight with no drop, trailing falling sparks and dust, and blows up the first thing it touches |
| **Shrink Beam** | Special | Omneco exp1 | A continuous ray that shrinks whatever it's held on until it pops; get close enough to a small one and you'll step on it |

### Tools

| Weapon | Model | In one line |
|---|---|---|
| **Victory Banner** | Stalwart Flow G2 (C-Foam launcher) | Put a flag down in front of you, like a sentry, with a glowing healing aura; walk up and press Use to pick it up. Heals everyone inside and buffs your damage |

### Consumables

| Item | Model | In one line |
|---|---|---|
| **Hand Grenade** | Lock melter (thrown like a glowstick) | Hold to wind up, release to throw; bounces, then blows after 3 s. Everyone starts each level with 99, and they never run out |

## Melee weapons

"Handles like" means everything except the model comes from that weapon: animations, sounds,
reach, stamina costs, and the swing teammates see, unless a stat below says otherwise.

### Katana

The Mastaba Fixed Blade with a ~1.85 m blade (a real katana's is ~70 cm; the grip stays knife-sized),
on the bat's moves: 4 / 14 damage, 3.5 m reach, cutting every enemy in the arc.

**Katana dash-slash:** release a fully charged swing and, instead of swinging, you dash up to
**10 m** forward along your aim (kept level) in 0.6 s **with the swing held**. The moment you make
contact - an enemy within reach, or a wall or prop that stops you - the charged cut is released
right there, twice as fast as a normal charged swing. If you reach nothing, it swings at the end of the dash.

### Mjolnir

A short-hafted war hammer on the bat's moveset.

- **Thunderclap:** every hit that lands releases an electric shock with a blue flash, hitting every
  other enemy within 5 m for up to 8 damage (falling to 40% at the edge) with a x3 stagger. Walls
  block it.
- **Throw:** release a full charge and a Mjolnir flies from your hand, tumbling end over end, out
  to 25 m (or the first wall), then comes back to you (one throw at a time; the one in your hand
  stays put). It hits every enemy it passes through (20
  damage, x4 stagger, once each way) and whenever an enemy is within 3 m of it, it casts chain lightning at
  the nearest one (every 0.12 s while one's that close), which jumps on to two more (6 damage, 75% per jump).

### Warhammer

A sledgehammer with a head the size of a crate. Charged 500 / light 6 damage, x55 / x11 stagger,
x10 to doors and locks on a charged hit, 2.4 m reach, hits several enemies. **Shockwave:** every hit that lands
staggers everything within 6 m (1 damage, x100 stagger). **Slam:** a fully charged swing always comes
down on the ground in front of you with the shockwave and a crater's worth of scattered debris and
scorch marks, not one small mark - and flings everything around away - even if it hits nothing. **Knockback:** enemies are flung across
the floor: the one you hit 4 m (**12 m** on a charged hit), the rest 6 m out from the impact. Walls
and ledges stop the slide. **Wall slam:** an enemy that hits a wall with fling still to go takes
"sideways fall damage": 5 per metre it didn't get to travel (the first metre is free), with a
stagger and a puff of dust. A charged hit into a nearby wall does about 50.

### Shadow Dagger

Quick stabs play **2.5x as fast** as the knife's, and **a fully charged hit on an enemy makes you
unseen for 2 seconds**: enemies can't see or hear you, won't lock on, and lose you if they were
chasing. You fade to a faint purple tint while hidden - the dagger's own glow - filling back in as
the time runs out, and flickering faster and faster over the last second as a warning before it
reverts. Another full-charge hit tops the stealth back up to the full 2 seconds (never cut short).
**Put the dagger away and it ends at once:** wielding any other item makes you
visible again.

- **Kills while hidden** sharpen the dagger: each adds **+25% damage**, up to **+100%**, for the
  rest of that stealth.
- **A kill made with a full charge while hidden** also adds **+0.25 seconds** to your stealth, so a
  chain of charged kills keeps you hidden.

## Guns

### Plasma Pistol (main) and Plasma Rifle (special)

Doom's plasma gun: a rapid stream of blue plasma balls (real enemy projectiles, tinted) that fly
dead straight at twice Doom's speed, with no spread at all - every ball goes exactly where the
crosshair is - and stop at walls and doors. Each does **5 × 1d8 (5–40)** and a small 1.25 m electric
splash (6 damage falling to 40% at the edge, x3 stagger). One every 3 tics, ~11.7 a second, like Doom. No recoil, no brass.

### Hive Pistol (main) and Hive Rifle (special)

Titanfall's Smart Pistol: each locks onto up to 6 enemies within 35° of your crosshair (60 m) -
you see a lock marker build up on each - and every round bends out of the barrel onto one of them,
taking turns. Every round lands but does only **1 damage**. ~11 a second. No recoil.

### Laser Pistol (main) and Laser Rifle (special)

One continuous red beam from the barrel to whatever your crosshair is on, for as long as you hold
the trigger. Where it lands it **burns**: a white-hot spot, an orange glow, sparks and scorch marks.
They don't shoot: it's one constant beam, and its end burns whatever enemy it's on, 40 damage a
second out to 70 m. No bullets, no muzzle flash, no kickback.

### Tentacle Cannon (special)

Every round is a burst of three striker tentacles, mouths and all, spewing out from spots just below
your eyeline around the view - never where you can see them start - and thrashing in from off-screen.
**While an enemy is near your crosshair and in reach**, every tentacle in the burst reaches for it,
each landing its own bite on its body; with none in reach they instead reach out along their own
random lines for the nearest wall, floor or crate (and melt a lock they grab). It's carried very low
at the waist, like a minigun. Each bite does 25 and staggers hard (x12); ~4.3 bursts a second, three
tentacles each.

### Grenade Pistol (main) and Grenade Rifle (special)

Plain semi-auto launchers on heavy guns - the Bataldo 3RB revolver (main) and the TechMan Veruta XII (special). A shot does
exactly the same from either; each just looks like its own gun firing - its own barrel, its own kick. Every shot is a **lock melter rigged to blow**, with infinite ammo.

- **Hits an enemy:** explodes instantly.
- **Misses:** bounces off walls and floors, losing half its speed each time, and explodes **2.5 seconds** after firing.
- While it's out it **flashes red faster and faster** (with a beep each flash) as the fuse burns.
- It's lobbed: launched fast with a little upward lift, so it arcs out a long way.
- The blast does 75 damage at the center, falling to a third at 5.2 m, with a heavy stagger (x5). It
  never hurts players.

### Railgun (special)

The Köning PR 11, semi-auto, one shot every 1.5 seconds. Each shot is an instant slug with **infinite
penetration**: it goes through every enemy and every wall out to 200 m, doing **150 damage** to every
enemy on the line (headshots still count). It's drawn as a white-hot beam wrapped in a spinning
double helix that spins, widens and fades over 0.8 s, with sparks wherever it punches through a wall.
Slow but devastating, like Quake's rail. Keeps the sniper's scope, with no kickback; the beam starts
well out ahead of you.

### Chain Lightning Gun (special)

The Omneco exp1, full-auto at ~6.7 bolts a second. Each bolt strikes the enemy under your crosshair
(or the nearest within 6° if you just miss), then **jumps** to the nearest enemy within 8 m it
hasn't hit, up to 5 jumps. Damage is 5 to the first, then 75% of the last on each jump. Jumps need
a clear line. No recoil, no brass.

### Rocket Launcher (special)

Quake 1's own rocket launcher. Every shot is a rocket that flies dead straight out of the barrel -
no drop, no bounce - to the first enemy or wall in its way, trailing falling sparks and a dust trail,
then explodes: **110 damage** at the center, falling to a third at **5.2 m**, with a heavy stagger
(x5). The blast reaches nearby players too (shooter, bots and teammates alike), Quake/Doom style.
Semi-auto, one shot every **0.8 seconds**.

### Shrink Beam (special)

A continuous green ray. Hold it on an enemy and it shrinks - smaller and slower by the same share -
the longer you keep the beam on it; let go and, after a second off the beam, it starts growing back,
so a shrink has to finish in one sustained hold. Shrink it below **25%** of its size and it pops
outright. Once it's under **40%** size, just getting within **0.6 m** of it pops it too - small
enough that walking near it counts as stepping on it.

### Victory Banner

You carry the flag itself. Press fire and it's **planted right in front of you** at once - a pole
with a green banner waving from it - no charging up. Like a sentry, you **pick it back up** by walking up to it, looking at it and pressing **Use** (a
prompt shows) - planting switches you to your main weapon meanwhile. Switching your tool slot away from it - PageUp/PageDown, equipping something else, or
pressing Home to restore your starting loadout - **retires it on its own**, no need to pick it up
first.

While it's up, a **glowing green aura** lies on the ground around it out to **8 m**. Every quarter
second it pulses, **healing** everyone standing inside it 1% of their max health - stay in it and
you're back to full well within a minute. That's all it does.

## Swapping weapons in a level

Hold a main, special, tool or melee weapon and press **Page Up** for the next of this mod's weapons
for that slot, or **Page Down** for the previous one. A five-second message names what you switched
to. Only this mod's weapons are in the cycle, never vanilla gear. Teammates get the swap too.

Press **Home** to go back to a serious run: every slot you've swapped this level - main, special,
tool and melee, all at once - goes back to the gear you started with, with the magazines and
reserve ammo you had the moment you first swapped it away. Slots you never swapped aren't touched.
The keys are configurable (below).

## Config

`BepInEx/config/MindAttic.GTFO.AbsurdWeapons.cfg` is created on first launch.

One section per melee weapon:

| Setting | Default | What it does |
|---|---|---|
| `BladeLength` | Katana 6.9, Warhammer 3, others 1 | How much the blade (or hammer head) is stretched; 1 is the model's own size. Re-read whenever the weapon is rebuilt (loadout screen, re-equip), so you can tune it without restarting. |
| `BladeLengthAxis` | Y (Warhammer: All) | Which of the model's axes is "length". All scales it evenly, so a head gets bigger instead of longer. |
| `Reach` | 0 (built-in: Katana 3.5, Warhammer 2.4) | How far hits land, in meters. Takes effect on restart. |

`[Gear Cycling]`:

| Setting | Default | What it does |
|---|---|---|
| `NextWeaponKey` | PageUp | Swap to the next of this mod's weapons for the slot you're holding. |
| `PreviousWeaponKey` | PageDown | Swap to the previous one. |
| `RestoreLoadoutKey` | Home | Put back every swapped slot, ammo included, as it was before its first swap. |

`[Tentacles]`:

| Setting | Default | What it does |
|---|---|---|
| `PoolMultiplier` | 4 | How many times more tentacles the game can draw at once, for every tentacle type (enemies' included). Takes effect on restart. |

## Multiplayer

Everyone in the lobby needs this mod. Weapons sync between players by an id that only exists for
players who have it installed. The player who fires deals the damage through the normal bullet
path, so health, stagger and kills stay in sync. Teammates are sent every shot, beam, tentacle,
bolt, grenade, flash and stealth, and see the same thing. Knockback is done by the host, who owns enemy positions.

## Sounds

Every weapon sounds like what it is: no gunshots from the lightning or laser guns (a crackling electric
loop and a continuous beam hum instead), a gauss crack from the Railgun, a hollow thump from the grenade guns,
tentacle lashes and bites, thunder from Mjolnir. Every one is the game's own
sound, and each can be swapped for any other game sound by name in the config (`[Sounds]`).

## Compatibility

- **GTFO.EZ:** compatible, in either load order. Weapons are built after EZ's tuning, so each
  starts from EZ's version of the weapon it's copied from. Stats this mod sets itself stay fixed.
- **Other mods that add weapons** at the same ids (1001-1010 melee, 1101-1119 guns, 1202 tool, 1203 consumable): if
  one already uses an id, that weapon isn't added and `LogOutput.log` says so. The rest still load.
  The mod also adds melee animation sets 1006-1010, recoil profiles 1101-1109 and 1119, and first-person hold
  1103; a clash there drops that weapon (animation sets) or leaves it its model's recoil.

## Requirements

BepInExPack_GTFO, which includes GTFO-API (used to send shots and effects to teammates).

## Changelog

See `CHANGELOG.md`.
