using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace SiH_Tweaks
{
    [BepInPlugin(GUID, DisplayName, Version)]
    [BepInDependency(ConfigurationManager.ConfigurationManager.GUID, ConfigurationManager.ConfigurationManager.Version)]
    [BepInDependency("gravydevsupreme.xunity.autotranslator", BepInDependency.DependencyFlags.SoftDependency)]
    public class SummerHeatTweaksPlugin : BaseUnityPlugin
    {
        public const string GUID = "SiH_Tweaks";
        public const string DisplayName = Constants.Name;
        public const string Version = Constants.Version;

        internal static new ManualLogSource Logger;

        private static ConfigEntry<KeyboardShortcut> _showDebugMode;
        private static ConfigEntry<bool> _showConfigManButton;

        private static ConfigurationManager.ConfigurationManager _configMan;

        private static bool _isJp = true;

        protected void Awake()
        {
            Logger = base.Logger;

            try
            {
                var lang = Traverse.CreateWithType("XUnity.AutoTranslator.Plugin.Core.AutoTranslatorSettings").Property<string>("DestinationLanguage").Value;
                _isJp = string.IsNullOrEmpty(lang) || lang.StartsWith("ja");
                Logger.LogInfo($"Running in {(_isJp ? "Japanese" : "non-Japanese")} mode");
            }
            catch (Exception e)
            {
                Logger.LogWarning("AutoTranslator is not installed or is outdated, some features will not work properly. Error: " + e.Message);
            }

            _configMan = (ConfigurationManager.ConfigurationManager)Chainloader.PluginInfos[ConfigurationManager.ConfigurationManager.GUID].Instance;

            _showDebugMode = Config.Bind("General", "Open debug menu", KeyboardShortcut.Empty, "If for some reason you can't use the official way to open the debug menu (hold left alt and click the top of Otoha's head a few times on the title screen) you can set a key that will open the debug menu instead (has to be pressed on the title screen).");

            _showConfigManButton = Config.Bind("General", "Show Plugin settings button in the Settings screen", true, "Changes take effect after a game restart.");

            Harmony.CreateAndPatchAll(typeof(Hooks));

            SceneManager.sceneLoaded += (scene, mode) => Logger.Log(LogLevel.Debug, $"SceneManager.sceneLoaded - Name=[{scene.name}] Mode=[{mode}]");
        }

        protected void Start()
        {
            if (_isJp) return;

            var store = FindObjectOfType<SaveCheck>().StoreName;
            NativeMethods.SetWindowTitle($"Summer in Heat v{Application.version}_{store}");
        }

        private static class NativeMethods
        {
            [DllImport("user32.dll")] private static extern bool SetWindowText(IntPtr hwnd, string lpString);
            [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();

            public static void SetWindowTitle(string name)
            {
                if (string.IsNullOrEmpty(name)) throw new ArgumentException("Value cannot be null or empty.", nameof(name));
                var activeWindow = GetActiveWindow();
                if (activeWindow != IntPtr.Zero)
                    SetWindowText(activeWindow, name);
                else
                    Logger.LogWarning("Failed to change window title text: GetActiveWindow returned a null pointer");
            }
        }

        private static class Hooks
        {
            #region Fix exception logging

            /// <summary>
            /// Stop the game from disabling logging of unhandled exceptions
            /// </summary>
            [HarmonyTranspiler]
            [HarmonyPatch(typeof(SaveCheck), nameof(SaveCheck.Awake))]
            [HarmonyPatch(typeof(ConfigSetting), nameof(ConfigSetting.Update))]
            private static IEnumerable<CodeInstruction> ConfigSettingUpdateTranspiler(IEnumerable<CodeInstruction> instructions)
            {
                // 281	0407	call	class [UnityEngine.CoreModule]UnityEngine.ILogger [UnityEngine.CoreModule]UnityEngine.Debug::get_unityLogger()
                // 282	040C	ldc.i4.0
                // 283	040D	callvirt	instance void [UnityEngine.CoreModule]UnityEngine.ILogger::set_logEnabled(bool)
                return new CodeMatcher(instructions).MatchForward(false,
                                                                  new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(Debug), nameof(Debug.unityLogger))),
                                                                  new CodeMatch(OpCodes.Ldc_I4_0),
                                                                  new CodeMatch(OpCodes.Callvirt, AccessTools.PropertySetter(typeof(ILogger), nameof(ILogger.logEnabled))))
                                                    .ThrowIfInvalid("Hook point not found")
                                                    .SetAndAdvance(OpCodes.Nop, null)
                                                    .SetAndAdvance(OpCodes.Nop, null)
                                                    .SetAndAdvance(OpCodes.Nop, null)
                                                    .Instructions();
            }

            /// <summary>
            /// Fix exception spam when right clicking in ADV scenes
            /// </summary>
            [HarmonyFinalizer]
            [HarmonyPatch(typeof(XYZ), nameof(XYZ.Update))]
            private static void XYZ_Update_ExceptionEater(XYZ __instance, ref Exception __exception)
            {
                if (__exception is NullReferenceException)
                    __exception = null;
            }

            #endregion

            /// <summary>
            /// Debug menu hotkey
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(TitleScript), nameof(TitleScript.Update))]
            private static void TitleScriptUpdatePostfix()
            {
                if (_showDebugMode.Value.IsDown())
                {
                    var uib = FindObjectOfType<TitleScript>()?.DebugBt.GetComponent<UIButton>();
                    if (uib != null) uib.OnClick();
                }
            }

            /// <summary>
            /// Show the Plugin settings button in the Settings screen
            /// </summary>
            [HarmonyPostfix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(ConfigSetting), nameof(ConfigSetting.Start))]
            private static void ConfigSettingHook(ConfigSetting __instance)
            {
                if (!_showConfigManButton.Value) return;

                var sideButtons = __instance.transform.Cast<Transform>().Where(x => x.gameObject.activeSelf && x.name.StartsWith("ConfBt0")).OrderByDescending(x => x.name).ToList();
                var top = sideButtons[1]; //__instance.transform.Find("ConfBt03");
                var bottom = sideButtons[0]; //__instance.transform.Find("ConfBt04");

                var newbt = Instantiate(bottom.gameObject, bottom.parent);
                newbt.name = "ConfBtPluginSettings";

                newbt.transform.localPosition = new Vector3(bottom.localPosition.x, bottom.localPosition.y - Mathf.Abs(top.localPosition.y - bottom.localPosition.y), bottom.localPosition.z);

                var label = newbt.GetComponentInChildren<UILabel>();
                label.text = "Plugin settings";

                var button = newbt.GetComponent<UIButton>();
                button.onClick_L.Clear();

                button.onClick_L.Add(new EventDelegate(() => _configMan.DisplayingWindow = true));

                var trigger = newbt.GetComponent<UIEventTrigger>();
                trigger.onHoverOver.Clear();
                trigger.onHoverOver.Add(new EventDelegate(() => FindObjectOfType<ConfigHelp>().HelpLabel.text = "Open BepInEx plugin settings"));
            }

            /// <summary>
            /// Look for local manual file and open it instead of the online manual
            /// </summary>
            [HarmonyPrefix]
            [HarmonyPatch(typeof(ConfigSetting), nameof(ConfigSetting.OnlineManual))]
            private static bool OnlineManualHook(ConfigSetting __instance)
            {
                try
                {
                    var localUrl = Path.GetFullPath(Paths.GameRootPath + "/../Manual/" + (_isJp ? "manual_jp.html" : "manual_en.html"));
                    Logger.LogDebug("Trying to open " + localUrl);
                    if (File.Exists(localUrl))
                    {
                        Process.Start(new ProcessStartInfo(localUrl) { UseShellExecute = true });
                        return false;
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }

                return true;
            }

            /// <summary>
            /// Fix character names in backlog not being translated correctly, resulting in the entire backlog text being auto-translated
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(ADV_Loader), nameof(ADV_Loader.LineLoad))]
            private static void BackLogTranslationFix(ADV_Loader __instance, ref IEnumerator __result)
            {
                IEnumerator CoPostifx(IEnumerator orig)
                {
                    yield return orig;
                    var log = __instance.AllLog.LastOrDefault();
                    if (!string.IsNullOrWhiteSpace(log?.TextLog))
                    {
                        var split = log.TextLog.Split(new[] { '\n' }, 2, StringSplitOptions.None);
                        if (split.Length == 2)
                        {
                            var name = split[0];
                            if (TranslationHelper.TryTranslate(name, out var nameTl))
                                log.TextLog = nameTl + "\n" + split[1];
                        }
                    }
                }
                __result = CoPostifx(__result);
            }

            /// <summary>
            /// Disable voice log updates, AT can sometimes catch those and translate garbage
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(VoiceController), "Awake")]
            private static void VoiceController_Awake_Postfix(VoiceController __instance)
            {
                if (__instance.DebugLog)
                {
                    __instance.DebugLog.enabled = false;
                    __instance.DebugLog.text = "";
                }
            }

            /// <summary>
            /// Fix the text width in the talk and koekake scenes
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(FH_Controller), nameof(FH_Controller.Awake_Method))]
            private static void FH_Controller_Awake_Postfix(FH_Controller __instance)
            {
                if (!_isJp && __instance.enabled)
                {
                    // HACK: Make text not flow all the way to the screen edges to make it easier to read and look better. The character name is still randomly placed vertically.
                    // To fix this properly the stock logic of having newlines in text and counting them to set position of the name would need to be replaced (KoekakeName)
                    __instance.KoekakeText.width = 1010;
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Talk_Controller), nameof(Talk_Controller.Awake_Method))]
            private static void Talk_Controller_Awake_Postfix(Talk_Controller __instance)
            {
                if (!_isJp && __instance.enabled)
                {
                    // HACK: Make text not flow all the way to the screen edges to make it easier to read and look better. The character name is still randomly placed vertically.
                    // To fix this properly the stock logic of having newlines in text and counting them to set position of the name would need to be replaced (TalkName)
                    __instance.TalkText.width = 1010;
                }
            }

        }
    }
}
