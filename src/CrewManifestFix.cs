using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CrewManifestFix
{
    /// <summary>
    /// Crew Manifest 6.5.1.1 declares CrewManifest.ManifestBehaviour as
    /// [KSPAddon(Startup.Flight, once: false)] and calls DontDestroyOnLoad on itself in Awake.
    /// KSP therefore creates a new instance on every flight scene load while the old ones never die:
    /// each one adds its own toolbar button and keeps running Update/OnGUI.
    ///
    /// The prefix on Awake keeps the first instance and destroys every later one before it does
    /// anything, which is what once: true would have done.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class Loader : MonoBehaviour
    {
        private const string HarmonyId = "CrewManifestFix";
        private const string BehaviourType = "CrewManifest.ManifestBehaviour";
        private static readonly Version SupportedVersion = new Version(6, 5, 1, 1);

        private static MonoBehaviour _instance;

        private void Start()
        {
            Type behaviour = AccessTools.TypeByName(BehaviourType);
            if (behaviour == null)
            {
                Log.Warning($"{BehaviourType} not found, Crew Manifest is not installed. Nothing patched.");
                return;
            }

            // The fix targets one specific bug in one specific release; a different version may
            // already be fixed or work differently, so it is left alone.
            Version version = behaviour.Assembly.GetName().Version;
            if (version != SupportedVersion)
            {
                Log.Warning($"Crew Manifest {version} found, this fix is only for {SupportedVersion}. Nothing patched.");
                return;
            }

            MethodInfo awake = AccessTools.Method(behaviour, "Awake", Type.EmptyTypes);
            if (awake == null)
            {
                Log.Error($"{BehaviourType}.Awake() not found. This Crew Manifest version is not supported. Nothing patched.");
                return;
            }

            var harmony = new Harmony(HarmonyId);
            harmony.Patch(awake, prefix: new HarmonyMethod(typeof(Loader), nameof(AwakePrefix)));
            Log.Info($"patched {BehaviourType}.Awake");

            // The window patches are optional: the duplicate fix above stays in if they cannot go in.
            try
            {
                ManifestWindowPatch.Apply(harmony, behaviour);
            }
            catch (Exception e)
            {
                Log.Error($"window patches failed, Crew Manifest window left as is: {e}");
            }
        }

        /// <summary>The live Crew Manifest instance (the one whose Awake ran), or null.</summary>
        internal static MonoBehaviour Instance => _instance;

        /// <returns>false to skip Crew Manifest's Awake for a duplicate instance.</returns>
        private static bool AwakePrefix(MonoBehaviour __instance)
        {
            try
            {
                // Unity's == also treats destroyed objects as null.
                if (_instance == null || _instance == __instance)
                {
                    _instance = __instance;
                    return true;
                }

                // Disable first so the duplicate gets no Update/OnGUI before it is destroyed at frame end.
                __instance.enabled = false;
                Destroy(__instance.gameObject);
                return false;
            }
            catch (Exception e)
            {
                Log.Error($"prefix failed, leaving Crew Manifest as is: {e}");
                return true;
            }
        }
    }
}
