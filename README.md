# GTFO.Weapons

Dev repo for **GTFO.Weapons**, a BepInEx IL2CPP plugin for GTFO that adds a new melee weapon, the
Glaive: the IsoCo Stinger spear model swung overhead with the sledgehammer's animation set. See
`package/README.md` for the player-facing description.

## How it works

No game files are shipped or edited. `GlaiveInjector` patches `GameDataInit.Initialize` and, once
the game's own datablocks are loaded, registers three new ones with `GameDataBlockBase<T>.AddBlock`:

| Block | Cloned from | Changes |
|---|---|---|
| `MeleeArchetypeDataBlock` 1001 | `Spear` | hammer's `MeleeAnimationSet`/`MeleeSFXSet`, damage, stagger, hit width; spear's reach; `EvaluateHoldBeforeAttack` off |
| `GearCategoryDataBlock` 1001 | `MeleeSpear` | points at archetype 1001, hammer's third-person movement set, name "Glaive" |
| `PlayerOfflineGearDataBlock` 1001 | `Spear_02` (IsoCo Stinger) | gear JSON category -> 1001, name "Glaive" |

Ids are fixed (not "next free") so every player in a lobby agrees on them; if another mod already
uses 1001 the Glaive isn't added and an error is logged. Vanilla ids and fields were taken from a
build-34855 datablock dump (github.com/igobyjama/devile, `Devile/BepInEx/GameData-Dump/34855`).

## Layout

```
src/GTFO.Weapons/  the plugin project (needs the game + a BepInEx profile to build)
package/           exactly what ships to Thunderstore
build.ps1          builds into the "GTFO - Easy Mode DEV" profile and zips dist/GTFO.Weapons-<version>.zip
```

## Building

Same setup as GTFO.EZ: BepInExPack_GTFO installed and launched once in a Thunderstore Mod Manager
profile, then `.\build.ps1` (or `.\build.ps1 -Profile MyProfile`).

## Still to verify in-game

- The Glaive shows up in the melee slot. If not, its category may also need adding to
  `GearCategoryFilterDataBlock` 14 ("MeleeWeapons").
- How the spear's hand placement looks under the hammer's swing.
- Hit detection lines up with the blade at spear reach.

## Versioning

Major-only (v1, v2, ...), matching the other MindAttic projects; the manifest uses `N.0.0`.
