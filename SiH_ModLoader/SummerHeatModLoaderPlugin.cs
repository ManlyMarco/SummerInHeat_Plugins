using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SiH_ModLoader.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SiH_ModLoader
{
    // Code to set a custom item in the game
    //d.CosTops = "CTTestItem_01";
    //d.UpdateMeshList_Text("CTTestItem_01", new string[] { "CT", "CF" });
    //d.UpdateMeshRenderer();

    [BepInPlugin(GUID, DisplayName, Version)]
    //[BepInDependency("gravydevsupreme.xunity.autotranslator", BepInDependency.DependencyFlags.SoftDependency)]
    public class SummerHeatModLoaderPlugin : BaseUnityPlugin
    {
        public const string GUID = "SiH_ModLoader";
        public const string DisplayName = Constants.Name;
        public const string Version = Constants.Version;

        internal static new ManualLogSource Logger;

        private static ConfigEntry<KeyboardShortcut> _reloadHotkey;
        private Harmony _hi;

        private static readonly Dictionary<string, ItemInfo> _CustomItems = new Dictionary<string, ItemInfo>();
        private static readonly Dictionary<string, ItemInfo> _ModelNameLookup = new Dictionary<string, ItemInfo>();

        private static AssetBundleSystem _assetBundleSystem;

        private static readonly Dictionary<ItemKind, string> _CatalogPaths = new Dictionary<ItemKind, string>()
        {
            { ItemKind.CT, @"UI Root (Custom)/CustomPanel/Catalog05Main/CostumeCatalog_Tops/Catalog/Tops" },
            { ItemKind.CB, @"UI Root (Custom)/CustomPanel/Catalog05Main/CostumeCatalog_Bottom/Catalog/Bottom" },
            { ItemKind.CF, @"UI Root (Custom)/CustomPanel/Catalog05Main/CostumeCatalog_OnePiece/Catalog/OnePiece" },
            //todo others
        };

        protected void Awake()
        {
            Logger = base.Logger;

            _hi = Hooks.ApplyHooks();

            _reloadHotkey = Config.Bind("General", "Reload mods", new KeyboardShortcut(KeyCode.H));
        }

        //protected void Start()
        //{
        //    LoadMods();
        //}

        protected void Update()
        {
            if (_reloadHotkey.Value.IsDown())
            {
                Logger.LogMessage("Reloading mods");
                StartCoroutine(ReloadMods());
            }
        }

        private static IEnumerator ReloadMods()
        {
            // TODO gracefully handle reloading mods that are currently in use
            CleanUp();
            yield return null;
            LoadMods();
        }

        protected void OnDestroy()
        {
            CleanUp();

            _hi?.UnpatchSelf();
        }

        private static readonly List<IDisposable> _Cleanup = new List<IDisposable>();
        private static void CleanUp()
        {
            foreach (var item in _CustomItems)
                item.Value.Dispose();

            _CustomItems.Clear();

            foreach (var disposable in _Cleanup)
            {
                try
                {
                    disposable?.Dispose();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            _Cleanup.Clear();
        }

        private static void LoadMods()
        {
            _assetBundleSystem = GameObject.Find("AssetBundleSystem").GetComponent<AssetBundleSystem>();

            _CustomItems.Clear();

            var customModelList = new List<string>();

            var lists = Directory.GetFiles(Path.Combine(Paths.GameRootPath, "mods"), "ModelList.txt", SearchOption.AllDirectories);
            foreach (var list in lists)
            {
                try
                {
                    var modDir = new DirectoryInfo(Path.GetDirectoryName(list) ?? throw new DirectoryNotFoundException($"Could not find directory for {list}"));

                    var assetbundle = Path.Combine(modDir.FullName, "assets");
                    if (!File.Exists(assetbundle))
                    {
                        Logger.LogError($"AssetBundle not found: {assetbundle}");
                        continue;
                    }
                    var ab = AssetBundle.LoadFromFile(assetbundle);
                    if (ab == null) throw new ArgumentException("Failed to load AssetBundle from " + assetbundle);

                    var modelList = File.ReadAllLines(list);
                    var itemInfos = ItemInfo.ParseModelList(modelList);

                    foreach (var itemInfo in itemInfos)
                    {
                        itemInfo.Bundle = ab;
                        itemInfo.ModDirectory = modDir;
                        _CustomItems[itemInfo.Name] = itemInfo;
                        foreach (var loadModel in itemInfo.LoadModelList)
                            _ModelNameLookup[loadModel.Key] = itemInfo;
                    }


                    customModelList.AddRange(modelList);

                    // todo make a hook for adding custom items to the item list in game
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error loading model list: {list} - state might be corrupted!\n{e}");
                }
            }

            // Update model lists in each character
            // TODO make a hook for this
            foreach (var dic in FindObjectsOfType<CustomDataInputCustomManager>())
            {
                var orig = dic.ModelList;
                _Cleanup.Add((Disposable)(() => dic.ModelList = orig));

                dic.ModelList = orig.Concat(customModelList).ToArray();
            }

            // Add custom items to the catalog scrollviews in maker
            foreach (var path in _CatalogPaths)
            {
                var items = _CustomItems.Values.Where(x => x.Kind == path.Key).ToArray();
                if (items.Length > 0)
                {
                    var go = GameObject.Find(path.Value);
                    if (go == null)
                    {
                        Logger.LogError($"Could not find catalog path: {path.Value}");
                        continue;
                    }

                    // Find the last item in the list
                    Transform last = null, select = null;
                    foreach (Transform child in go.transform)
                    {
                        if (child.name == "Select")
                        {
                            select = child;
                            continue;
                        }

                        if (!child.name.StartsWith("Sprite"))
                            continue;

                        if (last)
                        {
                            var childPos = child.localPosition;
                            var lastPos = last.localPosition;
                            if (Mathf.Abs(childPos.y - lastPos.y) < 0.1f && childPos.x > lastPos.x || childPos.y < lastPos.y)
                                last = child;
                        }
                        else
                        {
                            last = child;
                        }
                    }

                    if (!last)
                        throw new InvalidOperationException($"Could not find last item in catalog {path.Key} > {path.Value}");

                    const int columnCount = 5;
                    const int columnSpacing = 73;
                    const int rowSpacing = -73;

                    // Add new items by copying the last item and changing the name and sprite. No need to change the event, the sprite name is used to figure out what item it is.
                    foreach (var item in items)
                    {
                        var newItem = Instantiate(last, last.parent);

                        newItem.name = $"Sprite ({item.Name})"; // todo must be a sequential number?

                        var uiSprite = newItem.GetComponent<UISprite>();
                        var thumb = item.Thumbnail;
                        if (thumb != null)
                        {
                            var atlasCopy = (INGUIAtlas)Instantiate((Object)uiSprite.atlas);
                            atlasCopy.spriteMaterial = Instantiate(atlasCopy.spriteMaterial);
                            atlasCopy.spriteMaterial.mainTexture = thumb;
                            atlasCopy.spriteList.Clear();
                            atlasCopy.spriteList.Add(new UISpriteData() { x = 0, y = 0, height = 70, width = 70, name = item.Name });
                            uiSprite.mSpriteName = null; // Will be set to item.Name by atlas setter
                            uiSprite.atlas = atlasCopy;

                            uiSprite.SetRect(0, 0, 70, 70); // todo still necessary?
                        }
                        else
                        {
                            uiSprite.mSpriteName = item.Name; // Necessary because game uses this to figure out what item it is referring to on click
                        }

                        // Must set position after done messing with UISprite because it overrides the position in some places
                        var col = Mathf.RoundToInt(last.localPosition.x) / columnSpacing + 1;
                        if (col >= columnCount)
                        {
                            // Last column, start a new row
                            col = 0;
                            newItem.localPosition = new Vector3(col * columnSpacing, last.localPosition.y + rowSpacing, last.localPosition.z);
                        }
                        else
                        {
                            newItem.localPosition = new Vector3(col * columnSpacing, last.localPosition.y, last.localPosition.z);
                        }

                        _Cleanup.Add((Disposable)newItem);
                    }

                    // Always last in stock lists
                    select?.SetAsLastSibling();
                }
            }
        }

        private static class Hooks
        {
            /// <summary>
            /// to handle:
            /// inject into ModelList
            /// AssetBundleUnload
            /// </summary>
            public static Harmony ApplyHooks()
            {
                var hi = Harmony.CreateAndPatchAll(typeof(Hooks));
                return hi;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(AssetBundle), nameof(AssetBundle.LoadAsset), typeof(string), typeof(Type))]
            private static void LoadAssetPostfixHook(AssetBundle __instance, string name, Type type, ref Object __result)
            {
                if (type != typeof(GameObject) || __result != null || __instance != _assetBundleSystem.BundleData_ObjData) return;

                var prefix = CustomDataInputCustomManager.Path;
                var postfix = ".prefab";
                var trimmedName = name.Substring(prefix.Length, name.Length - prefix.Length - postfix.Length);

                if (_ModelNameLookup.TryGetValue(trimmedName, out var item))
                {
                    __result = item.Bundle.LoadAsset<GameObject>(trimmedName); // todo use trimmedname or full name?
                }
                else
                {
                    Logger.LogWarning($"Could not find model in any ModelList: {trimmedName} (full: {name})");
                }
            }
        }
    }
}
