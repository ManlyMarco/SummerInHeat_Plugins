using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SiH_ModLoader
{
    [BepInPlugin(GUID, DisplayName, Version)]
    //[BepInDependency(ConfigurationManager.ConfigurationManager.GUID, ConfigurationManager.ConfigurationManager.Version)]
    [BepInDependency("gravydevsupreme.xunity.autotranslator", BepInDependency.DependencyFlags.SoftDependency)]
    public class SummerHeatModLooaderPlugin : BaseUnityPlugin
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

            _cleanup.Add(Hooks.ApplyHooks());

            _cleanup.Add((Disposable)(() =>
            {
                foreach (var item in _CustomItems)
                    item.Value.Dispose();

                _customModelList.Clear();
                _CustomItems.Clear();
            }));
        }

        /// <summary>
        /// to handle:
        /// inject into ModelList
        /// AssetBundleUnload
        /// 
        /// </summary>

        private readonly List<string> _customModelList = new List<string>();

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

        /*@"

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


// Which visibility toggles the above meshes are linked to.

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
";*/
        //d.CosTops = "CTTestItem_01";
        //d.UpdateMeshList_Text("CTTestItem_01", new string[] { "CT", "CF" });
        //d.UpdateMeshRenderer();

        protected void Start()
        {
            LoadMods();
        }

        private void LoadMods()
        {
            _assetBundleSystem = GameObject.Find("AssetBundleSystem").GetComponent<AssetBundleSystem>();

            _customModelList.Clear();
            _CustomItems.Clear();

            var lists = Directory.GetFiles(Path.Combine(Paths.GameRootPath, "mods"), "ModelList.txt", SearchOption.AllDirectories);
            foreach (var list in lists)
            {
                try
                {
                    var modDir = Path.GetDirectoryName(list) ?? throw new DirectoryNotFoundException($"Could not find directory for {list}");

                    var assetbundle = Path.Combine(modDir, "assets");
                    if (!File.Exists(assetbundle))
                    {
                        Logger.LogError($"AssetBundle not found: {assetbundle}");
                        continue;
                    }
                    var ab = AssetBundle.LoadFromFile(assetbundle);
                    if (ab == null) throw new ArgumentException("Failed to load AssetBundle from " + assetbundle);
                    //_cleanup.Add((Disposable)(() => ab.Unload(true)));

                    var modelList = File.ReadAllLines(list);
                    var itemInfos = ItemInfo.ParseModelList(modelList);

                    foreach (var itemInfo in itemInfos)
                    {
                        itemInfo.Bundle = ab;
                        _CustomItems[itemInfo.Name] = itemInfo;
                        foreach (var loadModel in itemInfo.LoadModelList)
                            _ModelNameLookup[loadModel.Key] = itemInfo;
                    }


                    _customModelList.AddRange(modelList);

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
                _cleanup.Add((Disposable)(() => dic.ModelList = orig));

                dic.ModelList = orig.Concat(_customModelList).ToArray();
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

                        newItem.name = $"Sprite ({item.Name})"; // todo must be a sequential number?

                        var uiSprite = newItem.GetComponent<UISprite>();
                        uiSprite.spriteName = item.Name; // Necessary because game uses this to figure out what item it is referring to on click
                        var thumb = item.Thumbnail;
                        if (thumb != null)
                        {
                            uiSprite.mainTexture = thumb;
                            uiSprite.SetRect(0, 0, 70, 70); // todo do this first?
                        }

                        _cleanup.Add((Disposable)newItem);
                    }

                    // Always last in stock lists
                    select?.SetAsLastSibling();
                }
            }
        }


        protected void Update()
        {
            if (_test.Value.IsDown())
            {
                Logger.LogMessage("Reloading mods");
                OnDestroy();
                LoadMods();
            }
        }

        protected void OnDestroy()
        {
            foreach (var disposable in _cleanup)
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
            _cleanup.Clear();
        }

        private static class Hooks
        {
            public static Harmony ApplyHooks()
            {
                var hi = Harmony.CreateAndPatchAll(typeof(Hooks));
                hi.Patch(original: AccessTools.Method(typeof(AssetBundle), nameof(AssetBundle.LoadAsset), new[] { typeof(string) }, new[] { typeof(GameObject) }),
                         postfix: new HarmonyMethod(typeof(Hooks), nameof(LoadAssetPostfixHook)));
                return hi;
            }

            //public T LoadAsset<T>(string name) where T : Object
            //public GameObject LoadAsset<GameObject>(string name)
            private static void LoadAssetPostfixHook(AssetBundle __instance, string name, ref GameObject __result)
            {
                if (__result != null || __instance != _assetBundleSystem.BundleData_ObjData) return;

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

    public class ItemInfo : IDisposable
    {
        private Texture2D _thumbnail;

        public ItemInfo(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (id.Length < 6) throw new ArgumentException("ID must be at least 6 characters long");
            if (!id.StartsWith("ID_")) throw new ArgumentException("ID must start with ID_ and 2 character code for item kind e.g. ID_CTxxxx");

            ID = id;
            Name = id.Substring(3);

            var kind = GetItemKind(Name, true);
            Kind = kind;
        }

        public static ItemKind GetItemKind(string name, bool throwOnUnknown)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (name.Length < 3) throw new ArgumentException("Name must be at least 3 characters long");

            var kindStr = name.Substring(0, 2);
            if (!Enum.TryParse<ItemKind>(kindStr, out var kind) || kind == ItemKind.Unknown)
            {
                if (throwOnUnknown) throw new InvalidDataException($"Unknown item kind: {kindStr}");
                return ItemKind.Unknown;
            }
            return kind;
        }

        public static HideWearKind GetHideWearKind(string name, bool throwOnUnknown)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (!Enum.TryParse<HideWearKind>(name, out var kind) || kind == HideWearKind.Unknown)
            {
                if (throwOnUnknown) throw new InvalidDataException($"Unknown hide wear kind: {name}");
                return HideWearKind.Unknown;
            }
            return kind;
        }

        /// <summary>
        /// Kind of the item, used to determine which maker list to add it to.
        /// </summary>
        public ItemKind Kind { get; }

        /// <summary>
        /// Trimmed name of the item taken from the ID, used by the game for identification.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Full ID_Name as in the file
        /// </summary>
        public string ID { get; }
        public List<KeyValuePair<string, int>> LoadModelList { get; } = new List<KeyValuePair<string, int>>();
        public List<KeyValuePair<int, HideWearKind>> Hide_Wear_Full { get; } = new List<KeyValuePair<int, HideWearKind>>();
        public List<KeyValuePair<int, HideWearKind>> Hide_Wear_Half { get; } = new List<KeyValuePair<int, HideWearKind>>();
        public AssetBundle Bundle { get; set; }

        public Texture2D Thumbnail
        {
            get
            {
                if (_thumbnail != null) return _thumbnail;

                var path = Path.Combine(ModDirectory.FullName, ID + ".png");
                if (!File.Exists(path))
                {
                    SummerHeatModLooaderPlugin.Logger.LogError("Missing thumbnail " + path);
                    return null;
                }

                var t2d = new Texture2D(5, 5, TextureFormat.ARGB32, false);
                t2d.LoadImage(File.ReadAllBytes(path));
                return _thumbnail = t2d;
            }
        }

        public DirectoryInfo ModDirectory { get; set; }

        public static List<ItemInfo> ParseModelList(string path) => ParseModelList(File.ReadAllLines(path));

        public static List<ItemInfo> ParseModelList(string[] lines)
        {
            var items = new List<ItemInfo>();
            ItemInfo currentItem = null;
            var currentSection = -1;
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (string.IsNullOrWhiteSpace(line) || line.Contains("//"))
                    continue;
                try
                {
                    if (line.Contains(' '))
                        throw new InvalidDataException("No spaces allowed in line");

                    if (line.StartsWith("**"))
                    {
                        var name = line.Substring(2);

                        if (name.StartsWith("ID_"))
                        {
                            currentItem = new ItemInfo(name);
                            items.Add(currentItem);
                            currentSection = -1;
                        }
                        else
                        {
                            if (currentItem == null)
                                throw new InvalidDataException("No item **ID_ found before **section line");

                            switch (name)
                            {
                                case "LoadModelList":
                                    currentSection = 0;
                                    break;
                                case "Hide_Wear_Full":
                                    currentSection = 1;
                                    break;
                                case "Hide_Wear_Half":
                                    currentSection = 2;
                                    break;
                                default:
                                    throw new InvalidDataException("Unknown **section name");
                            }
                        }
                    }
                    else
                    {
                        if (currentItem == null)
                            throw new InvalidDataException("No item **ID_ found before data line");

                        var split = line.Split(',');
                        if (split.Length > 2)
                            throw new InvalidDataException("Too many commas in line");
                        switch (currentSection)
                        {
                            case 0:

                                var modelName = split[0];
                                if (GetItemKind(modelName, false) == ItemKind.Unknown)
                                    throw new InvalidDataException("Unknown item kind in model name. All model names should start with a valid two-letter item kind e.g. CTxx_yy"); //todo allow and only log warning?

                                currentItem.LoadModelList.Add(new KeyValuePair<string, int>(modelName, split.Length == 2 ? int.Parse(split[1]) : 0));
                                break;
                            case 1:
                                if (line.Length < 2) throw new InvalidDataException("Hide_Wear_Full data line must have at least 1 comma");
                                currentItem.Hide_Wear_Full.Add(new KeyValuePair<int, HideWearKind>(int.Parse(split[0]), ItemInfo.GetHideWearKind(split[1], true)));
                                break;
                            case 2:
                                if (line.Length < 2) throw new InvalidDataException("Hide_Wear_Half data line must have at least 1 comma");
                                currentItem.Hide_Wear_Half.Add(new KeyValuePair<int, HideWearKind>(int.Parse(split[0]), ItemInfo.GetHideWearKind(split[1], true)));
                                break;
                            default:
                                throw new InvalidDataException("No **section found before data line");
                        }
                    }
                }
                catch (Exception ex)
                {
                    SummerHeatModLooaderPlugin.Logger.LogError($"Error on line {index + 1} [ {line} ]\n{(ex is InvalidDataException ? ex.Message : ex.ToString())}");
                }
            }

            return items;
        }

        public void Dispose()
        {
            if (_thumbnail)
            {
                Object.Destroy(_thumbnail);
            }
            _thumbnail = null;

            if (Bundle)
            {
                Bundle.Unload(true);
                Object.Destroy(Bundle);
            }
            Bundle = null;
        }
    }

    public enum ItemKind
    {
        Unknown = 0,
        CT = 1, //Clothes Top
        CB = 2, //Clothes Bottom
        CF = 3, //Clothes Full (one-piece)
        UT = 10, //Underwear Top
        UB = 11, //Underwear Bottom
        UF = 12, //Underwear Full (one-piece)
        SX = 20, //Socks
        FT = 21, //Footwear
        GV = 22, //Gloves
    }

    public enum HideWearKind
    {
        Unknown = 0,
        Tops = 1,
        Bottom = 2,
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

        public static implicit operator Disposable(UnityEngine.Object obj)
        {
            return new Disposable(() => UnityEngine.Object.Destroy(obj));
        }
    }
}
