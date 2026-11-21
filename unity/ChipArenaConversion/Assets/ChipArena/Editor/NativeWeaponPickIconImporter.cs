using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace ChipArena.UnityConversion.Editor
{
    // Bake with the same rasterizer as the HUD. Each card can then scale an already
    // imported texture through the deal, reveal and selection flourish.
    [ScriptedImporter(1, "weaponpickicons")]
    public sealed class NativeWeaponPickIconImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            const string root = "Assets/Conversion/Resources/";
            const string artPath = root + "ClassIcons.json";
            const string linePath = root + "Icons.json";
            ctx.DependsOnSourceAsset(artPath);
            ctx.DependsOnSourceAsset(linePath);
            ctx.DependsOnSourceAsset("Assets/ChipArena/Core/NativeIconRaster.cs");
            var art = JsonUtility.FromJson<NativeIcons.ArtSet>(File.ReadAllText(artPath));
            var lines = JsonUtility.FromJson<NativeIcons.IconSet>(File.ReadAllText(linePath));
            var wanted = new HashSet<string>(StringComparer.Ordinal) { art.fallback, "shuffle", "cpu" };
            AddResolved(art.classes, lines.classes);
            AddResolved(art.specs, lines.specs);
            void AddResolved(NativeIcons.IconEntry[] preferred, NativeIcons.IconEntry[] fallback)
            {
                var mapped = new HashSet<string>(StringComparer.Ordinal);
                foreach (var e in preferred) { mapped.Add(e.id); wanted.Add(e.icon); }
                foreach (var e in fallback) if (!mapped.Contains(e.id)) wanted.Add(e.icon);
            }

            var index = new TextAsset("Weapon pick icon textures");
            ctx.AddObjectToAsset("index", index);
            ctx.SetMainObject(index);
            foreach (var e in art.icons)
            {
                if (!wanted.Remove(e.id)) continue;
                Bake(e.id, e.markup, 256, art.box, 1, NativeIconRaster.Mode.Art);
                if (!string.IsNullOrEmpty(e.small))
                    Bake(e.id + "-small", e.small, art.smallPx, art.box, 1, NativeIconRaster.Mode.Art);
            }
            foreach (var e in lines.icons)
                if (wanted.Remove(e.id))
                    Bake(e.id, e.markup, 256, 24, lines.strokeWidth, NativeIconRaster.Mode.Lucide);
            if (wanted.Count != 0)
                throw new InvalidDataException("Missing weapon pick artwork: " + string.Join(", ", wanted));

            void Bake(string id, string svg, int size, float box, float stroke, NativeIconRaster.Mode mode)
            {
                var rgba = NativeIconRaster.Raster(NativeIconRaster.Parse(svg, stroke, mode), size, box);
                var pixels = new Color32[size * size];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color(rgba[4*i], rgba[4*i+1], rgba[4*i+2], rgba[4*i+3]);
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
                {
                    name = id, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear
                };
                texture.SetPixels32(pixels);
                texture.Apply(true, false);
                ctx.AddObjectToAsset(id, texture);
            }
        }
    }
}
