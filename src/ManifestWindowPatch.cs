using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace CrewManifestFix
{
    /// <summary>
    /// Two changes to the main Crew Manifest window (ManifestController.ManifestWindow):
    ///
    /// 1. A close button "×" in the top right corner. It goes the same way as the toolbar button
    ///    (ManifestBehaviour.onButtonClick), so the toolbar icon follows, and also turns the stock
    ///    toolbar button off without a callback, so the next toolbar click opens the window again.
    /// 2. Crew Manifest 6.5.1.1 logs "Crewmanifest, vessel: ..." from the window function, i.e. on
    ///    every OnGUI event while the window is open: tens of thousands of KSP.log lines per session.
    ///    A transpiler drops that one Debug.Log call.
    /// </summary>
    internal static class ManifestWindowPatch
    {
        private const string ControllerType = "CrewManifest.ManifestController";
        private const float ButtonSize = 18f;
        private const float ButtonMargin = 3f;

        private static Type _behaviour;
        private static MethodInfo _onButtonClick;
        private static FieldInfo _toolbarControl;
        private static FieldInfo _settings;
        private static FieldInfo _manifestPosition;
        private static bool _closeFailed;

        public static void Apply(Harmony harmony, Type behaviour)
        {
            _behaviour = behaviour;
            Type controller = AccessTools.TypeByName(ControllerType);
            MethodInfo window = controller == null ? null : AccessTools.Method(controller, "ManifestWindow", new[] { typeof(int) });
            _onButtonClick = AccessTools.Method(behaviour, "onButtonClick", Type.EmptyTypes);
            _toolbarControl = AccessTools.Field(behaviour, "toolbarControl");
            _settings = AccessTools.Field(behaviour, "Settings");
            _manifestPosition = _settings == null ? null : AccessTools.Field(_settings.FieldType, "ManifestPosition");
            if (window == null || _onButtonClick == null || _settings == null || _manifestPosition == null)
            {
                Log.Warning("ManifestController.ManifestWindow / ManifestBehaviour.onButtonClick / Settings.ManifestPosition not found, window left as is");
                return;
            }

            harmony.Patch(window,
                prefix: new HarmonyMethod(typeof(ManifestWindowPatch), nameof(WindowPrefix)),
                transpiler: new HarmonyMethod(typeof(ManifestWindowPatch), nameof(DropDebugLog)));
            Log.Info("patched ManifestController.ManifestWindow: close button, no per-frame log line");
        }

        /// <summary>
        /// Draws the close button before Crew Manifest's controls: the window ends with
        /// GUI.DragWindow(), which would take the click if the button came after it.
        /// </summary>
        private static void WindowPrefix()
        {
            try
            {
                Rect position = (Rect)_manifestPosition.GetValue(_settings.GetValue(null));
                var rect = new Rect(position.width - ButtonSize - ButtonMargin, ButtonMargin, ButtonSize, ButtonSize);
                if (GUI.Button(rect, "×"))
                    Close();
            }
            catch (Exception e)
            {
                if (_closeFailed)
                    return;
                _closeFailed = true;
                Log.Error($"close button failed: {e}");
            }
        }

        private static void Close()
        {
            MonoBehaviour instance = Loader.Instance;
            if (instance == null || instance.GetType() != _behaviour)
                return;
            _onButtonClick.Invoke(instance, null);

            // Stock toolbar button: show it as off, without calling Crew Manifest back.
            object toolbar = _toolbarControl?.GetValue(instance);
            MethodInfo setFalse = toolbar == null ? null : AccessTools.Method(toolbar.GetType(), "SetFalse", new[] { typeof(bool) });
            setFalse?.Invoke(toolbar, new object[] { false });
        }

        /// <summary>Replaces the one Debug.Log(object) call in ManifestWindow with a pop of its argument.</summary>
        private static IEnumerable<CodeInstruction> DropDebugLog(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo debugLog = AccessTools.Method(typeof(Debug), nameof(Debug.Log), new[] { typeof(object) });
            int dropped = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(debugLog))
                {
                    dropped++;
                    yield return new CodeInstruction(OpCodes.Pop).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                    continue;
                }
                yield return instruction;
            }
            if (dropped != 1)
                Log.Warning($"expected one Debug.Log in ManifestWindow, dropped {dropped}");
        }
    }
}
