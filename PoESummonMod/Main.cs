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

            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll();

            Logger.Log("Harmony patches applied.");
            Logger.Log("Skeleton Crew loaded successfully.");

            return true;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.Label("Skeleton Names, Models and Weapons");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Skeleton 1:", GUILayout.Width(100));
            Settings.SkeletonName1 =
                GUILayout.TextField(
                    Settings.SkeletonName1 ?? "",
                    GUILayout.Width(200));
            GUILayout.EndHorizontal();
            Settings.SkeletonModel1 = DrawModelDropdown(0);
            DrawHands(0);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Skeleton 2:", GUILayout.Width(100));
            Settings.SkeletonName2 =
                GUILayout.TextField(
                    Settings.SkeletonName2 ?? "",
                    GUILayout.Width(200));
            GUILayout.EndHorizontal();
            Settings.SkeletonModel2 = DrawModelDropdown(1);
            DrawHands(1);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Skeleton 3:", GUILayout.Width(100));
            Settings.SkeletonName3 =
                GUILayout.TextField(
                    Settings.SkeletonName3 ?? "",
                    GUILayout.Width(200));
            GUILayout.EndHorizontal();
            Settings.SkeletonModel3 = DrawModelDropdown(2);
            DrawHands(2);

            GUILayout.Space(10);

            GUILayout.Label(
                "Names, models and racial bonuses, and weapons are applied the next time skeletons are summoned.");
        }

        private static string DrawModelDropdown(int skeletonIndex)
        {
            // Weapon dropdowns use IDs 0-5; model dropdowns use IDs 6-8.
            int dropdownId = 6 + skeletonIndex;
            SkeletonModelOption selected = Settings.GetModel(skeletonIndex);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Model:", GUILayout.Width(100));
            if (GUILayout.Button(selected.Label +
                (openDropdown == dropdownId ? " [-]" : " [+]"), GUILayout.Width(250)))
                openDropdown = openDropdown == dropdownId ? -1 : dropdownId;
            GUILayout.EndHorizontal();

            if (openDropdown == dropdownId)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(104);
                GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(250));
                foreach (SkeletonModelOption option in SkeletonModelCatalog.Options)
                {
                    if (GUILayout.Button(option.Label))
                    {
                        selected = option;
                        openDropdown = -1;
                    }
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(selected.BonusDescription);
            return selected.Id;
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
