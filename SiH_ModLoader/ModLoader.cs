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

namespace SiH_ModLoader
{
    [BepInPlugin(GUID, DisplayName, Version)]
    //[BepInDependency(ConfigurationManager.ConfigurationManager.GUID, ConfigurationManager.ConfigurationManager.Version)]
    [BepInDependency("gravydevsupreme.xunity.autotranslator", BepInDependency.DependencyFlags.SoftDependency)]
    public class SummerHeatTweaksPlugin : BaseUnityPlugin
    {
        private static List<IDisposable> _cleanup = new List<IDisposable>();

        public const string GUID = "SiH_ModLoader";
        public const string DisplayName = Constants.Name;
        public const string Version = Constants.Version;

        internal static new ManualLogSource Logger;

        private static ConfigEntry<KeyboardShortcut> _test;

        protected void Awake()
        {
            Logger = base.Logger;

            _test = Config.Bind("General", "test", new KeyboardShortcut(KeyCode.H));

            _cleanup.Add(Harmony.CreateAndPatchAll(typeof(Hooks)));
        }

        /// <summary>
        /// to handle:
        /// inject into ModelList
        /// AssetBundleUnload
        /// 
        /// </summary>

        private string customModelList = @"

//■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
//TestItem[Test]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

// Lines that contain '//' at any position will be ignored
// Functional lines should not have any spaces, including at the end

// Begin info about a new item
// CTTestItem_01 is the actual object name that is saved to the card, **ID_ is trimmed (but always required).  ??? CT is a prefix specifying type of clothes/item, required. ???
// CT - Clothes Top
// CB - Clothes Bottom
// CF - Clothes Full (one-piece)
// UT - Underwear Top
// UB - Underwear Bottom
// UF - Underwear Full (one-piece)
// SX - Socks
// FT - Footwear
// GV - Gloves

**ID_CTTestItem_01

// Specify resource names of prefabs with meshes to load
// The asset names are constructed as follows: (""Assets/Data_AssetBundle/Model/CH/"" + the name below + "".prefab"")
// First is name of the prefab (CT02_01), the number is the sub material ID (0). The ID can be left out if the feature is not used (leave only CT02_01).
// The ID needs MeshPrefabData component to be on the prefab, that's where material names are looked up with the ID.
// The names of the meshes should start with the type indicator (CT, CB, etc) because it's used by the game to know which old meshes to remove when changing clothes.
// TODO: sub materials are not implemented yet

// The _(D) suffix means it's for partially undressed state. There's also _(W) and _(Z) - check UpdateMeshShowIE in dnSpy to see how they work exactly.
// No suffix means the mesh is for the normally dressed state. It's hidden in any other state.

**LoadModelList
CT03_01,1
CT03_01_(D),1
//CT02_01,0
//CT02_01_(D),0
CT02_02,0
CT02_02_(D),0
CT02_03_ribon,0
CT02_03_ribon_(D),0
CT02_03_ribonPoint,0


// which visibility toggles the above meshes are linked to.

// The number is kind of weird, it controls which objects under RootBone are to be displayed in each state. The object(transform) name is made by adding any of the below + the number, eg. UT01_01_bra02
// { ""BD00"", ""GV01_01"", ""UT01_01_bra"", ""UF01_01_top"", ""UT20_03_parts"", ""UT20_02_bondage"", ""UT11_02_FrilRibon"", ""UB01_01_panty"", ""UF01_01_under"" };
// Most commonly BD00 is used to hide body parts to avoid clothes clipping.
// The second parameter can be either Tops or Bottom 

**Hide_Wear_Full
01,Tops
02,Tops
03,Tops
08,Tops
09,Tops
18,Tops
19,Tops

// Same as above except for the half undressed state

**Hide_Wear_Half
08,Tops
";
        //d.CosTops = "CTTestItem_01";
        //d.UpdateMeshList_Text("CTTestItem_01", new string[] { "CT", "CF" });
        //d.UpdateMeshRenderer();

        protected void Start()
        {
            foreach (var dic in FindObjectsOfType<CustomDataInputCustomManager>())
            {
                var orig = dic.ModelList;
                _cleanup.Add((Disposable)(() => dic.ModelList = orig));
                dic.ModelList = orig.AddRangeToArray(customModelList.Split('\n'));
            }
        }

        protected void Update()
        {
            if (_test.Value.IsDown())
            {
                Logger.LogMessage("reload");
            }
        }

        protected void OnEnable()
        {

        }
        protected void OnDisable()
        {
            foreach (var disposable in _cleanup)
            {
                disposable?.Dispose();
            }
        }

        private static class Hooks
        {
        }
    }

    public class Disposable : IDisposable
    {
        private Action _dispose;
        public Disposable(Action dispose)
        {
            _dispose = dispose;
        }
        public void Dispose()
        {
            _dispose();
        }

        public static implicit operator Disposable(Action dispose)
        {
            return new Disposable(dispose);
        }
    }
}
