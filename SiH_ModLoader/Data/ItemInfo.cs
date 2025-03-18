using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SiH_ModLoader.Data
{
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
                    SummerHeatModLoaderPlugin.Logger.LogError("Missing thumbnail " + path);
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
                                currentItem.Hide_Wear_Full.Add(new KeyValuePair<int, HideWearKind>(int.Parse(split[0]), GetHideWearKind(split[1], true)));
                                break;
                            case 2:
                                if (line.Length < 2) throw new InvalidDataException("Hide_Wear_Half data line must have at least 1 comma");
                                currentItem.Hide_Wear_Half.Add(new KeyValuePair<int, HideWearKind>(int.Parse(split[0]), GetHideWearKind(split[1], true)));
                                break;
                            default:
                                throw new InvalidDataException("No **section found before data line");
                        }
                    }
                }
                catch (Exception ex)
                {
                    SummerHeatModLoaderPlugin.Logger.LogError($"Error on line {index + 1} [ {line} ]\n{(ex is InvalidDataException ? ex.Message : ex.ToString())}");
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
                // TODO magenta tex kills
                Bundle.Unload(true);
                Object.Destroy(Bundle);
            }
            Bundle = null;
        }
    }
}
