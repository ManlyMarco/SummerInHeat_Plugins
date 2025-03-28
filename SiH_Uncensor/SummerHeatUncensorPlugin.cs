using System.Collections;
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
        public const string GUID = "SiH_Uncensor";
        public const string DisplayName = Constants.Name;
        public const string Version = Constants.Version;

        private const int NoMosaicId = 4;
        private const string NoMosaicStr = "OFF";

        internal static new ManualLogSource Logger;

        private static bool _enableOnStart;

        protected void Awake()
        {
            Logger = base.Logger;

            _enableOnStart = Config.Bind("General", "Enable uncensor on game start", true, "Change the 'Mosaic type' setting to OFF on every game start. Disable if you'd like to use a mosaic censor all the time.").Value;


            TextureReplacer.ReloadReplacementImages(Info.Location);

            Harmony.CreateAndPatchAll(typeof(Hooks));
        }

        private static class Hooks
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(SaveCheck), nameof(SaveCheck.Awake))]
            private static void SaveCheck_Awake_Postfix()
            {
                // This is always false in the dmm version but not others for some reason
                // Set to off to fix some ADV scenes still having the backside mosaic
                ConfigClass.AnalMoza = false;
            }

            #region Mosaic on/off
            
            /// <summary>
            /// Replacing guy's textures and materials
            /// </summary>
            [HarmonyPrefix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(CustomDataInputCustomManager_PC), nameof(CustomDataInputCustomManager_PC.UpdateMaterialID_Moza))]
            private static void UpdateMaterialID_Moza_Prefix(CustomDataInputCustomManager_PC __instance, Transform ___RootBone)
            {
                TextureReplacer.ReplaceMaterialsAndTextures(___RootBone.GetComponentsInChildren<Renderer>(true));
                // Afterwards MaterialChange_Moza.MatChange is called
            }

            /// <summary>
            /// The actual mosaic disabling for guys happens here
            /// </summary>
            [HarmonyPostfix]
            [HarmonyWrapSafe]
            [HarmonyPatch(typeof(MaterialChange_Moza), nameof(MaterialChange_Moza.MatChange))]
            private static void MaterialChange_Moza_MatChange_Postfix(MaterialChange_Moza __instance, bool ___RendeCheck, int ID, string[] List, bool b)
            {
                var run = List.Contains(__instance.name);
                if (b) run = !run;
                if (!run) return;

                var isDecensor = ID == NoMosaicId;

                var overrideEye = __instance.GetComponent<OverrideEye>();
                if (overrideEye != null) overrideEye.enabled = !isDecensor;
                // Needs to be disabled since it forces enabled=true on the renderer (only seems to affect mob characters)
                var bgBoxCollider = __instance.GetComponent<BgBoxCollider>();
                if (bgBoxCollider != null) bgBoxCollider.enabled = !isDecensor;

                var renderer = ___RendeCheck ? (Renderer)__instance.GetComponent<SkinnedMeshRenderer>() : __instance.GetComponent<MeshRenderer>();
                renderer.enabled = !isDecensor;
            }

            /// <summary>
            /// The actual mosaic disabling for girls happens here (for some reason heir controller uses a different way of handling it)
            /// </summary>
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

            private static void UpdateXrayMosaicState(Camera ___DanmenCamera)
            {
                if (!___DanmenCamera) return;

                var mosaicEnabled = ConfigClass.MosaicSetting != NoMosaicId;
                var xrayWindow = ___DanmenCamera.transform.parent;
                foreach (Transform child in xrayWindow)
                {
                    if (child.name.Contains("moza"))
                        child.gameObject.SetActive(mosaicEnabled);

                }

                TextureReplacer.ReplaceMaterialsAndTextures(xrayWindow.GetComponentsInChildren<Renderer>(true));
            }

            /// <summary>
            /// Hacky fix for mob characters doing the wall position not being uncensored on map load.
            /// Changing mosaic setting to OFF after the map is loaded fixes it but obviously isn't ideal.
            /// Couldn't find the core issue, this works well enough.
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(BG_Loader), nameof(BG_Loader.UpdateMaterialBG13_Body))]
            private static void OrgySceneMozaPostfix()
            {
                if (ConfigClass.MosaicSetting == NoMosaicId)
                {
                    IEnumerator DelayedCo(ConfigSetting inst)
                    {
                        yield return new WaitForEndOfFrame();
                        yield return new WaitForSeconds(1);
                        inst.MosaicSetting();
                    }

                    var configSetting = FindObjectOfType<ConfigSetting>();
                    configSetting.StartCoroutine(DelayedCo(configSetting));
                }
            }

            #endregion

            #region Add OFF to mosaic dropdown

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ConfigSetting), nameof(ConfigSetting.Awake))]
            private static void ConfigSetting_Awake_Postfix(ConfigSetting __instance, UIPopupList ___ConfBt_MosaicType_List, UIPopupList ___HS_QS09)
            {
                if (_enableOnStart)
                {
                    ConfigClass.MosaicSetting = NoMosaicId;
                    _enableOnStart = false;
                }

                ___ConfBt_MosaicType_List.AddItem(NoMosaicStr);
                ___HS_QS09.AddItem(NoMosaicStr);
            }

            /// <summary>
            /// Fix the setting getting clamped just before ConfigSetting.MosaicSetting is called
            /// </summary>
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
                    ConfigClass.MosaicSetting = NoMosaicId;
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

                UpdateXrayMosaicState(___DanmenCamera);
            }

            #endregion
        }
    }
}
