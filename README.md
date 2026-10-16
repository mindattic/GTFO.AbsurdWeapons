# GTFO.AbsurdWeapons

Dev repo for **GTFO.AbsurdWeapons**, a BepInEx IL2CPP plugin for GTFO that adds twenty-three new weapons, each shown with "(!)"
after its name: nine melee weapons that put one weapon's model on another's moves (Yari, Tanto,
Wakizashi, Katana, Odachi, Mjolnir, Warhammer, Gravity Hammer, Shadow Dagger), twelve guns (Plasma,
Hive, Laser and Tentacle Pistol/Rifle, Railgun, Chain Lightning Gun, Flamethrower, Flak Cannon) and two tools (Grenade Launcher, Victory Banner).
Their names get the marker from `DataBlocks.DisplayName`; block names stay unmarked. See
`package/README.md` for the player-facing breakdown and stat tables.

Not yet published on Thunderstore (the package name will be `GtfoAbsurdWeapons`).

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
| Yari (was Glaive) | 1001 | `Spear` | `MeleeSpear` | `Spear_02` (IsoCo Stinger) | hammer's animation/SFX sets, damage, stagger, hit width, third-person movement; spear's reach and cleave; hold-thrust off |
| Katana | 1002 | `Bat` | `MeleeBat` | `Knife` (Mastaba Fixed Blade, blade x2.6 = 70 cm) | light 4 / charged 14, reach 2.3, cleaves; the dash is runtime (`KatanaDash`) |
| Mjolnir | 1003 | `Bat` | `MeleeBat` | `Mallet` (Santonian Mallet) | none beyond id and name; the thunderclap is runtime (`ImpactBursts`) |
| ~~Rapier~~ | 1004 | - | - | - | removed (it never worked); the id stays retired so old loadouts can't pick up a different weapon |
| Warhammer | 1005 | `Hammer` | `MeleeSledgehammer` | `Sledgehammer` (Kovac) | hammer's own animation set and charge; charged 500 / light 6, stagger x55 charged / x11 light, environment x10, hit width 0.6, reach 2.4, cleaves, half speed charging; the shockwave and knockback are runtime (`ImpactBursts`, `Knockback`) |
| Shadow Dagger | 1006 | `Knife` | `MeleeKnife` | `Knife` (Mastaba Fixed Blade) | none beyond id and name; the stealth is runtime (`Stealth`) |
| Wakizashi | 1007 | `Knife` | `MeleeKnife` | `Knife` (blade x1.87 = 50 cm) | light 3 / charged 9, reach 2.0; light attacks x1.25 (`SwordTempo`) |
| Odachi | 1008 | `Hammer` | `MeleeSledgehammer` | `Knife` (blade x8.4 = 2.26 m) | light 6 / charged 9999, reach 3.5, sphere 0.4, cleaves, 70% speed charging; light attacks x0.85 |
| Tanto | 1009 | `Knife` | `MeleeKnife` | `Knife` (blade x0.93 = 25 cm) | light 2 / charged 6, reach 1.6; light attacks x1.6 |
| Gravity Hammer | 1010 | `Hammer`, on its own `MeleeAnimationSetDataBlock` 1010 (the hammer's with `AutoAttackTime` pushed out of reach) | `MeleeSledgehammer` | `Maul` (Omneco Maul) | light 4 / charged 30, charged stagger x20, sphere 0.5, reach 2.2, cleaves, 60% speed charging; the pull and blast are runtime (`GravityHammer`) |
| Plasma Pistol | 1101 | the LRG's archetype (shared with the Plasma Rifle) | the Steigro's (`Pistol_Stagger`) | `Autopistol staggering` (Raptus Steigro, main, no sight) | `AddPlasma`: `FireMode` Auto, damage 22.5 (display; rolled per hit), shot delay 2/35 s, clip 100, own kickless `RecoilDataBlock` 1101 |
| Hive Pistol | 1102 | the HXC's archetype (shared with the Hive Rifle) | the S49's (`Pistol`) | `PistolSemi` (Shelling S49, main, no sight) | `AddHive`: `FireMode` Auto, damage 1, shot delay 0.09 s, clip 30, own kickless `RecoilDataBlock` 1102 |
| Hive Rifle | 1104 | the HXC's archetype | the HXC's (`RifleHeavy`) | `RifleHeavyAuto` (Malatack HXC, special) | `AddHive`: same stats as the Hive Pistol, kickless recoil 1104 |
| Plasma Rifle | 1105 | the LRG's archetype | the LRG's (`Gauss_Rifle`) | `HELRifleSemi` (Omneco LRG, special) | `AddPlasma`: same stats as the Plasma Pistol, kickless recoil 1105 |
| Tentacle Rifle (was Tentacle Cannon) | 1103 | the XDist2's archetype (shared with the Tentacle Pistol) | the XDist2's | `ShotgunRanged` (Buckland XDist2, special, iron sights) | `AddTentacle`: `FireMode` Auto, 1 pellet, no cone or spread, damage 11 (per tentacle), shot delay 0.235 s, `ShellCasingSize` 0, clip 60, no spin-up, own kickless `RecoilDataBlock` 1103, own first-person hold `ItemFPSSettingsDataBlock` 1103 (`AddFarLowHold`: vanilla `Shotgun` pushed lower and further out, aim FOV 30); the lashes are runtime (`TentacleCannon`) |
| Laser Pistol | 1106 | the DEL P1's archetype (shared with the Laser Rifle) | the Treffen 2's | `Pistolauto` (Raptus Treffen 2, main, no sight) | `AddLaser`: `FireMode` Auto, damage 2 per tick, beam 70 m, shot delay 0.05 s (20 ticks/s), clip 100, no spread, `ShellCasingSize` 0, kickless recoil 1106, hold swapped to `ItemFPSSettings` 48 (`Pistol_Chunky`, 10 cm lower) so the beam shows; the beam is runtime (`LaserGuns`) |
| Laser Rifle | 1107 | the DEL P1's archetype | the DEL P1's | `Precision_Rifle` (Drekker DEL P1, special) | `AddLaser`: same stats as the Laser Pistol, kickless recoil 1107, model's own hold |
| Railgun | 1108 | the PR 11's archetype for its fire mode | the PR 11's (`Sniper`) | `SniperSemi` (Koning PR 11, special) | `AddRailgun`: Semi, damage 60 (to every enemy on the line), shot delay 1.0 s, clip 5, no spread, `ShellCasingSize` 0, the sniper's own recoil and scope; the slug is runtime (`Railgun`) |
| Chain Lightning Gun | 1109 | the exp1's archetype for its fire mode | the exp1's (`MachineGun_LowRPM`) | `HELGun` (Omneco exp1, special) | `AddChainLightning`: Auto, damage 10 (first strike), shot delay 0.15 s, clip 60, no spread or spin-up, no brass, kickless recoil 1109; the bolts are runtime (`ChainLightning`) |
| Flamethrower | 1111 | the Veruta XII's archetype for its fire mode | the Veruta XII's (`MachinegunAuto`) | `MachinegunAuto` (TechMan Veruta XII, special) | `AddFlamethrower`: Auto, damage 6 (per tick, before falloff), shot delay 1/15 s, clip 150, no spread, no brass, kickless recoil 1111; the cone, afterburn, fire spread and fuel bomb are runtime (`FlameThrower`) |
| Flak Cannon | 1112 | the J 300's archetype for its fire mode | the J 300's (`ShotgunHEL`) | `ShotgunHEL` (Bataldo J 300, special) | `AddFlakCannon`: Semi, 9 pellets, cone/spread 5, damage 7 (per shard, before falloff), shot delay 0.85 s, clip 6, no brass, kickless recoil 1112; the bouncing shards and the Aim-held ball are runtime (`FlakCannon`) |
| Tentacle Pistol | 1110 | the XDist2's archetype (shared with the Tentacle Rifle) | the 3RB's (`RevolverStandard`) | `RevolverHEL` (Bataldo 3RB, main) | `AddTentacle`: same stats as the Tentacle Rifle, kickless recoil 1110, model's own hold |
| Grenade Launcher | 1201 | (tools have none) | `GlueGun` | `Glue_distance` (Stalwart Flow G2, tool) | `AddGrenadeLauncher`: category and gear only; the launcher's own prefab drives pressure and bursts; the grenades are runtime (`GrenadeLauncher`) |
| Victory Banner | 1202 | (tools have none) | `GlueGun` | `Glue_distance` (Stalwart Flow G2, tool) | `AddVictoryBanner`: category and gear only; placing, picking up, healing and the damage buff are runtime (`VictoryBanner`) |

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
  every shot from one of our guns, for the local player. `InfiniteAmmo` (a per-frame component)
  also keeps the wielded gun's clip and its slot's reserve full, so a gun swapped in by gear
  cycling starts loaded.
- **Damage:** only the shooter's copy deals damage, through `Dam_EnemyDamageLimb.BulletDamage`. That
  is the normal bullet path, so it syncs host and client.
- **Multiplayer:** each shot is sent to teammates with GTFO-API's `NetworkAPI`
  (`MindAttic.GTFO.AbsurdWeapons.SeekerShot`). They spawn a visual-only copy that deals no damage.
  GTFO-API ships inside BepInExPack_GTFO, so it isn't a separate dependency.

### Tentacle Pistol and Rifle (`TentacleCannon`, `TentacleLash`)

- **Firing:** `SeekerGuns`' `CastWeaponRay` prefix swallows the tentacle guns' rays too, one per
  round. Shotgun pellet hit data has no `owner`, so the shooter falls back to the firing weapon's
  `Owner`. For the local shooter each ray calls `TentacleCannon.FirePellet`, which turns the aim into
  that tentacle's own line with `Fan` (random yaw up to 30°, pitch up to 5°: a wide, level fan). It
  picks an enemy with `SeekerGuns.FindLocks` around that line (15°, `Reach`, line of sight) that no
  other tentacle fired the same frame has claimed; failing that, any enemy within 60° of the aim and `Reach` (nearest the crosshair first, doubling up if all are claimed), so no tentacle wanders to a surface while an enemy is in view. With no enemy at all, `SomethingToGrab` casts 10 probes in a
  flat fan (35° sideways, 8° up/down, `Reach`, `MASK_BULLETWEAPON_RAY`) and latches onto the nearest
  hit; with nothing in reach it flails `Reach` out along its line.
- **Reach:** the strikers' own tentacle length, `EAB_MovingEnemeyTentacle.m_absoluteMaxDistanceToHitTarget`,
  read (longest found) from the level's strikers via `FindObjectsOfType`, retried every 5 s until
  one exists (25 m until then) and logged. A lash whose target moves out of reach is cut to it.
- **Spline ends:** the GPU tentacle's spline runs through the inner control points only (its head
  was measured at 75% of the line with 5 points), so `Draw` puts the second point on the barrel and
  the second-to-last on the target, the outer two extrapolated beyond.
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
- **Damage:** the bite lands at full extension: `BulletDamage(11, stagger x5)` on the target's
  limb nearest the tip, on the shooter's game only.
- **Multiplayer:** teammates get `MindAttic.GTFO.AbsurdWeapons.TentacleLash` (start and end) per tentacle
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
- **Multiplayer:** each tick is sent as `MindAttic.GTFO.AbsurdWeapons.LaserBeam` (start, end, hit kind,
  surface normal); teammates draw the same beam and burn.

### Katana dash (`KatanaDash`, `DashRunner`)

A postfix on `MeleeWeaponFirstPerson.ChangeState` times the local player's Katana charge (from a
charge-up state to a charge-release state). If it was held at least 95% of the animation set's
`MaxDamageChargeTime`, `DashRunner` (an injected MonoBehaviour) moves the player 40 m along the aim,
flattened, over 0.6 s through `PlayerCharacterController.Move`, so collision stops it and position
syncs like any movement. Each frame it sweeps an `OverlapCapsule` (1.2 m) from the last position to
the new one, so nothing is skipped at ~67 m/s, and slashes each enemy once per dash with the
archetype's `ChargedAttackDamage` and x3 stagger through `BulletDamage`.

### Railgun (`Railgun`, `RailBeam`)

The `CastWeaponRay` prefix swallows the Railgun's ray. For the local shooter `Railgun.Fire` runs
`RaycastAll` on `MASK_ENEMY_DAMAGABLE` along the aim out to 200 m - walls don't stop it - sorts the
hits by distance and calls `BulletDamage` once per enemy on the first limb met (so a head is a
head). `RailBeam` draws a core `LineRenderer` the full length and two helix strands around the
first 80 m (0.6 m a turn, 12 points a turn) that spin, widen and fade over 0.8 s, with an
`EffectLight`. `Impact_Metal` sparks play at every wall the line crosses. Teammates get
`MindAttic.GTFO.AbsurdWeapons.RailShot`.

### Chain Lightning Gun (`ChainLightning`, `LightningBolt`)

The `CastWeaponRay` prefix swallows its ray too. `ChainLightning.Fire` takes the enemy the ray hits
(`MASK_BULLETWEAPON_RAY`, 40 m) or, failing that, `SeekerGuns.FindLocks` within 6°. Each strike is
`BulletDamage` (x2 stagger); the next target is the nearest unstruck living enemy within 8 m of the
last strike with a clear `MASK_WORLD` line, up to five jumps at 75% of the previous damage.
`LightningBolt` re-jags a `LineRenderer` through the strike points every frame for 0.16 s, with a
`BurstFlash` and `Impact_Electronics` sparks at each strike. The path (up to 7 points) goes to
teammates as `MindAttic.GTFO.AbsurdWeapons.ChainLightning`.

### Flamethrower (`FlameThrower`, `FlameAfterburn`, `FireBombProjectile`)

The `CastWeaponRay` prefix swallows its ray too, once a tick (15/s). `FlameThrower.Tick` checks
`InputMapper.GetButtonKeyMouse(InputAction.Aim, ...)` for the local shooter: held, it lobs a fuel
bomb instead of streaming (`FireBomb`, rate-limited to one every 1.25 s); otherwise `Stream` fans 5
raycasts across a 12°-either-side cone out to 8 m and calls `BulletDamage` on whatever enemy each
hits, falling off with distance (`Formulas.AreaFalloff`). A landed hit also `Ignite`s that enemy: an
afterburn stack (capped at 5, `Formulas.AfterburnStacks`) that expires 3 s after the last hit.
`FlameAfterburn`, an always-running `MonoBehaviour` (not gated on the trigger), ticks every burning
enemy every 0.5 s for `AfterburnDamagePerStack x stacks` through the bullet path, and each tick also
checks other living enemies within 3 m in sight and ignites them too, so fire spreads through a
cluster. `FireBomb` spawns a `FireBombProjectile` that flies by hand (gravity, a sphere cast each
step) and explodes on the first enemy or wall it touches - no fuse, no bounce - leaving a
`FlameThrower.PoolState` that `FlameAfterburn` also ticks: `AreaDamage.Apply` every 0.5 s for 4 s,
igniting anything it catches. Everything above is the igniting player's own game only (damage
already syncs through the bullet path, same as every other weapon here); the bomb's flight is sent
to teammates (`MindAttic.GTFO.AbsurdWeapons.FlameBombLaunch`) so they see it thrown and explode, but
the afterburn ticks and the fire pool are not synced - a teammate doesn't see the lingering flames.

### Flak Cannon (`FlakCannon`, `ShrapnelShard`, `FlakBall`)

The `CastWeaponRay` prefix swallows each pellet of the shotgun's blast. `FlakCannon.FirePellet`
checks `InputMapper.GetButtonKeyMouse(InputAction.Aim, ...)`: not held, every pellet becomes a
`ShrapnelShard` - a physical shard flown by hand that sphere-casts each step for an enemy
(`BulletDamage`, falling off with distance travelled) or the world (bounce, `ShardBounciness`, up to
2 bounces) and expires after 1.2 s; shards are the shooter's own game only (a shotgun's worth of
bouncing debris, every shot, isn't worth the network traffic). Held, only the shot's first pellet
(`FlakCannon.ResetShot`, called from `SeekerGuns.BeginFire`) spawns a `FlakBall` instead - the rest
are swallowed with no effect - a slower, lightly-lobbed orb that explodes on the first enemy or wall
it touches: one `AreaDamage.Apply` blast plus ten radial raycasts in random directions
(`BallShrapnelRays`) for a burst of secondary shrapnel hits layered over it, catching things just
past the blast's edge. The ball's flight is sent to teammates
(`MindAttic.GTFO.AbsurdWeapons.FlakBallLaunch`); only the shooter's own copy (`owner != null`) deals
damage.

### Grenade Launcher (`GrenadeLauncher`, `LauncherGrenade`, `Explosion`)

- **Firing:** a prefix on `ProjectileManager.WantToFireGlue` checks the owner's wielded item's
  `GearCategoryData`; for the launcher it spawns a grenade instead of glue and returns false. One
  grenade per 0.5 s, so the launcher's burst extras are dropped. A `GlueGun.UpdateLocal` postfix
  keeps the class (tool) ammo full while it's held.
- **Flight:** `LauncherGrenade` flies by hand - gravity, `SphereCast` against
  `MASK_ENEMY_DAMAGABLE` (shooter's game only: explode) and `MASK_WORLD` (reflect, x0.55, settle on
  floors) - so it never touches players. The look is the lock melter's meshes copied from its loaded
  prefab (no scripts), else a capsule. A red `EffectLight` and `LineRenderer` dot blink from ~2 to
  ~16 a second with `DEPLOYER_BEEP_TARGET_ACQUIRED`; bounces play `FRAGGRENADEBOUNCE`.
- **Blast:** at 3 s (or on an enemy) `AreaDamage.Apply` (5 m, 40 falling to 25%, x5 stagger) on the
  shooter's game, the trip mine's explosion effect (`FX_Tripmine` pool), a `BurstFlash` and
  `FRAGGRENADEEXPLODE`. Teammates get launch and detonate messages; their copies only bounce and
  explode when told (or 0.75 s after the fuse).

### Gravity Hammer (`GravityHammer`, `GravityPull`)

A `MeleeWeaponFirstPerson.ChangeState` postfix marks the local player as holding while the Gravity
Hammer is in a charge-up state. `GravityPull` (an injected MonoBehaviour) then, every 0.2 s, pushes
each living enemy within 12 m that it can see 1.2 m toward the player through `Knockback.Push`,
stopping at 2.5 m, and pulses a violet `EffectLight`. A charge release held at least 95% of the
animation set's `MaxDamageChargeTime` calls `ImpactBursts.Release(ImpactBurst.GravityBlast)` 2.5 m in
front: 8 m, 15 damage, x100 stagger, 5 m knockback out, flash sent to teammates.

### Light-attack tempo (`MeleeInjector.WithLightAttackTempo`, `SwordTempo`)

The `MeleeSpeedModifier` getter is never called natively (IL2CPP inlined it), so patching it does
nothing. Instead the Tanto (x1.6), Wakizashi (x1.25), Odachi (x0.85) and Shadow Dagger (x2.5) each
get their own copy of their `MeleeAnimationSetDataBlock` (id = the weapon's id) whose light-attack
`MeleeAttackData` (miss and hit, left and right - copied, never edited in place) has every `*Time`
divided by the speed, and a `ChangeState` postfix sets `WeaponAnimator.speed` to match during
light attacks (1 otherwise). Charge-ups keep their timing, which the full-charge checks use.

### Shadow Dagger kill bonus (`Stealth`)

A `ChangeState` postfix times the local player's dagger windup. A hit while hidden is queued and
checked 0.4 s later (the host confirms kills); a kill adds +25% (max +100%) to a damage bonus that a
`DoAttackDamage` prefix applies to `m_damageToDeal` while hidden, and a full-windup kill extends the
stealth by the windup, sent to everyone. Wielding anything but the dagger ends the stealth.

### In-level weapon swapping (`GearCycler`)

An injected MonoBehaviour reads `[Gear Cycling]` keys (PageUp / PageDown) while
`FocusStateManager.CurrentState` is `FPS`. For the wielded slot (standard, special, class or melee)
it takes `GearManager.GetAllGearForSlot`, keeps the entries whose category is in
`DataBlocks.AddedCategories` (filled as each weapon registers), and equips the next or previous
with `PlayerBackpackManager.EquipLocalGear` - the lobby's own path, which syncs - then re-wields the
slot 0.4 s later and shows a five-second `PUI_WardenIntel` sub-objective message.

**Restore (Home):** before a slot's first swap in a level, `Save` records its `BackpackItem.GearIDRange`,
the gun's `GetCurrentClip()` and the slot's `GetAmmoInPack` reserve. Home re-equips every saved slot
whose category differs (`EquipLocalGear`, so an unchanged tool - a deployed sentry - is left alone),
then `FinishRestore` waits until each item `IsLoaded` (5 s at most) to `SetAmmo` the reserves,
`SetCurrentClip` the magazines and re-wield the slot that was held. Saves clear on leaving `InLevel`.

### Mjolnir's throw (`MjolnirThrow`, `ThrownHammer`)

A `ChangeState` postfix times the local player's Mjolnir charge; a release held at least 95% of
`MaxDamageChargeTime` spawns a `ThrownHammer` - the held weapon's meshes copied onto the default
layer (a primitive stand-in on teammates' games) - flown by hand at 26 m/s along the camera,
spinning about its right axis (end over end), out to 25 m or the first `MASK_WORLD` hit, then home
to the thrower at 32 m/s. On the thrower's game it `SphereCastAll`s (0.6 m) each step for enemies
(20 damage, x4, once each way) and every 0.12 s calls `ChainLightning.Pulse` (nearest visible enemy
within 6 m, then two jumps; the bolt is sent like any chain-lightning bolt). Teammates get
`MindAttic.GTFO.AbsurdWeapons.MjolnirThrow` and fly a visual copy back to the sender's player agent. The
thunderclap (`ImpactBurst.Thunderclap`) now goes off on every landed hit.

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
`MindAttic.GTFO.AbsurdWeapons.ImpactBurst`.

**Knockback (`Knockback`, `KnockbackSlides`):** the Warhammer's bursts also shove enemies: the
struck one 4 m along the swing (12 m charged), the rest of the burst 6 m out from the impact. The
slide eases out over a short time and is clipped by `NavMesh.Raycast`, so it stops at walls and
ledges. **Wall slam:** a burst's `SlamDamagePerMeter` (Warhammer 5, Gravity Blast 3) turns the
distance a slide didn't get to travel when it hit the edge (minus 1 free metre) into damage and a
x3 stagger on the limb nearest the wall, dealt on the host but credited to the player (and weapon) that knocked it back, with an
`Impact_Concrete` puff. Enemy positions belong to the host: a client sends
`MindAttic.GTFO.AbsurdWeapons.Knockback` (the enemy's `pEnemyAgent` network id, direction, distance, slam
damage; its position only as a fallback) and the host slides it; the game's own position sync
shows it to everyone.

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
- **Sync:** the hit is sent over GTFO-API (`MindAttic.GTFO.AbsurdWeapons.Stealth`); the sender is the
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

### Victory Banner (`VictoryBanner`, `BannerPylon`, `VictoryBannerBuff`)

The C-Foam launcher's `WantToFireGlue` hook is reused a third time: it toggles instead of firing a
projectile. Nothing active for the owner: a raycast down from just in front of them places a
`BannerPylon` right there (no throw), sent to teammates (`...BannerPlant`). One already active:
that pull instead retires it (`...BannerPickUp`) - the same message also fires automatically,
from the owner's own game, when `BannerPylon.StillInToolSlot` (checked twice a second, reading
`PlayerBackpackManager.LocalBackpack`'s `GearClass` slot the same way `GearCycler` does) finds the
Victory Banner is no longer what's actually equipped there - PageUp/PageDown, picking something
else, or Home's restore. Every client runs the same planted `BannerPylon` independently: a pulsing
ring every `PulseSeconds` out to `Radius`, and each client's own local player self-heals
(`ReceiveAddHealth`, like Shadow Dagger's per-client decisions) if they're standing in it - no
cross-client authority needed. `VictoryBannerBuff`, an always-on ticker, separately tracks whether
the local player is in *any* active banner's ring; while they are, a `Dam_EnemyDamageLimb.
BulletDamage` prefix multiplies every hit they deal - any weapon, this mod's or vanilla, since
`BulletDamage` is the one choke point all instant damage runs through - by `BaseDamageBuff`,
climbing by `DamageBuffPerKill` per enemy a postfix catches them killing while still inside, capped
at `MaxDamageBuff` and reset to base the moment they leave.

## Layout

```
src/GTFO.AbsurdWeapons/       the plugin project (BepInEx/Il2Cpp - needs the game installed to build)
  Pure/GearJson.cs      gear-JSON reading/rewriting (no game types; unit tested)
  Pure/Formulas.cs      the weapons' numbers (no game types; unit tested)
  Plugin.cs             entry point: config, Il2Cpp type registration, network event, Harmony
  DataBlocks.cs         shared clone / lookup / gear-JSON rewrite helpers
  MeleeInjector.cs      melee datablocks (Yari, swords, hammers, Shadow Dagger), Gravity Hammer animation set
  GunInjector.cs        gun and Grenade Launcher datablocks, holds, kickless recoil
  BladeStretcher.cs     blade / head scaling on model spawn
  WeaponSettings.cs     per-weapon config (BladeLength, BladeLengthAxis, Reach)
  SeekerGuns.cs         fire hooks, seeker kinds, network message
  SeekerProjectile.cs   the player-fired homing projectile
  ShadowDagger.cs       Shadow Dagger fast stabs, stealth (enemy AI patches, sync) and its shimmer
  TentacleCannon.cs     Tentacle Pistol/Rifle fan, targeting, surface grab and the GPU tentacle lash
  Railgun.cs            Railgun slug and its double-helix beam
  ChainLightning.cs     Chain Lightning Gun strikes, jumps and bolt
  FlameThrower.cs       Flamethrower cone, afterburn, fire spread and the fuel bomb
  FlakCannon.cs         Flak Cannon shrapnel shards and the Aim-held ball
  VictoryBanner.cs      Victory Banner placement/pickup, healing and the damage buff
  GrenadeLauncher.cs    Grenade Launcher hook, grenade flight, fuse, blast, infinite tool ammo
  GravityHammer.cs      Gravity Hammer pull and release blast
  SwordTempo.cs         the swords' light-attack speeds
  GearCycler.cs         PageUp/PageDown in-level weapon swapping, Home restores the starting loadout
  LaserGuns.cs          laser beam ticks, the persistent beam, its burn and the muted tracer
  KatanaDash.cs         the Katana's full-charge dash-slash
  Knockback.cs          host-side physical knockback slides (Warhammer)
  TentaclePool.cs       raises every tentacle type's GPU pool size (config PoolMultiplier)
  ImpactBursts.cs       hammer impact bursts (Mjolnir thunderclap, Warhammer shockwave) and their flash
package/                exactly what ships to Thunderstore / gets zipped for sharing
  manifest.json         Thunderstore package name is "GtfoAbsurdWeapons" (no period allowed there)
  README.md             becomes the Thunderstore page
  CHANGELOG.md
  icon.png
tests/GTFO.AbsurdWeapons.Tests/  NUnit tests for Pure/ - plain `dotnet test`, no game needed
docs/USER_STORIES.md    generated from the workflow database - change it with the workflow CLI
build.ps1               builds the plugin and packages dist/GTFO.AbsurdWeapons-<version>.zip
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
dotnet build .\src\GTFO.AbsurdWeapons\GTFO.AbsurdWeapons.csproj -p:BepInEx="$env:AppData\r2modmanPlus-local\GTFO\profiles\Default\BepInEx"
```

This builds straight into that profile's `BepInEx/plugins/GTFO.AbsurdWeapons/` folder, so you can launch
the game and test immediately. It also produces `dist/GTFO.AbsurdWeapons-<version>.zip` for sharing or
upload.

## Testing

```powershell
dotnet test tests/GTFO.AbsurdWeapons.Tests/GTFO.AbsurdWeapons.Tests.csproj
```

`tests/GTFO.AbsurdWeapons.Tests` (NUnit, no game needed) covers the game-free code in
`src/GTFO.AbsurdWeapons/Pure/`, which the test project compiles straight in (the mod still ships
as one DLL):

- `GearJson`: reading and rewriting gear JSON (category, fire mode, hold), the "(!)" names, names
  with special characters, and not matching component or category numbers that only start alike.
- `Formulas`: full-charge checks, area falloff, chain-lightning damage, wall-slam damage, the gear
  cycler's wrap-around, tentacle pool sizing (never compounding across levels), the grenade blink,
  the Shadow Dagger bonus cap and the light-attack timing scale.

Everything else runs against the game's loaded data, Unity objects or Il2Cpp types, so verify it
by launching the game after `build.ps1`. `BepInEx/LogOutput.log` should show one "added the ..."
line per weapon and, while you look at the melee weapons, the blade length lines.

Still to verify in-game:
- All twenty weapons appear in the loadout screen in the right slots. If not, their categories may
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
- New since the last in-game test: Grenade Launcher (tool slot, model, bounce, fuse, flash,
  blast), Railgun, Chain Lightning Gun, Gravity Hammer (charge never auto-swings, pull, blast),
  Tanto/Wakizashi/Odachi sizes and tempo, Tentacle Pistol, PageUp/PageDown swapping mid-level.
- Flamethrower: the cone falls off with range and the stagger reads as "uncontrollable" up close;
  afterburn keeps ticking after you stop firing or switch weapons and stacks/refreshes correctly;
  fire visibly catches between enemies standing close together; holding Aim lobs a fuel bomb that
  explodes on first contact (no fuse) and leaves a damaging patch rather than one blast. Known gap:
  afterburn ticks and the fuel bomb's fire pool are the igniting player's own game only - a
  teammate doesn't see your flame's impact flashes or the lingering ground fire, only the gun firing
  and the enemy reacting (which does sync).
- Flak Cannon: not aiming scatters bouncing shrapnel that reads as devastating up close and weak
  across a room; holding Aim fires one ball instead that explodes into a visible radial burst of
  shrapnel on whatever it touches first (enemy or wall), never a fuse. Known gap: the primary
  shards aren't sent to teammates (only the ball is), so they only see your muzzle flash and the
  enemies reacting, not the bouncing debris itself.
- Victory Banner: fires it sets one down right in front of you (no throw); firing again picks it
  back up; PageUp/PageDown away from it, re-equipping another tool, or Home's restore all despawn
  it on their own within half a second. The ring heals everyone standing in it and the damage buff
  shows up on your own hits (any weapon) while you're inside it, climbing as you get kills without
  leaving, resetting the moment you step out.
- Tentacle Rifle (tentacles draw and bite, confirmed):
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
  - The Plasma Pistol and Rifle fire ~17.5 blue balls a second, straight, with no recoil; the blue tint takes
    on the whole projectile (glow, trail, particle core).
  - Hive Pistol/Rifle rounds leave the muzzle (not the camera) and curve onto locked enemies, cycling
    between them.
  - Neither gun ever reloads or empties.
- Performance with many impact flashes at once.

## Config

`BepInEx/config/MindAttic.GTFO.AbsurdWeapons.cfg` has one section per melee weapon: `BladeLength`,
`BladeLengthAxis`, `Reach`, and now also `LightAttackDamage`, `ChargedAttackDamage`,
`LightStaggerMulti`, `ChargedStaggerMulti`, `AttackSphereRadius`, `ChargeSpeedMulti` and
`LightAttackSpeed` (0 keeps the built-in value for each; a weapon that doesn't use a given stat
just ignores it). `BladeLength` is re-read whenever a model is built, so it can be tuned without
restarting; everything else here is baked in at `GameDataInit.Initialize`, so it needs a restart.
Shadow Dagger's stealth duration/bonus-per-kill/cap (`[Shadow Dagger] StealthSeconds` etc.) and the
Katana's dash distance/time/slash radius/stagger (`[Katana] Dash...`) are their own entries, read
live. Also config now: Gravity Hammer's pull (`[Gravity Hammer] Pull...`), the hammer-family impact
bursts - Mjolnir's thunderclap, the Warhammer's shockwave and the Gravity Hammer's release blast
(`[Mjolnir] Thunderclap...`, `[Warhammer] Shockwave...`, `[Gravity Hammer] Blast...`) - Mjolnir's
throw (`[Mjolnir] Throw...`) and the shared physical-knockback tuning (`[Knockback]`). The gun stats are fixed in `GunInjector` for the ten original guns (see G.AWPN-T19/T20),
except for the Flamethrower, Flak Cannon and Victory Banner: every one of their numbers - gun stats
(damage, rate of fire, clip, pellet count/spread) included - is its own config entry
(`[Flamethrower]`, `[Flak Cannon]`, `[Victory Banner]`), read once at `GameDataInit.Initialize` for
the gun stats and live for the runtime behaviour (ranges, radii, afterburn, the damage buff), so
most of it can be dialed in without restarting.

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
