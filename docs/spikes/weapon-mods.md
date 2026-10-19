# Spike: one mod per weapon

**Ticket:** G.AWPN-T43 · **Type:** experiment spike (time-boxed, throwaway allowed) · **Status:** proposed

## Goal

Make it easy to ship a single weapon as its own Thunderstore mod (`GtfoRailgun`, `GtfoKatana`, ...)
and to add a new weapon without touching anyone else's code - while the full set can still be
installed together.

## Why it's hard today

Everything is one plugin, and the weapons are wired into each other through shared files:

- `Plugin.Load` binds, registers Il2Cpp types, inits and adds components for every weapon by hand.
- `GunInjector` / `MeleeInjector` hold every weapon's datablock code plus the shared helpers.
- `SeekerGuns.BeforeWeaponRay` is one long `if` chain that knows every gun by id; the stream
  (no-fire) prefix lists the laser and chain lightning ids; infinite ammo checks `GunInjector.IsOurs`.
- One config file (`MindAttic.GTFO.EZ.AbsurdWeapons.cfg`) holds every weapon's section.

Removing a weapon (as done in T27-T40) meant edits in five or six files; adding one is the same.

## Proposed shape

```
src/
  AbsurdWeapons.Core/        BepInEx plugin + library every weapon mod depends on
  Weapons/
    Railgun/                 one small plugin per weapon: code + package/ (manifest, icon, README)
    Katana/
    ...
  AbsurdWeapons.Bundle/      no code: a Thunderstore package whose manifest depends on all of them
templates/weapon/            `dotnet new` template (or new-weapon.ps1) for a new weapon mod
```

### Core owns (shared, patched once)

- **Datablock helpers:** `DataBlocks`, `GearJson`, and the register paths now in the injectors -
  `GunRegistration` (`ModelSource`, `StatsFrom`, `Register`, kickless / half recoil, holds) and
  `MeleeRegistration` (`Register`, `WithLightAttackTempo`), plus id-collision checks.
- **A weapon registry** and the single `GameDataInit.Initialize` postfix that asks every
  registered weapon to add its datablocks (after GTFO.EZ, as now).
- **One dispatcher per shared hook**, consulting the registry instead of hard-coded ids:
  `Weapon.CastWeaponRay` (a weapon claims its rays), `BulletWeapon.Fire` (stream weapons skip it),
  `MeleeWeaponFirstPerson.ChangeState` / `DoAttackDamage`. Weapon mods never patch these
  themselves, so two mods can't fight over the same prefix.
- **Shared runtime:** infinite ammo (any registered gun / tool), `GearCycler` (cycles every
  registered weapon), `AreaDamage`, `BurstFlash`, `Explosion`, `Knockback`, `LightningBolt`,
  trigger-driven stream support (`LaserDriver`/`ChainLightningDriver` become one `StreamDriver`),
  `Formulas`.

### A weapon mod is

```csharp
[BepInPlugin("MindAttic.GTFO.EZ.AbsurdWeapons.Railgun", "Railgun (!)", "1.0.0")]
[BepInDependency(Core.Guid)]
public class RailgunPlugin : BasePlugin
{
    public override void Load() => WeaponRegistry.Add(new RailgunWeapon(Config));
}

sealed class RailgunWeapon : GunWeapon            // or MeleeWeapon / ToolWeapon / ConsumableItem
{
    public override uint Id => 1108;
    public override string Name => "Railgun";
    public override void AddDataBlocks(GunRegistration r) { /* model, stats, recoil */ }
    public override bool OnRay(in ShotContext shot) { /* slug + beam */ return true; }
    public override void RegisterTypes() => ClassInjector.RegisterTypeInIl2Cpp<RailBeam>();
    public override void Init() { /* its network events */ }
}
```

- Its own config file (BepInEx gives each plugin one), its own manifest/icon/README, its own
  Harmony instance only for patches nothing else needs.
- Base classes give the common cases for free: `GunWeapon` (ray hook), `StreamGun` (trigger-driven,
  no fire), `MeleeWeapon` (charge timing, full-charge check), `ToolWeapon` (glue hook,
  infinite tool ammo), `ConsumableItem` (item clone, infinite stack, hand-out).

### Ids

A checked-in `weapon-ids.json` in Core lists every id in use (and the retired ones: 1004,
1007-1010, 1111, 1112, 1114, 1201's model swap, ...). Core logs and refuses a duplicate at load,
so two separately installed mods can't collide.

## Spike scope (time-box: 1-2 days)

1. Extract `AbsurdWeapons.Core` with the registry, the `CastWeaponRay` / `Fire` / `ChangeState`
   dispatchers, infinite ammo, gear cycler and the datablock helpers.
2. Convert **two** weapons that cover the different shapes into standalone mods:
   **Railgun** (gun, ray hook, recoil block, network event) and **Katana** (melee, charge timing,
   runtime `DashRunner` component, blade stretch).
3. Leave every other weapon in the current plugin, re-pointed at Core, so the game still plays.
4. Build each as its own package zip from `build.ps1`.

## Questions the spike must answer

- Does BepInEx dependency ordering get every weapon registered before `GameDataInit.Initialize`?
- Can Core register shared Il2Cpp types once while weapon mods register their own without clashes?
- Does a weapon mod installed **without** the others load, show in the loadout, sync in a lobby?
- Do Harmony patches from Core + a weapon mod coexist (e.g. Katana's `ChangeState` via the dispatcher)?
- What does a new weapon cost in files and lines with the template? (Target: one folder, ~2 files.)
- Config migration: settings move from the one `.cfg` to per-weapon files - acceptable reset, or copy?
- Estimate for converting the remaining weapons.

## Exit criteria

- Railgun and Katana each load alone, together, and alongside the remaining monolith, in a lobby.
- A short findings section added here: answers to the questions above, a go / no-go, and the
  follow-up tickets (one per remaining weapon conversion, the template, the bundle package).

## Risks

- Il2Cpp type registration and Harmony patching across assemblies (watch for double patching).
- Thunderstore: every weapon mod depends on Core and GTFO-API; versions have to move together.
- Players with old loadouts: ids stay the same, so nothing changes for them.
