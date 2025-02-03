using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Renderer = UnityEngine.Renderer;

namespace SiH_Uncensor
{
    [BepInPlugin(GUID, DisplayName, Version)]
    public class SummerHeatUncensorPlugin : BaseUnityPlugin
    {
        public const string DisplayName = "SummerInHeat Uncensor";
        public const string GUID = "SiH_Uncensor";
        public const string Version = "1.0.0";

        private const int NoMosaicId = 4;
        private const string NoMosaicStr = "OFF";

        internal static new ManualLogSource Logger;

        protected void Awake()
        {
            Logger = base.Logger;

            TextureReplacer.ReloadReplacementImages(Info.Location);

            Harmony.CreateAndPatchAll(typeof(Hooks));
        }

        private static class Hooks
        {
            [HarmonyPostfix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(MaterialChange_Moza), nameof(MaterialChange_Moza.MatChange))]
            private static void MaterialChange_Moza_MatChange_Postfix(MaterialChange_Moza __instance, bool ___RendeCheck, int ID, string[] List, bool b)
            {
                var run = List.Contains(__instance.name);
                if (b) run = !run;
                if (!run) return;

                var isDecensor = ID == NoMosaicId;

                var renderer = ___RendeCheck ? (Renderer)__instance.GetComponent<SkinnedMeshRenderer>() : __instance.GetComponent<MeshRenderer>();
                renderer.enabled = !isDecensor;

                var overrideEye = __instance.GetComponent<OverrideEye>();
                if (overrideEye.enabled)
                    overrideEye.enabled = !isDecensor;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ConfigSetting), "Awake")]
            private static void ConfigSetting_Awake_Postfix(ConfigSetting __instance, UIPopupList ___ConfBt_MosaicType_List, UIPopupList ___HS_QS09)
            {
                ___ConfBt_MosaicType_List.AddItem(NoMosaicStr);
                ___HS_QS09.AddItem(NoMosaicStr);
            }

            [HarmonyTranspiler]
            [HarmonyPatch(typeof(ConfigSetting), nameof(ConfigSetting.MosaicSetting_Conf))]
            [HarmonyPatch(typeof(ConfigSetting), nameof(ConfigSetting.MosaicSetting_Mini))]
            private static IEnumerable<CodeInstruction> MosaicSettingTpl(IEnumerable<CodeInstruction> instructions)
            {
                return new CodeMatcher(instructions).MatchForward(false, new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(ConfigSetting), nameof(ConfigSetting.MosaicSetting))))
                                                    .ThrowIfInvalid("No MosaicSetting?")
                                                    .Insert(new CodeInstruction(OpCodes.Ldloc_0),
                                                            CodeInstruction.Call(typeof(Hooks), nameof(MosaicSettingHelper)))
                                                    .Instructions();
            }
            private static void MosaicSettingHelper(string text)
            {
                if (text == NoMosaicStr)
                    ConfigClass.MosaicSetting = 4;
            }

            [HarmonyPrefix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(ConfigSetting), nameof(ConfigSetting.MosaicSetting))]
            private static void MosaicSetting_Prefix(ConfigSetting __instance, ref bool ___MosaicSettingTimer_, UIPopupList ___ConfBt_MosaicType_List, UIPopupList ___HS_QS09, Camera ___DanmenCamera)
            {
                if (___MosaicSettingTimer_) return;

                if (ConfigClass.MosaicSetting == NoMosaicId)
                {
                    ___ConfBt_MosaicType_List.value = NoMosaicStr;
                    ___HS_QS09.value = NoMosaicStr;
                }

                // Handle xray window
                if (___DanmenCamera)
                {
                    var mosaicEnabled = ConfigClass.MosaicSetting != NoMosaicId;
                    var xrayWindow = ___DanmenCamera.transform.parent;
                    foreach (Transform child in xrayWindow)
                    {
                        if (child.name.Contains("moza"))
                            child.gameObject.SetActive(mosaicEnabled);

                    }

                    TextureReplacer.ReplaceMaterialsAndTextures(xrayWindow.GetComponentsInChildren<Renderer>(true));
                }
            }

            [HarmonyPrefix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(CustomDataInputCustomManager), nameof(CustomDataInputCustomManager.UpdateMaterialID))]
            private static bool UpdateMaterialID_Override(CustomDataInputCustomManager __instance, string[] MeshNames, int ID, AssetBundleSystem ___AssetBundleSystem)
            {
                TextureReplacer.ReplaceMaterialsAndTextures(__instance.RootBone.GetComponentsInChildren<Renderer>(true));

                foreach (Transform child in __instance.RootBone)
                {
                    if (!MeshNames.Contains(child.name)) continue;

                    var mpd = child.GetComponent<MeshPrefabData>();
                    if (mpd == null) continue;

                    var renderer = child.GetComponent<SkinnedMeshRenderer>();

                    if (child.name.Contains("moza"))
                        renderer.enabled = ID != NoMosaicId;

                    if (mpd.MatsNameList.Count > ID)
                        renderer.sharedMaterials = ___AssetBundleSystem.GetMaterial(mpd.MatsNameList[ID].Mats);
                }

                return false;
            }

            [HarmonyPrefix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(CustomDataInputCustomManager_PC), nameof(CustomDataInputCustomManager_PC.UpdateMaterialID_Moza))]
            private static void UpdateMaterialID_Moza_Prefix(CustomDataInputCustomManager_PC __instance, Transform ___RootBone)
            {
                TextureReplacer.ReplaceMaterialsAndTextures(___RootBone.GetComponentsInChildren<Renderer>(true));
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(VoiceController), "Awake")]
            private static void VoiceController_Awake_Postfix(VoiceController __instance)
            {
                if (__instance.DebugLog)
                {
                    // Disable log updates, AT can sometimes catch those and translate garbage
                    __instance.DebugLog.enabled = false;
                    __instance.DebugLog.text = "";
                }
            }
        }
    }
}
