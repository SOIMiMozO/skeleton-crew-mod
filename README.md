# Skeleton Crew

## Summon lifetime tracing (1.3.6)

The diagnostic log now traces every creature created by `Summon.SummonCreature`
(spells, Chanter invocations, item summons such as figurines, scripts), not only
the ones this mod scales. Gameplay is unchanged; all hooks only read state.

| Line | Meaning |
| --- | --- |
| `SUMMON SPAWN` | creature, prefab, summoner, source ability/spell class/item, `duration`, `timer`, `roundTime`, `timerRounds`, summoner duration multiplier, tactical mode, round |
| `SUMMON TICK` | turn-based duration tick: elapsed time and `timer=before->after` |
| `SUMMON END` | the game ending summons: `DestroyAllSummoned`, `DeactivateSummons`, `DestroyTacticalSummons` or the source's `OnDestroy`, with remaining summons and `via=` call chain (timer, re-cast, combat end, unequip, ...) |
| `SUMMON DEATH` / `SUMMON UNCONSCIOUS` | a traced creature went down, and who did it |
| `SUMMON GONE` | the creature was disabled/destroyed: lifetime in seconds, rounds and own turns, plus the last recorded cause |

Every ID is now the creature's GameObject ID, so `TURN`, `HIT`, `EQUIPMENT` and
`SUMMON` lines for one creature share the same `#id`. `SUMMON SCALING` also
includes `creature=`, and the `SESSION` header reports the installed version.

In turn-based mode vanilla deducts one round of effect time per tick and drops
the timer to 0 once less than a full round remains, so a summon can expire a round
earlier than `duration / roundTime` suggests. Compare `SUMMON SPAWN timer` with the
`SUMMON TICK` lines to see this.

## Repeated HD texture upgrade fix (1.2.9)

Vanilla appearance setup appends `_HD` to texture names on every upgrade pass,
including textures already upgraded. The mod now skips replacement when this
would request `_HD_HD`, preserving the existing HD texture and material. This
addresses repeated requests such as `Rapier01_V01_C_HD_HD` and
`ShieldL04_V01_N_HD_HD`. The fix applies to the game's appearance upgrade routine
for all characters; normal first-time upgrades and unrelated asset errors are
unchanged. Restart and check equipment/paperdoll appearance and logs in-game.

## Skeleton equipment instance fix (1.2.8)

Armor, weapons, and shields now share a creation path that parents each cloned
item to its skeleton and assigns its original Item.Prefab reference before
placing it in DefaultEquippedItems. Vanilla equipment initialization therefore
reuses the customized instance instead of treating it as a prefab and cloning it
again. This addresses the custom armor-name asset lookup and unparented-item
warnings. Freshly summon skeletons after restarting with this build; existing
saved items are not migrated. Recharge behavior is unchanged. This is not a
confirmed fix for the separate native crash.

In-game validation: summon with a spear/shield and a rapier, confirm equipment
and quality bonuses, dismiss and summon again, and inspect the log for
`EQUIPMENT INSTANCE` entries with `isPrefab=False` and an owner. The previous
`Skeleton_Hide_Chanter_Armor cannot be loaded` and `found with no parent` errors
should no longer occur for newly summoned equipment.

## Live recharge tooltip (1.2.7)

While Rhymer's Summon is tracking a depleted charge, its existing tooltip includes
a **Next charge** section with damage dealt / required damage, kills / required
kills, and the current multiplier. It refreshes while open using the game's
normal tooltip refresh, including contributions from your summoned skeletons.
The section disappears when a charge returns, combat ends, or recharge is disabled.
It appears only during active tracking; other tooltips and the icon's timer are
unchanged. Diagnostic logging from 1.2.6 remains enabled.

## Crash diagnostics (1.2.6)

This diagnostic build automatically writes a separate file for each game session
under `Mods/SkeletonCrew/Logs/recharge-<UTC timestamp>.log`. Each entry is flushed
immediately with write-through enabled. It records combat/turn/round boundaries,
recharge callbacks, ownership attribution, damage/kill progress, charge restoration,
and Unity errors/exceptions with stack traces. Logging failures disable file output
without interrupting gameplay. Gameplay exceptions are logged, not swallowed.

After a crash, keep the newest file from that folder and the game's crash report
or `output_log.txt`, if generated. An unmatched ENTER/RESTORE BEGIN can identify
the last operation reached, but does not by itself prove the cause of a native
crash. Logs are retained across restarts; this verbose diagnostic build may produce
large files over long sessions. No recharge balance rules change in this release.

Chanter skeleton summons with configurable per-hand weapons and shields, total-level
scaling, and equipment quality bonuses based on chanter level when Multiclass is active.

## Optional Rhymer's Summon recharge (1.2.5)

Enable **Recharge Rhymer's Summon through damage and kills** in the mod settings
(off by default). Spending the last charge starts a new progress counter for that
character. Deal **(52 + 16 × (summoner level − 1)) × multiplier damage** and kill **2 enemies**, in either order, to
restore exactly one use. Spending that restored use starts another fresh cycle.
Damage and kills before depletion do not count. The character and their summoned
skeletons share that character's progress counter. Skeleton ownership uses the
game's summoner reference; other party members, their skeletons, non-skeleton
summons, pets, and animal companions do not contribute to this counter.
Targets must be hostile to both the attacking skeleton and its summoner.

The base damage target uses two skeletons with 26 Endurance each at level 1 and
8 additional Endurance each per level: 52 at level 1, 68 at level 2, 116 at
level 5, and 196 at level 10. It uses the summoner's total level, matching the
mod's skeleton level scaling, and is fixed when the last charge is spent.
This is the base Endurance formula; temporary buffs and difficulty modifiers
do not change the recharge target.

The normal initial charge is unchanged. The first restored charge in a battle
requires base damage ×2, the next ×3, then ×4, and so on. At level 1 these targets
are 104, 156, and 208 damage. Each cycle still requires two fresh kills, and no
excess damage carries over. Only successfully restored charges advance the
multiplier. Combat end clears both progress and the multiplier; the next battle
starts at ×2 again. Changing the toggle clears progress but preserves the current
battle's multiplier. Counters are runtime-only and are not saved across reloads.

Only hostile targets count. The game's Animat, FleshConstruct, and Eyeless classes
and Advanced_Construct subrace are excluded; other vessels such as skeletons
remain eligible. Damage counts actual Endurance lost (Health for targets without
Endurance), after mitigation, capped at the target's remaining vitality. Direct
damage, including damage over time credited to the character, also counts.
Instant kills count as a kill but do not manufacture damage progress.

Progress is temporary and clears when the ability regains uses normally, is
destroyed/reloaded, or the toggle changes. Enabling the option with zero uses does
not start a counter retroactively: spend the last charge with the option enabled.
The existing turn-based charge-consumption fix remains in effect.

In-game check: enable the option, spend the final use, then deal at least the
level-scaled damage target and land killing blows on two non-construct enemies.
Confirm exactly one use returns,
then spend it and repeat at the next multiplier. Confirm a new battle starts at
×2 again. Check that an ally's kill and construct damage/kills do
not advance progress, and that disabling the option prevents recharge.
Also check that your skeletons can provide either or both kills and combine
their damage with yours, while another character's skeletons cannot.

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

## Optional scaling for other Chanter summons (1.3.0)

Enable **Scale other Chanter summons with summoner level** in the mod settings.
The option defaults to off and applies to newly summoned creatures. Each affected
summon receives the summoner's total character level, including multiclass levels.
Turning the option off does not change creatures already summoned.

| Summon | Vanilla creature level | Balance reference | Base compensation at normal scaling |
| --- | ---: | ---: | --- |
| Wurm | 2 | Summoner level 5 | -48 base Endurance/Health; -9 Accuracy and each defense |
| Wisp | 3 | Summoner level 5 | -24 base Endurance/Health; -6 Accuracy and each defense |
| Drake | 6 | Summoner level 9 | -72 base Endurance/Health; -9 Accuracy and each defense |
| Ogre | 6 | Summoner level 9 | -84 base Endurance/Health; -9 Accuracy and each defense |
| Phantom | 3 | Summoner level 1 | No compensation; existing base stats at level 1 |

Endurance/Health offsets are applied before Constitution, difficulty vitality,
and the class Health multiplier. Subsequent levels use the game's normal class
growth. Ability attacks' additional per-level Accuracy is compensated too.
Difficulty/content level multipliers are considered when calibrating the unlock
point. Unusually early summons have compensation capped so their vitality is
positive. The level is assigned at creation, as it is for skeletons.

The compensation is applied after each class-table reload so CharacterStats.Start
and later stat refreshes cannot erase it or accumulate extra penalties. Only live
instances from the five named summon prefabs and their matching Chanter invocations
are registered; shared creature and equipment assets are never edited. Skeleton scaling retains its existing behavior.
Abilities and summon duration are unchanged. Quality progression added in 1.3.1 is described below.

Run `./Tests/Test-SummonScaling.ps1` for the managed regression harness. It compiles
the production scaling source against small game API stubs, checking unlock-point
balance, level-16 growth, difficulty rounding, repeated table reloads, toggle
behavior, prefab isolation, ability Accuracy, and early summons. It does not run
Unity or validate Harmony interception in the game.

In-game validation: compare newly summoned wurms/wisps at level 5 and drakes/ogres
at level 9 with the toggle off/on; Endurance, Health, defenses and attack Accuracy
should match. Repeat at level 16 to check growth. Verify phantoms at levels 1 and
16, trigger a stat refresh during combat, and resummon with the option disabled.
The diagnostic log records each scaled spawn's level, compensation and vitality.

### Invocation scope and quality progression (1.3.1)

The same optional toggle now also enables quality bonuses. The mod checks both the
originating Chanter invocation and the creature it creates. It preserves the cast
identity across AttackRoar's separate, asynchronous Summon instance. Summons from
items (including the item-granted wisp spell), other spells, unrelated creatures,
and unrecognized sources do not receive this optional scaling. The five supported
invocations are But Reny Daret's Ghost, Gernisc Slew the Beast, My Son Do You See,
Gernisc's Beast Lit the Night, and Oh But Knock Not.

Creature level still follows total summoner level. Quality follows **Chanter level**
using the same Multiclass integration as skeleton equipment. If that integration
cannot supply a class level, the new optional feature skips the summon instead of
guessing from total level. Existing skeleton behavior is unchanged.

Bonuses below are additions to the original creature equipment, not replacements
for its existing damage, Accuracy, or DR. Each additional weapon grade grants
**+15% additive damage and +4 Accuracy**, including the final grade at level 16.

| Summon | Chanter level 4 | Level 8 | Level 12 | Level 16 |
| --- | --- | --- | --- | --- |
| Phantom | +15% damage, +4 Accuracy, +1 DR | +30%, +8, +2 DR | +45%, +12, +4 DR | +60%, +16, +8 DR |
| Wurms / wisps (Fine baked in) | None | +15%, +4, +1 DR | +30%, +8, +3 DR | +45%, +12, +7 DR |
| Drakes / ogres (Exceptional baked in) | None | None | +15%, +4, +2 DR | +30%, +8, +6 DR |

There is no +0.5 DR per level for these summons. DR uses the difference between
the skeleton armor-quality schedule (1/2/4/8) and the baked-in tier. Armor damage-type
modifiers remain in effect. Armorless wisps receive the equivalent innate DR;
Raw damage still bypasses DR.

Both hand items are cloned and parented to the individual summon before equipment
initialization. Natural weapons such as claws and wurm breath are supported even
when their item lacks the Weapon component. The additive damage/Accuracy changes
apply to those hand-item attacks and their displayed damage; separate special
abilities are unchanged. No shared weapon, armor, spell, or creature prefab is edited.

Regression coverage includes every tier boundary, both hands sharing one original
weapon, shared-prefab isolation, armorless DR, Raw damage, multiclass level selection,
and rejection of item spells, other classes, wrong creatures, and mismatched owners.
The spawn injection sites were also checked against the installed game assembly.
These managed checks do not replace in-game verification of Harmony and Unity:
compare the same invocation at Chanter levels 7/8, 11/12, and 15/16; test an item wisp
summon on the same Chanter and confirm it retains vanilla level and equipment.

### Wurm burning attacks and wisp penetration (1.3.2)

Under the same optional scaling toggle, Chanter-summoned wurms apply an additional
Burn DOT on successful weapon attacks. Its base total is 100% of the completed hit's
FinalAdjustedDamage, after the original hit's mitigation, over 6 base seconds.
Might, difficulty damage scaling, and tactical damage are not applied a second time.
The native DOT handles graze/crit duration (3/6/9 seconds before other duration
modifiers), Intellect, target duration modifiers, and turn-based timing. Graze/crit
changes to the original hit already affect the burn's damage budget. Burn ticks
still face the game's normal Burn mitigation, so their actual total can be lower.
Each hit applies a separate stacking burn, attributed to the attacking wurm.
Misses, zero-damage attacks, and dead/unconscious targets do not receive a burn.

Only private wurm hand-attack instances are registered. The trigger runs after the
original attack deals damage, and native DOT ticks use a different damage path,
so burns cannot recursively create burns. There are no custom timers or global
combat-event subscriptions. Shared attacks, other summons, and item-granted summons
remain excluded. Existing summons retain their configuration until resummoned.

Wisp claws receive +5 DR bypass at their initial tier, +10 at Chanter level 8,
+15 at level 12, and +20 at level 16 (capped thereafter). This is added to any
existing weapon bypass and applies to both equipped claws. Separate wisp abilities
retain their existing penetration. The claws and wurm weapon are cloned even when
no quality upgrade has yet been earned.

Managed regression tests cover damage normalization, hit/graze/crit input and native
creation parameters, independent burns, non-recursion, excluded targets/attacks,
all penetration thresholds, and preservation of shared equipment. Unity's actual
status-effect ticking must still be tested in game: compare hit/graze/crit burns
against a low-DR target in real-time and turn-based modes, check repeated hits and
multiple wurms, and verify wisp claw bypass at levels 5/8/12/16.
### Wurm Burn display and combat log (1.3.3)

Wurm Burn now has an explicit status name and uses the Fan of Flames spell icon.
The overrides identify only the mod's tagged Burn damage effect, including existing
instances of that effect. They no longer rely on the natural weapon's missing item
localization entry or placeholder icon. The spell and item assets are not modified.

Native ApplyDamageDirectly displays floating damage but does not report these DOT
hits to the combat log. A narrowly scoped prefix/postfix now records vitality before
and after each Wurm Burn tick and uses the native ReportDamage function to report
actual post-mitigation damage under the Wurm Burn name. It uses Endurance, or Health
for targets without Endurance, caps overkill at remaining vitality, and skips hidden,
zero-damage and unrelated ticks. Burn damage and duration formulas are unchanged.

The build and managed regression tests verify the label/icon override and reporting
rules; the Fan of Flames icon reference was verified in installed assets. Confirm
status-window appearance and combat-log entries in game after restarting with 1.3.3.
### Detailed burn combat log (1.3.4)

Wurm Burn now uses the native hoverable combat-log message API with a Burn damage
type and DamageInfo metadata. The hover text records the tick before and after source
modifiers, the native DOT DR factor, flat/percentage mitigation, minimum damage,
the mitigation result, and actual vitality lost. DOT ticks do not make another
attack roll, so the tooltip explicitly explains that rather than inventing one.

The previous ReportDamage formatter rounded small positive ticks to whole numbers,
which could display them as zero. Burn entries now retain fractional precision,
including very small positive values. Genuine zero results (including Burn immunity)
remain visible and explained. Native mitigation and minimum-damage rules are unchanged.

The logging scope observes the existing native mitigation call; it does not apply
damage twice or replace the DamageInfo passed into the game's calculation. Nested
unrelated damage suspends the scope, and a Harmony finalizer restores it on errors.
Regression checks cover fractional output, immunity, minimum damage, overkill,
health-only targets, metadata, and nested scope isolation. Verify the actual hover
panel in game; the tests run with managed game API stubs, not the Unity UI.