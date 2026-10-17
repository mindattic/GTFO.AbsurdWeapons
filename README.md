# GTFO.AbsurdWeapons

Dev repo for **GTFO.AbsurdWeapons**, a BepInEx IL2CPP plugin for GTFO that adds seventeen new weapons, each shown with "(!)"
after its name: four melee weapons that put one weapon's model on another's moves (Katana, Mjolnir, Warhammer, Shadow Dagger), eleven guns (Plasma,
Hive, Laser and Grenade Pistol/Rifle, Tentacle Cannon, Railgun, Chain Lightning Gun), a tool (Victory Banner) and a consumable (Hand Grenade).
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
| Katana | 1002 | `Bat` | `MeleeBat` | `Knife` (Mastaba Fixed Blade, blade x6.9 = ~1.85 m) | light 4 / charged 14, reach 3.5, cleaves; the dash is runtime (`KatanaDash`) |
| Mjolnir | 1003 | `Bat` | `MeleeBat` | `Mallet` (Santonian Mallet) | none beyond id and name; the thunderclap is runtime (`ImpactBursts`) |
| ~~Rapier~~ | 1004 | - | - | - | removed (it never worked); the id stays retired so old loadouts can't pick up a different weapon |
| Warhammer | 1005 | `Hammer` | `MeleeSledgehammer` | `Sledgehammer` (Kovac) | hammer's own animation set and charge; charged 500 / light 6, stagger x55 charged / x11 light, environment x10, hit width 0.6, reach 2.4, cleaves, half speed charging; the shockwave and knockback are runtime (`ImpactBursts`, `Knockback`) |
| Shadow Dagger | 1006 | `Knife` | `MeleeKnife` | `Knife` (Mastaba Fixed Blade) | none beyond id and name; the stealth is runtime (`Stealth`) |

| Plasma Pistol | 1101 | the LRG's archetype (shared with the Plasma Rifle) | the Steigro's (`Pistol_Stagger`) | `Autopistol staggering` (Raptus Steigro, main, no sight) | `AddPlasma`: `FireMode` Auto, damage 22.5 (display; rolled per hit), shot delay 2/35 s, clip 100, own kickless `RecoilDataBlock` 1101 |
| Hive Pistol | 1102 | the HXC's archetype (shared with the Hive Rifle) | the S49's (`Pistol`) | `PistolSemi` (Shelling S49, main, no sight) | `AddHive`: `FireMode` Auto, damage 1, shot delay 0.09, clip 30 s, clip 30, own kickless `RecoilDataBlock` 1102 |
| Hive Rifle | 1104 | the HXC's archetype | the HXC's (`RifleHeavy`) | `RifleHeavyAuto` (Malatack HXC, special) | `AddHive`: same stats as the Hive Pistol, kickless recoil 1104 |
| Plasma Rifle | 1105 | the LRG's archetype | the LRG's (`Gauss_Rifle`) | `HELRifleSemi` (Omneco LRG, special) | `AddPlasma`: same stats as the Plasma Pistol, kickless recoil 1105 |
| Tentacle Cannon | 1103 | the XDist2's archetype | the XDist2's | `ShotgunRanged` (Buckland XDist2, special, iron sights) | `AddTentacle`: `FireMode` Auto, 1 pellet, no cone or spread, damage 11 (per tentacle), shot delay 0.235 s, `ShellCasingSize` 0, clip 60, no spin-up, own kickless `RecoilDataBlock` 1103, own first-person hold `ItemFPSSettingsDataBlock` 1103 (`AddFarLowHold`: vanilla `Shotgun` pushed lower and further out, aim FOV 30); the lashes are runtime (`TentacleCannon`) |
| Laser Pistol | 1106 | the DEL P1's archetype (shared with the Laser Rifle) | the Treffen 2's | `Pistolauto` (Raptus Treffen 2, main, no sight) | `AddLaser`: `FireMode` Auto, damage 2 per tick, beam 70 m, shot delay 0.05 s (20 ticks/s), clip 100, no spread, `ShellCasingSize` 0, kickless recoil 1106, hold swapped to `ItemFPSSettings` 48 (`Pistol_Chunky`, 10 cm lower) so the beam shows; the beam is runtime (`LaserGuns`) |
| Laser Rifle | 1107 | the DEL P1's archetype | the ND6's | `SMGHeavyAuto` (Accrat ND6, no animated parts) | `AddLaser`: same stats as the Laser Pistol, kickless recoil 1107, model's own hold; the ND6 is a main-slot SMG in vanilla, so its cloned gear category's `BaseItem` is forced to the special slot's item (`forceSpecialSlot: true`) |
| Railgun | 1108 | the PR 11's archetype for its fire mode | the PR 11's (`Sniper`) | `SniperSemi` (Koning PR 11, special) | `AddRailgun`: Semi, damage 150 (to every enemy on the line), shot delay 1.5.0 s, clip 5, no spread, `ShellCasingSize` 0, the sniper's own recoil and scope; the slug is runtime (`Railgun`) |
| Chain Lightning Gun | 1109 | the exp1's archetype for its fire mode | the exp1's (`MachineGun_LowRPM`) | `HELGun` (Omneco exp1, special) | `AddChainLightning`: Auto, damage 10 (first strike), shot delay 0.15 s, clip 60, no spread or spin-up, no brass, kickless recoil 1109; the bolts are runtime (`ChainLightning`) |
| Grenade Pistol | 1115 | the Arid 5's archetype (shared with the Grenade Rifle) | the 3RB's | `RevolverHEL` (Bataldo 3RB, main) | `AddGrenadeGun`: Semi, 1 round, no spread, damage shown 40 (the blast), shot delay 0.6 s, clip 6, no brass, the Arid 5's recoil; each round is a grenade (`GrenadeLauncher`) |
| Grenade Rifle | 1116 | same archetype as the Grenade Pistol | the Veruta XII's | `MachinegunAuto` (TechMan Veruta XII, special) | `AddGrenadeGun`: same stats as the Grenade Pistol |
| Victory Banner | 1202 | (tools have none) | `GlueGun` | `Glue_distance` (Stalwart Flow G2, tool) | `AddVictoryBanner`: category and gear only; placing, picking up, healing and the damage buff are runtime (`VictoryBanner`) |
| Hand Grenade | 1203 (an `ItemDataBlock`, not gear) | - | - | the glowstick's item, found by name/prefab | `HandGrenade.AddItem`: the glowstick's `ItemDataBlock` cloned and renamed; the look, fuse, blast, infinite stack and handing it out are runtime (`HandGrenade`) |

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
  - **Hive** is Titanfall's Smart Pistol on a `SemiTargetingQuick` (the Hybrid's), full-auto.
    `SmartLock`: at the trigger, `NextLock` gathers living, visible enemies within 35° of the
    crosshair and 60 m, nearest the crosshair first, and successive shots cycle through up to 6.
    The round leaves the barrel and flies a quadratic Bezier (barrel -> a control point 40% of the
    way along the aim -> the target's `AimTarget`, re-read every frame) at a constant 45 m/s. With
    no lock, or once its target dies, it homes normally (1500°/s, 120° search cone, 50 m).
    `HiveLocks` marks those same enemies with camera-facing diamonds that build from orange to red
    (one per 40 health, up to 3) - the look only.
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

### Tentacle Cannon (`TentacleCannon`, `TentacleLash`)

- **Firing:** `SeekerGuns`' `CastWeaponRay` prefix swallows the tentacle guns' rays too, one per
  round. Shotgun pellet hit data has no `owner`, so the shooter falls back to the firing weapon's
  `Owner`. For the local shooter each ray calls `TentacleCannon.FirePellet`, which turns the aim into
  that tentacle's own line with `Cone` (a random direction within 8° of the aim: a tight cone straight ahead). It
  picks an enemy with `SeekerGuns.FindLocks` around that line (15°, `Reach`, line of sight) that no
  other tentacle fired the same frame has claimed; failing that, any enemy within 15° of the aim and `Reach` (nearest the crosshair first, doubling up if all are claimed), so no tentacle wanders to a surface while an enemy is in view. With no enemy at all, `SomethingToGrab` sends it a short way (`IdleReach`, 4 m) straight out
  along its own line, or to the first surface on that line, with a tighter, faster wriggle - it only
  reaches out to full length, and curves, for an enemy. The hold (`AddFarLowHold`) carries it very
  low at the waist like a minigun, tipped up at the crosshair.
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

- **No shots at all:** prefixes skip the laser guns' archetype trigger logic (`BulletWeaponArchetype.Update`,
  `OnStartFiring`, `OnFireShot`, `BWA_Auto.OnFireShot` - the firing, fire animation and recoil) and
  `BulletWeapon.Fire` (and `BulletWeaponSynced.Fire`), so they never fire - no bullet, muzzle flash, brass or kick. Their hold
  (`AddSteadyHold`, an `ItemFPSSettingsDataBlock` at the gun's id cloned from the model's) never
  relaxes and has no `RecoilAnimation`, so they sit raised at hip height.
- **Trigger-driven:** `LaserDriver` (an injected MonoBehaviour) reads `InputAction.Fire` while a
  laser gun is wielded and, every archetype `ShotDelay` (0.05 s), calls `LaserGuns.Tick`: a raycast
  from the eye (`MASK_BULLETWEAPON_RAY`, 70 m) - the beam's end - and `BulletDamage` (archetype
  damage) on any enemy limb there.
- **One beam:** each player has one `LaserBeam` (an injected MonoBehaviour with a `LineRenderer`
  using the trip mine's laser material). Ticks keep it lit; it goes off 0.15 s after they stop. The
  shooter's own beam re-traces from `FPSCamera` every frame, so it stays on the gun and crosshair
  between ticks. It starts low and right of the eye, where the gun sits on screen.
- **Burn:** where the beam lands, a second short `LineRenderer` is a flickering white-hot spot, an
  `EffectLight` flickers orange, and every 0.06 s `FX_Manager.PlayEffect` plays `Impact_Metal`
  (sparks; a decal every 6th) and on enemies, every third, `Impact_EnemyBody`.
- **Multiplayer:** each tick is sent as `MindAttic.GTFO.AbsurdWeapons.LaserBeam` (start, end, hit kind,
  surface normal); teammates draw the same beam and burn.

### Katana dash (`KatanaDash`, `DashRunner`)

A postfix on `MeleeWeaponFirstPerson.ChangeState` times the local player's Katana charge (from a
charge-up state to a charge-release state). If it was held at least 95% of the animation set's
`MaxDamageChargeTime`, a `ChangeState` prefix holds that release back (the swing waits) and
`DashRunner` (an injected MonoBehaviour) moves the player up to 10 m along the aim,
flattened, over 0.6 s through `PlayerCharacterController.Move`, so collision stops it and position
syncs like any movement. Each frame a `SphereCast` (1.2 m) ahead finds the first living enemy in the
way and cuts the step short to reach it. Contact - that enemy, any enemy within reach of the swept
`OverlapCapsule`, or a wall that stops most of a step - or the end of the distance releases the held
swing (`ChangeState` to the release state, let through by `KatanaDash.Releasing`), so the charged cut
lands right there. If the release won't take, a `BulletDamage` cut stands in.

### Railgun (`Railgun`, `RailBeam`)

The `CastWeaponRay` prefix swallows the Railgun's ray. For the local shooter `Railgun.Fire` runs
`RaycastAll` on `MASK_ENEMY_DAMAGABLE` along the aim out to 200 m - walls don't stop it - sorts the
hits by distance and calls `BulletDamage` once per enemy on the first limb met (so a head is a
head). `RailBeam` draws a core `LineRenderer` the full length and two helix strands around the
first 80 m (0.6 m a turn, 12 points a turn) that spin, widen and fade over 0.8 s, with an
`EffectLight`. `Impact_Metal` sparks play at every wall the line crosses. Teammates get
`MindAttic.GTFO.AbsurdWeapons.RailShot`.

### Chain Lightning Gun (`ChainLightning`, `ChainLightningDriver`, `LightningBolt`)

It never fires: the same `BulletWeapon.Fire` prefix that stops the lasers skips it (no shot, flash
or kick). `ChainLightningDriver` reads `InputAction.Fire` while it's wielded and calls
`ChainLightning.Fire` every 0.08 s, each strike's damage the archetype's scaled by
`0.08 / ShotDelay` (same damage per second as one shot per `ShotDelay`). A bolt lasts 0.16 s, so
successive ticks overlap into one continuous arc. Each tick takes the enemy a ray from the eye hits
(`MASK_BULLETWEAPON_RAY`, 40 m) or, failing that, `SeekerGuns.FindLocks` within 6°. Each strike is
`BulletDamage` (x2 stagger); the next target is the nearest unstruck living enemy within 8 m of the
last strike with a clear `MASK_WORLD` line, up to five jumps at 75% of the previous damage.
`LightningBolt` re-jags a `LineRenderer` through the strike points every frame for 0.16 s, with a
`BurstFlash` and `Impact_Electronics` sparks at each strike. The path (up to 7 points) goes to
teammates as `MindAttic.GTFO.AbsurdWeapons.ChainLightning`.

### Grenade Pistol and Rifle (`GrenadeLauncher`, `LauncherGrenade`, `Explosion`)

- **Firing:** plain semi-auto guns (Bataldo 3RB, main / TechMan Veruta XII, special) sharing the Arid 5's stats,
  so a shot does the same from either; each keeps its own model's recoil. The `CastWeaponRay`
  prefix swallows their ray and, for the local shooter, calls `GrenadeLauncher.Launch`: a grenade
  from that model's barrel as drawn on screen (`Barrel.Shot`), aimed at the crosshair point, at 24 m/s.
  Infinite ammo like every gun here; no brass.
- **Flight:** `LauncherGrenade` flies by hand - gravity, `SphereCast` against
  `MASK_ENEMY_DAMAGABLE` (shooter's game only: explode) and `MASK_WORLD` (reflect, x0.55, settle on
  floors) - so it never touches players. The look is the lock melter's meshes copied from its loaded
  prefab (no scripts), else a capsule. A red `EffectLight` and `LineRenderer` dot blink from ~2 to
  ~16 a second with `DEPLOYER_BEEP_TARGET_ACQUIRED`; bounces play `FRAGGRENADEBOUNCE`.
- **Blast:** at 3 s (or on an enemy) `AreaDamage.Apply` (5 m, 40 falling to 25%, x5 stagger) on the
  shooter's game, the trip mine's explosion effect (`FX_Tripmine` pool), a `BurstFlash` and
  `FRAGGRENADEEXPLODE`. Teammates get launch and detonate messages; their copies only bounce and
  explode when told (or 0.75 s after the fuse).

### Hand Grenade (`HandGrenade`, `HandGrenadeFuse`, `HandGrenadeSupply`)

A consumable, thrown like a glowstick. `HandGrenade.AddItem` (a `GameDataInit.Initialize` postfix)
clones the glowstick's `ItemDataBlock` (the consumable whose name or instance prefab says
"glowstick", preferring the uncolored one) as item 1203, so `ThrowingWeapon`'s wind-up throw, the
thrown item's rigidbody bounce and its replication all stay vanilla.
- **Look:** a `ThrowingWeapon.OnWield` postfix and a `GlowstickInstance.Setup` postfix hide the
  glowstick's renderers and lights and add the lock melter's meshes (`LauncherGrenade.BuildModel`).
  The thrown copy's `GlowstickInstance` is disabled, so it never glows. Third-person and pickup
  models are still the glowstick's.
- **Fuse:** `HandGrenadeFuse`, added to every client's thrown copy, blinks red faster and faster
  and at 3 s shows `Explosion` everywhere; the item's `Owner` (or the host, if it has none) deals
  `AreaDamage` (6 m, 60 falling to 25%, x6 stagger). The host then despawns the item
  (`ItemReplicationManager.DeSpawn`); other games hide it until that arrives.
- **Infinite / supply:** `HandGrenadeSupply` keeps the consumable ammo full while the local
  consumable slot holds item 1203, and on the host gives every player a stack of 99 (`StackSize`) straight into their consumable
  slot once per level (`PlayerBackpackManager.MasterAddItem`), drop-ins included (`GiveOnDrop`).
All of it is `[Hand Grenade]`.

### Sounds (`Sounds`, `LoopSounds`)

Every weapon's sounds are the game's own Wwise events, chosen to match what it is and does, each a
`[Sounds]` config entry holding the event name (empty = silent, or the gun's own sound for a shot).
Guns with a shot override have their vanilla fire audio suppressed (prefixes on `BulletWeapon`'s
`Trigger*FireAudio` / `TriggerSynced*PerShotAudio`) and play the override once per shot from the
ray hook, on every client; "2D" events play for the shooter only. Defaults: Plasma
`GAUSSPISTOLBODYA3D`, Railgun `GAUSSPISTOLFIRE2D_RANDOM`, Grenade Pistol/Rifle
`FLAREGUNFIRE`, Tentacle Cannon
`SNATCHERTENTACLESHOOT` (+ `SNATCHERTENTACLEHIT` on the bite). The trigger-driven guns never fire,
so they have no gunshot: the laser runs a `FREEZERLOOP` (the freeze thrower's beam loop) at the gun while the trigger is held and
Chain Lightning a `TERMINAL_CREEPY_STATIC_LOOP` with `FLUORESCENT_LIGHT_CRACKLE` at the strikes
(`LoopSounds` stops a loop 0.25 s after its last tick). Mjolnir's thunderclap adds
`ELEVATOR_THUNDER`; the Hand Grenade throws with `FRAGGRENADETHROW` (its `m_throwSoundEvent`) and
bounces with `FRAGGRENADEBOUNCE`; the Victory Banner plants with `SENTRYGUN_DEPLOY`.

### Warhammer slam (`WarhammerSlam`)

A `ChangeState` postfix times the local player's Warhammer charge; a release held at least 95% of
`MaxDamageChargeTime` schedules a slam `SlamDelaySeconds` (0.25 s) later - the swing's hit moment. Unless
the swing's own hit already burst since the release (`ImpactBursts.LastBurstTime`), it finds the floor
`SlamReach` (1.8 m) ahead (short of a wall) and calls `ImpactBursts.Release(ImpactBurst.Shockwave)` there:
stagger and knockback all around, the flash (sent to teammates) and an `Impact_Concrete` dust burst.

### Light-attack tempo (`MeleeInjector.WithLightAttackTempo`, `SwordTempo`)

The `MeleeSpeedModifier` getter is never called natively (IL2CPP inlined it), so patching it does
nothing. Instead the Shadow Dagger (x2.5) gets its own copy of their `MeleeAnimationSetDataBlock` (id = the weapon's id) whose light-attack
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
ledges. **Wall slam:** a burst's `SlamDamagePerMeter` (Warhammer 5) turns the
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

The C-Foam launcher's `WantToFireGlue` hook toggles instead of firing a projectile, and logs each
plant and pickup. The planted `BannerPylon` is a flag built from primitives (base, 2.1 m pole,
finial, a green banner with a white emblem on a pivot that waves) with a permanent aura: a
breathing green ground ring at `Radius` and an `EffectLight` filling it, both flaring on each heal
pulse alongside the expanding pulse ring. Nothing active for the owner: a raycast down from just in front of them places a
`BannerPylon` right there (no throw), sent to teammates (`...BannerPlant`), and switches them to
their main weapon. Like a sentry, it's picked up by the owner walking within 2.5 m, looking at it
and pressing Use (`InteractionLayer.SetInteractPrompt` shows "Pick up Victory Banner"), which retires it
(`...BannerPickUp`) and wields the tool slot again - the same message also fires automatically,
from the owner's own game, when `BannerPylon.StillInToolSlot` (checked twice a second, reading
`PlayerBackpackManager.LocalBackpack`'s `GearClass` slot the same way `GearCycler` does) finds the
Victory Banner is no longer what's actually equipped there - PageUp/PageDown, picking something
else, or Home's restore. Every client runs the same planted `BannerPylon` independently: a pulsing
ring every `PulseSeconds` out to `Radius`, and each client's own local player self-heals
(`AddHealth`, `HealPercentPerPulse` of max health - the game's own synced heal, like Shadow Dagger's per-client decisions) if they're standing in it - no
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
  MeleeInjector.cs      melee datablocks (Katana, hammers, Shadow Dagger)
  GunInjector.cs        gun and tool datablocks, holds, kickless recoil
  BladeStretcher.cs     blade / head scaling on model spawn
  WeaponSettings.cs     per-weapon config (BladeLength, BladeLengthAxis, Reach)
  SeekerGuns.cs         fire hooks, seeker kinds, network message
  SeekerProjectile.cs   the player-fired homing projectile
  ShadowDagger.cs       Shadow Dagger fast stabs, stealth (enemy AI patches, sync) and its shimmer
  TentacleCannon.cs     Tentacle Cannon cone, targeting, surface grab and the GPU tentacle lash
  Railgun.cs            Railgun slug and its double-helix beam
  ChainLightning.cs     Chain Lightning Gun's trigger-driven stream (no shots), strikes, jumps and bolt
  HiveLocks.cs          Hive guns' Titanfall-style lock-on: pips, markers, locked-target cycling
  VictoryBanner.cs      Victory Banner placement/pickup, healing and the damage buff
  GrenadeLauncher.cs    Grenade Pistol/Rifle launch, grenade flight, fuse, blast
  HandGrenade.cs        Hand Grenade consumable: item clone, lock-melter look, fuse, infinite stack, hand-out
  SwordTempo.cs         the swords' light-attack speeds
  GearCycler.cs         PageUp/PageDown in-level weapon swapping, Home restores the starting loadout
  LaserGuns.cs          trigger-driven laser beam (no shots), its ticks and burn
  Sounds.cs             per-weapon sound choices, vanilla shot audio suppression, loop sounds
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
- All the weapons appear in the loadout screen in the right slots. If not, their categories may
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
- New since the last in-game test: Grenade Pistol/Rifle (model, bounce, fuse, flash,
  blast), Railgun, Chain Lightning Gun, PageUp/PageDown swapping mid-level.

- Victory Banner: fires it sets one down right in front of you (no throw); firing again picks it
  back up; PageUp/PageDown away from it, re-equipping another tool, or Home's restore all despawn
  it on their own within half a second. The ring heals everyone standing in it and the damage buff
  shows up on your own hits (any weapon) while you're inside it, climbing as you get kills without
  leaving, resetting the moment you step out.
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
  working at 4 m) now goes up to 10 m in 0.6 s and stops at the first enemy it reaches: check it
  stops on contact and slashes that enemy.
- Warhammer knockback: enemies slide and stop at walls and ledges, as host and as client.
- Lasers: one steady beam while held (no per-tick tracer streaks), sparks and glow where it lands,
  no brass; teammates see the beam and burn.
- Seeker guns:
  - The disabled enemy projectile stays quiet (no player homing, sound or self-destruct).
  - Seekers hit enemies, are blocked by walls and doors, and never hurt players.
  - Damage registers when fired by the host and when fired by a client.
  - Teammates see the shots.
  - The Plasma Pistol and Rifle fire ~11.7 blue balls a second, straight, with no recoil; the blue tint takes
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
live. Also config now: the hammer-family impact
bursts - Mjolnir's thunderclap and the Warhammer's shockwave
(`[Mjolnir] Thunderclap...`, `[Warhammer] Shockwave...`) - Mjolnir's
throw (`[Mjolnir] Throw...`) and the shared physical-knockback tuning (`[Knockback]`). The ten original guns' damage, rate of fire and clip size are config too (`[Plasma Guns]`,
`[Hive Guns]`, `[Laser Guns]`, `[Railgun]`, `[Chain Lightning Gun]`, `[Tentacle Guns]` - one section
covers both guns of a pistol/rifle pair, since they share stats); their other runtime numbers are config too: the Plasma/Hive seeker kinds' speed, damage and lock
range (`[Plasma Guns]`/`[Hive Guns]`, alongside the gun stats above), the laser beam's
range/width (`[Laser Guns]`), the Railgun's penetration range (`[Railgun]`), Chain Lightning's
range/jump range/falloff/stagger (`[Chain Lightning Gun]` - jump *count* stays fixed at 5: the
network message has exactly enough fields for that many), and the Tentacle guns' spew/engage/lock
cones and stagger (`[Tentacle Guns]`). The Grenade Pistol/Rifle's speed, fuse, blast
radius/damage/stagger, bounciness, shot delay and clip are `[Grenade Launcher]`; the Hand Grenade's fuse, blast and hand-out
are `[Hand Grenade]`. For
the Victory Banner, every one of its numbers - gun stats
(damage, rate of fire, clip, pellet count/spread) included - is its own config entry
(`[Victory Banner]`), read once at `GameDataInit.Initialize` for
the gun stats and live for the runtime behaviour (ranges, radii, the damage buff), so
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
