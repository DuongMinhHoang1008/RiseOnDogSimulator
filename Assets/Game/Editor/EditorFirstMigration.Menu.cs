using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DogSimulator.Editor
{
    public static partial class EditorFirstMigration
    {
        private const string MenuAssets = "Assets/Game/UI/EditorAuthored";

        private static Sprite SaveMenuSprite(Sprite sprite)
        {
            var path = $"{MenuAssets}/{sprite.name.Replace(' ', '_')}.asset";
            var existing = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().SingleOrDefault();
            if (existing != null)
            {
                var texture = sprite.texture;
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                return existing;
            }
            AssetDatabase.CreateAsset(sprite.texture, path);
            AssetDatabase.AddObjectToAsset(sprite, path);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
        }

        private static TMP_FontAsset MenuFont()
        {
            var path = $"{MenuAssets}/Sniglet_SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var source = Resources.Load<Font>("DogMenu/Sniglet");
            if (source == null) return TMP_Settings.defaultFontAsset;
            var font = TMP_FontAsset.CreateFontAsset(source);
            font.name = "Sniglet SDF";
            if (TMP_Settings.defaultFontAsset != null)
                font.fallbackFontAssetTable = new() { TMP_Settings.defaultFontAsset };
            AssetDatabase.CreateAsset(font, path);
            AssetDatabase.AddObjectToAsset(font.material, path);
            foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, path);
            EditorUtility.SetDirty(font);
            return font;
        }

        private static void EnsureMenuAssetFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/UI")) AssetDatabase.CreateFolder("Assets/Game", "UI");
            if (!AssetDatabase.IsValidFolder(MenuAssets)) AssetDatabase.CreateFolder("Assets/Game/UI", "EditorAuthored");
        }
    }
}
