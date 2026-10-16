# GTFO.Weapons

Dev repo for **GTFO.Weapons**, a BepInEx IL2CPP plugin for GTFO that adds thirteen new weapons: five
melee weapons that put one weapon's model on another's moves (Glaive, Katana, Mjolnir, Warhammer,
Shadow Dagger), and eight guns (Plasma, Hive and Laser Pistol/Rifle, Tentacle Cannon). See
`package/README.md` for the player-facing breakdown and stat tables.

Not yet published on Thunderstore (the package name will be `GtfoWeapons`).

Like GTFO.EZ, this ships no copies of the game's data files. It patches `GameDataInit.Initialize`
and registers new datablocks cloned from the game's own loaded ones at runtime, so the weapons
inherit vanilla balance changes across patches instead of going stale. Nothing vanilla is edited.

## How it works

### Datablocks (`MeleeInjector`, `GunInjector`)

Each weapon is three cloned blocks (archetype, gear category, offline gear) at a fixed id. Every
block for a weapon uses that weapon's id. The offline gear's JSON is rewritten to point at the new
category and renamed. The model's own gear entry also decides the inventory slot.

| Weapon | Id | Archetype from | Category from | Model (gear) from | Changes |
|---|---|---|---|---|---|
| Glaive | 1001 | `Spear` | `MeleeSpear` | `Spear_02` (IsoCo Stinger) | hammer's animation/SFX sets, damage, stagger, hit width, third-person movement; spear's reach and cleave; hold-thrust off |
| Katana | 1002 | `Bat` | `MeleeBat` | `Knife` (Mastaba Fixed Blade) | reach 3.2 (bat 1.75) to match the 7.5x-stretched blade; the dash is runtime (`KatanaDash`) |
| Mjolnir | 1003 | `Bat` | `MeleeBat` | `Mallet` (Santonian Mallet) | none beyond id and name; the thunderclap is runtime (`ImpactBursts`) |
| ~~Rapier~~ | 1004 | - | - | - | removed (it never worked); the id stays retired so old loadouts can't pick up a different weapon |
| Warhammer | 1005 | `Hammer` | `MeleeSledgehammer` | `Sledgehammer` (Kovac) | hammer's own animation set and charge; charged 500 / light 6, stagger x55 charged / x11 light, environment x10, hit width 0.6, reach 2.4, cleaves, half speed charging; the shockwave and knockback are runtime (`ImpactBursts`, `Knockback`) |
| Shadow Dagger | 1006 | `Knife` | `MeleeKnife` | `Knife` (Mastaba Fixed Blade) | none beyond id and name; the stealth is runtime (`Stealth`) |
| Plasma Pistol | 1101 | the Steigro's archetype for its fire mode | the Steigro's (`Pistol_Stagger`) | `Autopistol staggering` (Raptus Steigro, main, no sight) | `AddPlasma`: `FireMode` Auto, damage 22.5 (display; rolled per hit), shot delay 3/35 s, clip 100, own kickless `RecoilDataBlock` 1101 |
| Hive Pistol | 1102 | the S49's archetype for its fire mode | the S49's (`Pistol`) | `PistolSemi` (Shelling S49, main, no sight) | `AddHive`: `FireMode` Auto, damage 1, shot delay 0.12 s, clip 30, own kickless `RecoilDataBlock` 1102 |
| Hive Rifle | 1104 | the HXC's archetype for its fire mode | the HXC's (`RifleHeavy`) | `RifleHeavyAuto` (Malatack HXC, special) | `AddHive`: damage 1, shot delay 0.09 s, kickless recoil 1104 |
| Plasma Rifle | 1105 | the LRG's archetype for its fire mode | the LRG's (`Gauss_Rifle`) | `HELRifleSemi` (Omneco LRG, special) | `AddPlasma`: shot delay 2/35 s, kickless recoil 1105 |
| Tentacle Cannon | 1103 | the XDist2's archetype for its fire mode | the XDist2's | `ShotgunRanged` (Buckland XDist2, special, iron sights) | `FireMode` Auto, 1 pellet, no cone or spread, damage 10 (per tentacle), shot delay 0.235 s, `ShellCasingSize` 0, clip 60, no spin-up, own kickless `RecoilDataBlock` 1103, own first-person hold `ItemFPSSettingsDataBlock` 1103 (`AddFarLowHold`: vanilla `Shotgun` pushed lower and further out, aim FOV 30); the lashes are runtime (`TentacleCannon`) |
| Laser Pistol | 1106 | the Treffen 2's archetype for its fire mode | the Treffen 2's | `Pistolauto` (Raptus Treffen 2, main, no sight) | `AddLaser`: `FireMode` Auto, damage 1.2 per tick, shot delay 0.05 s (20 ticks/s), clip 100, no spread, `ShellCasingSize` 0, kickless recoil 1106, hold swapped to `ItemFPSSettings` 48 (`Pistol_Chunky`, 10 cm lower) so the beam shows; the beam is runtime (`LaserGuns`) |
| Laser Rifle | 1107 | the DEL P1's archetype for its fire mode | the DEL P1's | `Precision_Rifle` (Drekker DEL P1, special) | `AddLaser`: damage 2 per tick, kickless recoil 1107, model's own hold |

Ids are fixed (not "next free") so every player in a lobby agrees on them. If another mod already
uses an id, that weapon isn't added and an error is logged. The rest still load. Vanilla names,
ids and fields came from a build-34855 datablock dump (github.com/igobyjama/devile,
`Devile/BepInEx/GameData-Dump/34855`).

Both injectors are `[HarmonyAfter("MindAttic.GTFO.EZ")]`, so with GTFO.EZ installed they clone
its already-tuned vanilla blocks, and EZ (keyed on vanilla names/ids) never re-tunes ours.

### Blade stretching (`BladeStretcher`, `WeaponSettings`)

A postfix on `GearPartHolder.OnAllPartsSpawned` runs for every model the game builds: loadout
preview, first person, and teammates' third person. For our categories it scales `MeleeHeadPart`
(the blade) along its length axis, Y by default (all of GTFO's melee parts run along Y; Auto picks
the longest mesh dimension instead). It then shifts the part
so the end nearest `MeleeHandlePart` stays put.
- A one-piece model with no head part (such as the Wox Compact knife) is stretched whole via its handle part,
  pinned at `m_aRightHand` so the grip stays in the hand.
- `BladeLengthAxis = All` scales evenly, anchored at the point nearest the shaft. That gives the
  Warhammer its giant head.

Original scale and position are cached per instance, so re-applying never compounds. Each build
logs the length in meters before and after, for tuning.

### Seeker guns (`SeekerGuns`, `SeekerProjectile`)

The approach follows Dinorush's ExtraWeaponCustomization (EWC). Vanilla `ProjectileTargeting`
homes onto and damages players only, so it can't be used as-is.
- **Firing:** `BulletWeapon.Fire`/`Shotgun.Fire` prefixes record which archetype is firing. A
  prefix on `Weapon.CastWeaponRay` (4-arg overload) swallows our guns' hitscan rays (`maxRayDist =
  0`, return false), so ammo, recoil, muzzle flash and sound stay vanilla. Plasma guns set `ShellCasingSize` 0, so no brass. Each ray, one per
  shotgun pellet, becomes a seeker with the pellet's spread applied.
- **Projectile:** `ProjectileManager.SpawnProjectileType` spawns the real enemy projectile for its
  model, glow and trail. Its `ProjectileBase` is fired with no target, silenced and disabled.
  `SeekerProjectile`, an injected MonoBehaviour, then flies it: it locks onto the enemy closest to
  the line of flight (`OverlapSphere` on `MASK_ENEMY_DAMAGABLE`, cone check, `Linecast` against
  `MASK_WORLD`), turns toward it at a capped rate, and sphere-casts each step against
  `MASK_ENEMY_DAMAGABLE | MASK_PROJECTILE_BLOCKERS`.
- **Kinds** (`SeekerKind`, tuned for fun, not balance yet; `ForGun` maps both pistol and rifle of a
  type to its kind):
  - **Plasma** is Doom's plasma gun on a `TargetingSmall` (the Shooter's) tinted blue (`Tint`: its
    light, trail, particle start colors and material color slots). `Homing = false`, so it flies
    straight at 28.6 m/s (25 Doom units/tic). Each hit rolls 5 x 1d8 (`DamageDie`/`DamageScale`),
    and every impact adds a 1.25 m `AreaDamage` shock like Mjolnir's (6 falling to 40%, stagger x3,
    a small 2 m blue flash). The tint sets every `Color` property of every material on the
    projectile, and logs the first one's renderers/shaders/color slots once for diagnosis.
  - **Hive** is Titanfall's Smart Pistol on a `SemiTargetingQuick` (the Hybrid's). `SmartLock`: at
    the trigger, `NextLock` gathers living, visible enemies within 35° of the crosshair and 60 m,
    nearest the crosshair first, and successive shots cycle through up to 6. The round spawns at
    the weapon's `MuzzleAlign` and flies a quadratic Bezier (muzzle -> a control point 40% of the
    way along the aim -> the target's `AimTarget`, re-read every frame) at a constant 45 m/s. With
    no lock, or once its target dies, it homes normally (1500°/s, 120° search cone, 50 m).
  - `Shard` (split seekers) is still defined but nothing uses it right now.
  - Every impact flashes a small `EffectLight`. Teammates' visual copies fly and flash on their
    own; a smart round's message carries its target's position, which the copy curves to.
- **Infinite ammo:** the `Fire` postfixes top the clip back up (`SetCurrentClip(ClipSize)`) after
  every shot from one of our guns, for the local player.
- **Damage:** only the shooter's copy deals damage, through `Dam_EnemyDamageLimb.BulletDamage`. That
  is the normal bullet path, so it syncs host and client.
- **Multiplayer:** each shot is sent to teammates with GTFO-API's `NetworkAPI`
  (`MindAttic.GTFO.Weapons.SeekerShot`). They spawn a visual-only copy that deals no damage.
  GTFO-API ships inside BepInExPack_GTFO, so it isn't a separate dependency.

### Tentacle Cannon (`TentacleCannon`, `TentacleLash`)

- **Firing:** `SeekerGuns`' `CastWeaponRay` prefix swallows the Tentacle Cannon's rays too, one per
  round. Shotgun pellet hit data has no `owner`, so the shooter falls back to the firing weapon's
  `Owner`. For the local shooter each ray calls `TentacleCannon.FirePellet`, which turns the aim into
  that tentacle's own line with `Fan` (random yaw up to 30°, pitch up to 5°: a wide, level fan). It
  picks an enemy with `SeekerGuns.FindLocks` around that line (15°, 25 m, line of sight) that no
  other tentacle fired the same frame has claimed. With none, `SomethingToGrab` casts 10 probes in a
  flat fan (35° sideways, 8° up/down, 12 m, `MASK_BULLETWEAPON_RAY`) and latches onto the nearest
  hit; with nothing in reach it flails 15 m out along its line.
- **Start point (`BarrelOnScreen`):** the first-person gun is drawn by `FPSCamera.m_itemCamera` with
  its own FOV, so the muzzle's world position doesn't match the barrel on screen. The lash starts
  where the item camera shows the muzzle (5 cm behind it), re-projected through the world camera at
  the same depth, every frame.
- **The tentacle:** `GPUCurvyManager.TryAllocateGPUCurvy(GPUCurvyType.Striker)` borrows a striker
  tentacle from the game's GPU tentacle pool, so no enemy or prefab is needed. `TentacleLash` is an
  injected MonoBehaviour. Every frame it lays the control points (`GPUC_ControlPoint[]`, the
  curvy's `m_pointCount`) along a snake from the barrel to the target's `AimTarget`: a big
  side-to-side wave and a small vertical one in a plane within 20° of level, both zero at both ends,
  rippling at their own random speeds and pulsing in strength, plus a slight random bow. It forces
  `m_isVisible` (an enemy's tentacle has its owner do that), feeds the points with
  `FeedControlPositions`, then `FeedSettings(extend, glow 1)` and `CalculateEndPointsAndUpdateGPU`.
  It shoots out in 0.04 s, hangs 0.5 s thrashing and retracts over 0.2 s, then `DeallocateGPUCurvy`.
- **Damage:** the bite lands at full extension: `BulletDamage(10, stagger x5)` on the target's
  limb nearest the tip, on the shooter's game only.
- **Multiplayer:** teammates get `MindAttic.GTFO.Weapons.TentacleLash` (start and end) per tentacle
  and draw visual-only lashes.
- **Pool size (`TentaclePool`):** each tentacle type's GPU pool is a fixed size
  (`GPUC_Setup.m_globalLimit`), and its compute buffers are built from it in `GPUC_Setup.Setup`. A
  prefix there multiplies it by `[Tentacles] PoolMultiplier` (default 4) for every type, so the
  cannon's volleys and enemy tentacles stop competing for slots. The log lists each type's
  before and after.

### Laser guns (`LaserGuns`, `LaserBeam`)

- **Firing:** the `CastWeaponRay` prefix swallows the lasers' rays too. Each one, 20 a second while
  the trigger is held, is a damage tick for the local shooter: `LaserGuns.Tick` raycasts from the
  eye (`MASK_BULLETWEAPON_RAY`, 40 / 70 m) and calls `BulletDamage` on any enemy limb it hits.
- **One beam:** each player has one `LaserBeam` (an injected MonoBehaviour with a `LineRenderer`
  using the trip mine's laser material). Ticks keep it lit; it goes off 0.15 s after they stop. The
  shooter's own beam re-traces from `FPSCamera` every frame, so it stays on the gun and crosshair
  between ticks. It starts low and right of the eye, where the gun sits on screen.
- **No tracer:** `BulletWeapon.Fire`'s prefix adds a laser gun's `m_vfxTracer` (`FeedbackPlayer`)
  to a muted set, and a prefix on `FeedbackPlayer.DoPlay` skips it, so ticks don't streak vanilla
  tracers down the beam.
- **Burn:** where the beam lands, a second short `LineRenderer` is a flickering white-hot spot, an
  `EffectLight` flickers orange, and every 0.06 s `FX_Manager.PlayEffect` plays `Impact_Metal`
  (sparks; a decal every 6th) and on enemies, every third, `Impact_EnemyBody`.
- **Multiplayer:** each tick is sent as `MindAttic.GTFO.Weapons.LaserBeam` (start, end, hit kind,
  surface normal); teammates draw the same beam and burn.

### Katana dash (`KatanaDash`, `DashRunner`)

A postfix on `MeleeWeaponFirstPerson.ChangeState` times the local player's Katana charge (from a
charge-up state to a charge-release state). If it was held at least 95% of the animation set's
`MaxDamageChargeTime`, `DashRunner` (an injected MonoBehaviour) moves the player 40 m along the aim,
flattened, over 0.6 s through `PlayerCharacterController.Move`, so collision stops it and position
syncs like any movement. Each frame it sweeps an `OverlapCapsule` (1.2 m) from the last position to
the new one, so nothing is skipped at ~67 m/s, and slashes each enemy once per dash with the
archetype's `ChargedAttackDamage` and x3 stagger through `BulletDamage`.

### Hammer impact bursts (`ImpactBursts`)

A postfix on `MeleeWeaponFirstPerson.DoAttackDamage` runs only for the local player's hits, not
pushes. Each `ImpactBurst` record says which weapon it belongs to, when it fires, and what it does:

| Burst | Weapon | Fires on | Radius | Damage | Stagger multi | Struck enemy |
|---|---|---|---|---|---|---|
| Thunderclap | Mjolnir | charged hits only (`CurrentStateName` is a charge release/hit) | 5 m | 8, falling to 40% at the edge | x3 | spared (it took the hammer) |
| Shockwave | Warhammer | any hit that lands | 6 m | 1 | x100 | included |

It finds every living enemy in the radius around `data.hitPos` (`OverlapSphere` on
`MASK_ENEMY_DAMAGABLE`, one nearest limb per enemy, line of sight against `MASK_WORLD`). Each one
gets `BulletDamage`, the bullet path, so it syncs. Stagger is damage x stagger multiplier, so the
shockwave's 1 x 100 knocks back anything that isn't stagger-immune. The flash is a fading
`EffectLight` (`BurstFlash`, an injected MonoBehaviour), sent to teammates over
`MindAttic.GTFO.Weapons.ImpactBurst`.

**Knockback (`Knockback`, `KnockbackSlides`):** the Warhammer's bursts also shove enemies: the
struck one 1.5 m along the swing (4 m charged), the rest of the burst 2 m out from the impact. The
slide eases out over a short time and is clipped by `NavMesh.Raycast`, so it stops at walls and
ledges. Enemy positions belong to the host: a client sends `MindAttic.GTFO.Weapons.Knockback`
(position, direction, distance) and the host finds the enemy there and slides it; the game's own
position sync shows it to everyone.

### Shadow Dagger stealth (`Stealth`, `StealthVisuals`)

- **Fast stabs:** a postfix on the `MeleeWeaponFirstPerson.MeleeSpeedModifier` getter multiplies
  it by 2.5 for the Shadow Dagger while `CurrentStateName` is a light attack (`AttackMiss*` /
  `AttackHit*`). The charge-up keeps its normal pace.
- **Trigger:** a postfix on `MeleeWeaponFirstPerson.DoAttackDamage` fires when the local player's
  Shadow Dagger lands a fully charged hit (not a push) on something with an `EnemyAgent` parent.
  Fully charged means the state is a charge release/hit and `m_damageToDeal` is at least 95% of
  the archetype's `ChargedAttackDamage`; a partial charge logs its numbers and is ignored. It hides
  that player for 6 s, keyed by their `SNet_Player.Lookup`. A further full-charge hit restarts the
  6 s.
- **Sync:** the hit is sent over GTFO-API (`MindAttic.GTFO.Weapons.Stealth`); the sender is the
  hidden player. Every game keeps the same `hiddenUntil` table. That matters most on the host,
  where enemy AI runs.
- **Enemies ignore hidden players:**
  - `EnemyDetection.IsTargetValid` returns false for them, so enemies drop them as a target.
  - Prefixes on `EnemyAI.SetDetectedAgent` (sight and noise), `EnemyAI.SetTarget` and
    `EnemyAI.InjectPropagatedTarget` (alerts from other enemies and noise) skip them.
- **Looks:** `StealthVisuals`, an injected MonoBehaviour added from `Plugin.Load`, runs on every
  game from the shared hidden table, so everyone sees the same thing without extra messages: a
  shimmer. Each renderer that was on at the start flickers 10 times a second with its own random
  offset, showing for 35% of each flicker, so the figure breaks up instead of blinking as a whole.
  Over the last second that share climbs to 100%, as the warning. It covers a teammate's whole
  body, and for the local player everything under `FPItemHolder` (arms and the dagger). Everything
  is turned back on at the end.

## Layout

```
src/GTFO.Weapons/       the plugin project (BepInEx/Il2Cpp - needs the game installed to build)
  Plugin.cs             entry point: config, Il2Cpp type registration, network event, Harmony
  DataBlocks.cs         shared clone / lookup / gear-JSON rewrite helpers
  MeleeInjector.cs      Glaive, Katana, Mjolnir, Warhammer, Shadow Dagger datablocks
  GunInjector.cs        Plasma/Hive/Laser pistol and rifle, Tentacle Cannon datablocks, holds, kickless recoil
  BladeStretcher.cs     blade / head scaling on model spawn
  WeaponSettings.cs     per-weapon config (BladeLength, BladeLengthAxis, Reach)
  SeekerGuns.cs         fire hooks, seeker kinds, network message
  SeekerProjectile.cs   the player-fired homing projectile
  ShadowDagger.cs       Shadow Dagger fast stabs, stealth (enemy AI patches, sync) and its shimmer
  TentacleCannon.cs     Tentacle Cannon fan, surface grab and the GPU tentacle lash that draws them
  LaserGuns.cs          laser beam ticks, the persistent beam, its burn and the muted tracer
  KatanaDash.cs         the Katana's full-charge dash-slash
  Knockback.cs          host-side physical knockback slides (Warhammer)
  TentaclePool.cs       raises every tentacle type's GPU pool size (config PoolMultiplier)
  ImpactBursts.cs       hammer impact bursts (Mjolnir thunderclap, Warhammer shockwave) and their flash
package/                exactly what ships to Thunderstore / gets zipped for sharing
  manifest.json         Thunderstore package name is "GtfoWeapons" (no period allowed there)
  README.md             becomes the Thunderstore page
  CHANGELOG.md
  icon.png
docs/USER_STORIES.md    generated from the workflow database - change it with the workflow CLI
build.ps1               builds the plugin and packages dist/GTFO.Weapons-<version>.zip
```

## Building

Same setup as GTFO.EZ. You need BepInExPack_GTFO installed and launched at least once ("Modded"
start) in a Thunderstore Mod Manager or r2modman profile.

```powershell
.\build.ps1                      # uses the Thunderstore Mod Manager profile "Easy Mode DEV"
.\build.ps1 -Profile MyProfile
```

If you're on the older r2modman app instead of Thunderstore Mod Manager, its profiles live under a
different path. Pass it directly instead:
```powershell
dotnet build .\src\GTFO.Weapons\GTFO.Weapons.csproj -p:BepInEx="$env:AppData\r2modmanPlus-local\GTFO\profiles\Default\BepInEx"
```

This builds straight into that profile's `BepInEx/plugins/GTFO.Weapons/` folder, so you can launch
the game and test immediately. It also produces `dist/GTFO.Weapons-<version>.zip` for sharing or
upload.

## Testing

There are no unit tests. Everything here runs against the game's loaded data, Unity objects or
Il2Cpp types, so verify by launching the game after `build.ps1`. `BepInEx/LogOutput.log` should
show one "added the ..." line per weapon and, while you look at the melee weapons, the blade
length lines.

Still to verify in-game:
- All thirteen weapons appear in the loadout screen in the right slots. If not, their categories may
  also need adding to `GearCategoryFilterDataBlock` (14 "MeleeWeapons" for melee).
- How each model sits in the hand under its borrowed animations.
- `BladeLength`:
  - The Y default lengthens every blade (confirmed for the Katana).
  - Blades grow away from the grip, not back through the hand.
  - A one-piece model (none in use now) would stay in the hand.
  - The Warhammer head grows outward from the shaft.
  - Teammates see the stretch too.
- Melee hits line up with the visible blade at each weapon's reach.
- Warhammer: hits send enemies reeling (stagger x55 charged / x11 light).
- Mjolnir: a charged hit (not a tap) shocks nearby enemies, the flash shows for you and your
  teammates, and nothing happens through walls.
- Warhammer: every landed hit staggers everything within 6 m, including the struck enemy; check
  which heavies shrug it off.
- Tentacle Cannon (tentacles draw and bite, confirmed):
  - The level fan reads as wild without going up or down.
  - Lashes start at the barrel on screen with the new hold.
  - Tentacles with no enemy grab nearby surfaces.
  - Teammates see the lashes.
- Shadow Dagger, as host and as a client:
  - Quick stabs are visibly faster and the charge-up isn't.
  - Only a fully charged hit hides you (the log notes partial charges): enemies stop chasing you
    for 6 s, including ones already on you.
  - Sleeping enemies and noise don't give you away while hidden.
  - The 10-a-second shimmer shows for you and for teammates and fills back in over the last
    second.
  - Everything comes back when the 6 s run out.
- Katana: hits land at the tip of the ~2.2 m blade (3.2 m reach). The full-charge dash (confirmed
  working at 4 m) now goes 40 m in 0.6 s: check it feels right and slashes everything it passes.
- Warhammer knockback: enemies slide and stop at walls and ledges, as host and as client.
- Lasers: one steady beam while held (no per-tick tracer streaks), sparks and glow where it lands,
  no brass; teammates see the beam and burn.
- Seeker guns:
  - The disabled enemy projectile stays quiet (no player homing, sound or self-destruct).
  - Seekers hit enemies, are blocked by walls and doors, and never hurt players.
  - Damage registers when fired by the host and when fired by a client.
  - Teammates see the shots.
  - The Plasma Pistol fires ~11.7 blue balls a second (Rifle ~17.5), straight, with no recoil; the blue tint takes
    on the whole projectile (glow, trail, particle core).
  - Hive Pistol/Rifle rounds leave the muzzle (not the camera) and curve onto locked enemies, cycling
    between them.
  - Neither gun ever reloads or empties.
- Performance with many impact flashes at once.

## Config

`BepInEx/config/MindAttic.GTFO.Weapons.cfg` has one section per melee weapon: `BladeLength`,
`BladeLengthAxis`, `Reach`. `BladeLength` is re-read whenever a model is built, so it can be tuned
without restarting. `Reach` overrides the archetype's `CameraDamageRayLength` at injection, so it
needs a restart. The gun stats are fixed in `GunInjector`.

## Versioning

v1, v2, v3... — major-only bumps, matching the convention across other MindAttic projects.
Thunderstore requires `manifest.json`'s `version_number` to be strict `x.y.z`, so "v1" is `1.0.0`,
"v2" is `2.0.0`, and so on (minor/patch stay 0). `package/CHANGELOG.md` headers use the short `vN`
form.

## Sharing with friends

- **Private:** Import the zip from `dist/` as a local mod in your mod manager, then
  `Settings -> Profile -> Export profile as code` and send friends the code.
- **Public:** not yet on Thunderstore.
- Everyone in a lobby needs the identical mod version; weapons sync by id.
