using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace SiH_Uncensor
{
    public static class TextureReplacer
    {
        private static string _imagesPath;
        private static Dictionary<string, string> _pathLookup;
        private static Dictionary<string, Texture2D> _texLookup;
        private static Shader _replacementShader;

        private static Shader ReplacementShader
        {
            get
            {
                if (!_replacementShader)
                {
                    _replacementShader = Shader.Find("Miconisomi/ASE_Miconisomi_VerTex");
                    if (_replacementShader == null) SummerHeatUncensorPlugin.Logger.Log(LogLevel.Error, "Failed to find replacement shader");
                }
                return _replacementShader;
            }
        }

        public static void ReloadReplacementImages(string pluginLocation)
        {
            _imagesPath = Path.GetDirectoryName(pluginLocation);
            if (string.IsNullOrEmpty(_imagesPath))
                _imagesPath = Paths.PluginPath;
            _imagesPath = Path.Combine(_imagesPath, "replacements");

            var files = Directory.GetFiles(_imagesPath, "*.png", SearchOption.TopDirectoryOnly);
            _pathLookup = files.ToDictionary(Path.GetFileNameWithoutExtension, x => x);
            _texLookup = new Dictionary<string, Texture2D>();

            SummerHeatUncensorPlugin.Logger.Log(LogLevel.Debug, $"Found {files.Length} replacement images:\n{string.Join("\n", files)}");
        }

        public static void ReplaceMaterialsAndTextures(Renderer[] renderers)
        {
            int hitsShd = 0, hitsTex = 0;

            foreach (var renderer in renderers)
                ReplaceMaterialsAndTextures(renderer, ref hitsShd, ref hitsTex);

            SummerHeatUncensorPlugin.Logger.Log(LogLevel.Debug, $"Replaced {hitsShd} shaders and {hitsTex} textures in {renderers.Length} renderers");
        }

        private static void ReplaceMaterialsAndTextures(Renderer renderer)
        {
            int hitsShd = 0, hitsTex = 0;
            ReplaceMaterialsAndTextures(renderer, ref hitsShd, ref hitsTex);

            SummerHeatUncensorPlugin.Logger.Log(LogLevel.Debug, $"Renderer={renderer} -> replaced shader={(hitsShd == 0 ? "No" : "Yes")} texture={(hitsTex == 0 ? "No" : "Yes")}");
        }

        private static void ReplaceMaterialsAndTextures(Renderer renderer, ref int hitsShd, ref int hitsTex)
        {
            if (!renderer) return;

            var material = renderer.sharedMaterial ?? renderer.material;
            if (!material) return;
            var shaderName = material.shader.name;
            if (shaderName == "Miconisomi/ASE_Miconisomi_VerTex_Moza")
            {
                material.shader = ReplacementShader;
                hitsShd++;
            }

            var validTarget = shaderName == "Miconisomi/Danmen" || // xray window
                              shaderName.StartsWith("Miconisomi/ASE_Miconisomi_VerTex"); // characters
            if (!validTarget) return;

            var mainTexture = material.mainTexture;
            if (!mainTexture) return;
            var mainTextureName = mainTexture.name;
            if (_pathLookup.TryGetValue(mainTextureName, out var bytes))
            {
                _texLookup.TryGetValue(mainTextureName, out var replacement);
                if (!replacement)
                {
                    replacement = new Texture2D(2, 2);
                    replacement.LoadImage(File.ReadAllBytes(bytes));
                    _texLookup[mainTextureName] = replacement;
                }

                if (material.mainTexture != replacement)
                {
                    material.mainTexture = replacement;
                    hitsTex++;
                }
            }
        }
    }
}
