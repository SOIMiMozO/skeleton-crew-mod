using HarmonyLib;
using UnityModManagerNet;
using UnityEngine;

namespace SkeletonCrew
{
    public static class Main
    {
        public static UnityModManager.ModEntry.ModLogger Logger;
        public static Settings Settings;
        private static int openDropdown = -1;
        private static readonly Vector2[] weaponScrollPositions = new Vector2[6];
        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Logger = modEntry.Logger;

            Logger.Log("Skeleton Crew loading...");

            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);

            if (Settings == null)
            {
                Settings = new Settings();
            }

            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            RechargeDiagnostics.Initialize(modEntry.Path, modEntry.Info.Version);
            RhymersSummonRecharge.Initialize();

            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll();

            Logger.Log("Harmony patches applied.");
            Logger.Log("Skeleton Crew loaded successfully.");

            return true;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            bool summonScaling = GUILayout.Toggle(Settings.EnableChanterSummonScaling,
                "Scale other Chanter summons with summoner level");
            if (summonScaling != Settings.EnableChanterSummonScaling)
            {
                Settings.EnableChanterSummonScaling = summonScaling;
                RechargeDiagnostics.Write("SETTING chanterSummonScaling=" + summonScaling);
            }
            GUILayout.Space(4);
            DrawDescription("New Chanter summons match your level. Starting strength: phantom 1, wurms/wisps 5, drakes/ogres 9.");
            DrawDescription("Quality upgrades at Chanter levels: phantom 4/8/12/16; wurms/wisps 8/12/16; drakes/ogres 12/16.");
            DrawDescription("Each upgrade: +15% weapon damage, +4 Accuracy, and quality-based DR.");
            DrawDescription("Wurms: burn for 100% of hit damage over 6 base seconds, with normal graze/crit and mitigation rules.");
            DrawDescription("Wisp claws: 5 DR bypass, +5 per upgrade. Item summons are excluded.");
            GUILayout.Space(14);
            bool coexist = GUILayout.Toggle(Settings.EnableSummonCoexistence,
                "Keep Rhymer's and item summons alongside other summons");
            if (coexist != Settings.EnableSummonCoexistence)
            {
                Settings.EnableSummonCoexistence = coexist;
                RechargeDiagnostics.Write("SETTING summonCoexistence=" + coexist);
            }
            GUILayout.Space(4);
            DrawDescription("Rhymer's skeletons and item summons (e.g. figurines) are no longer dismissed when another source summons, expires or is re-used. Re-using the same source still replaces its own summons.");
            GUILayout.Space(6);
            bool unlimited = GUILayout.Toggle(Settings.EnableUnlimitedSummonDuration,
                "Rhymer's and item summons last until combat ends");
            if (unlimited != Settings.EnableUnlimitedSummonDuration)
            {
                Settings.EnableUnlimitedSummonDuration = unlimited;
                RechargeDiagnostics.Write("SETTING unlimitedSummonDuration=" + unlimited);
            }
            GUILayout.Space(4);
            DrawDescription("No duration timer: they stay until combat ends, they die, or the summoner dies. Applies to new summons.");
            GUILayout.Space(14);
            bool recharge =GUILayout.Toggle(Settings.EnableRhymersRecharge,
                "Recharge Rhymer's Summon through damage and kills");
            if (recharge != Settings.EnableRhymersRecharge)
            {
                Settings.EnableRhymersRecharge = recharge;
                RechargeDiagnostics.Write("SETTING recharge=" + recharge);
                RhymersSummonRecharge.Clear();
            }
            GUILayout.Space(4);
            DrawDescription("At zero charges, you and your skeletons must deal the required damage and kill 2 enemies. Constructs do not count.");
            DrawDescription("Damage target: [52 + 16 x (summoner level - 1)] x2 for the first recharge, then x3, x4, etc.");
            DrawDescription("Each recharge restores 1 charge and resets progress. The multiplier resets after battle.");
            GUILayout.Space(14);
            GUILayout.Label("Skeleton Names and Weapons");
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Skeleton 1:", GUILayout.Width(100));
            Settings.SkeletonName1 =
                GUILayout.TextField(
                    Settings.SkeletonName1 ?? "",
                    GUILayout.Width(200));
            GUILayout.EndHorizontal();
            DrawHands(0);
            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Skeleton 2:", GUILayout.Width(100));
            Settings.SkeletonName2 =
                GUILayout.TextField(
                    Settings.SkeletonName2 ?? "",
                    GUILayout.Width(200));
            GUILayout.EndHorizontal();
            DrawHands(1);
            GUILayout.Space(12);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Skeleton 3:", GUILayout.Width(100));
            Settings.SkeletonName3 =
                GUILayout.TextField(
                    Settings.SkeletonName3 ?? "",
                    GUILayout.Width(200));
            GUILayout.EndHorizontal();
            DrawHands(2);

            GUILayout.Space(10);

            GUILayout.Label(
                "Changes apply when you next summon skeletons.");
        }

        private static void DrawDescription(string text)
        {
            GUILayout.Label(text);
            GUILayout.Space(4);
        }

        private static void DrawHands(int skeletonIndex)
        {
            SkeletonHands hands = Settings.GetHands(skeletonIndex);
            hands.MainHand = DrawWeaponDropdown(skeletonIndex * 2, hands.MainHand, false);
            hands.Normalize();
            if (WeaponCatalog.Find(hands.MainHand).TwoHanded)
            {
                if (openDropdown == skeletonIndex * 2 + 1)
                    openDropdown = -1;
                GUILayout.Label("Off hand: Empty (main-hand weapon uses both hands)");
            }
            else
            {
                hands.OffHand = DrawWeaponDropdown(skeletonIndex * 2 + 1, hands.OffHand, true);
            }
        }

        private static string DrawWeaponDropdown(int dropdownId, string selected, bool offHand)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(offHand ? "Off hand:" : "Main hand:", GUILayout.Width(100));
            if (GUILayout.Button(WeaponCatalog.Find(selected).Label +
                (openDropdown == dropdownId ? " [-]" : " [+]"), GUILayout.Width(250)))
                openDropdown = openDropdown == dropdownId ? -1 : dropdownId;
            GUILayout.EndHorizontal();

            if (openDropdown == dropdownId)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(104);
                GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(250));
                weaponScrollPositions[dropdownId] = GUILayout.BeginScrollView(
                    weaponScrollPositions[dropdownId], GUILayout.Height(260));
                foreach (WeaponOption option in WeaponCatalog.Options)
                {
                    if (!WeaponCatalog.CanEquip(option, offHand))
                        continue;
                    if (GUILayout.Button(option.Label))
                    {
                        selected = option.Id;
                        openDropdown = -1;
                    }
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            return selected;
        }
        private static void OnSaveGUI(
            UnityModManager.ModEntry modEntry)
        {
            Settings.Save(modEntry);
        }
    }
}
