using HarmonyLib;
using UnityModManagerNet;
using UnityEngine;

namespace SkeletonCrew
{
    public static class Main
    {
        public static UnityModManager.ModEntry.ModLogger Logger;
        public static Settings Settings;

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
            GUILayout.Label("Skeleton Names");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Spear skeleton:", GUILayout.Width(100));
            Settings.SkeletonName1 =
                GUILayout.TextField(
                    Settings.SkeletonName1 ?? "",
                    GUILayout.Width(200));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Saber skeleton:", GUILayout.Width(100));
            Settings.SkeletonName2 =
                GUILayout.TextField(
                    Settings.SkeletonName2 ?? "",
                    GUILayout.Width(200));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Morning star skeleton:", GUILayout.Width(100));
            Settings.SkeletonName3 =
                GUILayout.TextField(
                    Settings.SkeletonName3 ?? "",
                    GUILayout.Width(200));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.Label(
                "Names are applied the next time skeletons are summoned.");
        }

        private static void OnSaveGUI(
            UnityModManager.ModEntry modEntry)
        {
            Settings.Save(modEntry);
        }
    }
}