# Skeleton Crew

Chanter skeleton summons with configurable per-hand weapons and shields, total-level
scaling, and equipment quality bonuses based on chanter level when Multiclass is active.

## Build and package

Run `./Build-Release.ps1` in PowerShell. Requires Visual Studio MSBuild with the .NET
Framework 4.8 targeting pack. The reference paths in `PoESummonMod/SkeletonCrew.csproj`
must point to your installed game, Harmony, and Unity Mod Manager assemblies.

The script rebuilds Release and creates a validated ZIP under
`PoESummonMod/bin/Release/SkeletonCrew-<version>.zip`. It contains both `Info.json`
and `SkeletonCrew.dll`; it does not bundle game assemblies or settings.

## Install

Close the game and install the generated ZIP through Unity Mod Manager, or extract
both files directly into `<Pillars of Eternity>/Mods/SkeletonCrew/`.
Keep existing settings files when updating. Restart the game after replacing the DLL.

Expected layout:

```text
Mods/
  SkeletonCrew/
    Info.json
    SkeletonCrew.dll
```

GitHub source archives are source code, not installable mod releases. If the manager
reports `SkeletonCrew.dll not found`, check this layout: a folder containing only
`Info.json` cannot load the mod. The project does not install builds into the game
directory automatically.

## Rhymer's Summon turn-based fix (1.2.1)

Rhymer's Summon has no attack animation but applies its impact after a short
 delay. Turn-based mode can finish the action before that impact. The ability's
 readiness check then rejects activation with NoAction or NotYourTurn, skipping
 the normal charge consumption, while Summon still creates the skeletons.

The patch ignores only those two readiness flags while this specific summon is
 resolving its impact in tactical combat. Normal activation consumes the charge;
 there is no separate charge deduction. Charge limits and other prerequisites
 remain enforced. Ability selection, other summons, and real-time combat keep
 their original readiness behavior. A Harmony finalizer restores the temporary
 scope even if impact processing throws.

Built against the installed game assemblies and checked with a managed Harmony
 regression harness. In-game validation is still required: cast once in an
 encounter, confirm a second cast is unavailable, confirm the next encounter
 restores the use, and check with automatic turn ending both on and off.
