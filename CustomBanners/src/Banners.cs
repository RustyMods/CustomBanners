using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using PieceManager;
using UnityEngine;
using YamlDotNet.Serialization;
using Object = UnityEngine.Object;

namespace CustomBanners;

public static class Banners
{
    private static readonly MethodInfo LoadImage = AccessTools.Method(typeof(ImageConversion), nameof(ImageConversion.LoadImage), new [] { typeof(Texture2D), typeof(byte[]) });
    public static bool LoadImage4x(this Texture2D tex, byte[] data)
    {
        return (bool)LoadImage.Invoke(null, [tex , data]);
    }
    
    private static readonly Dictionary<string, Texture2D> textures = new();
    private static readonly List<BannerData> configurations = [];

    private static readonly GameObject sourcePrefab = PiecePrefabManager.RegisterAssetBundle("custom_banners").LoadAsset<GameObject>("piece_custom_banner");

    public static void Init()
    {
        var dirPath = Path.Combine(Paths.ConfigPath, "CustomBanners");
        Directory.CreateDirectory(dirPath);
        
        var imagePaths = Directory.GetFiles(dirPath, "*.png", SearchOption.AllDirectories);
        for (var index = 0; index < imagePaths.Length; ++index)
        {
            var imagePath = imagePaths[index];
            try
            {
                byte[] bytes = File.ReadAllBytes(imagePath);
                var (w, h) = GetPngSize(bytes);
                Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.LoadImage4x(bytes);
                tex.Apply();
                var filename = Path.GetFileNameWithoutExtension(imagePath);
                tex.name = filename;
                textures.Add(filename, tex);
            }
            catch
            {
                CustomBannersPlugin.CustomBannersLogger.LogWarning("Failed to load image: " +
                                                                   Path.GetFileName(imagePath));
            }
        }

        var bannerPaths = Directory.GetFiles(dirPath, "*.yml", SearchOption.AllDirectories);
        var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        for (var index = 0; index < bannerPaths.Length; ++index)
        {
            var bannerPath = bannerPaths[index];
            try
            {
                var txt = File.ReadAllText(bannerPath);
                var dat = deserializer.Deserialize<BannerData>(txt);
                configurations.Add(dat);
            }
            catch
            {
                CustomBannersPlugin.CustomBannersLogger.LogWarning("Failed to deserialize banner data: " +
                                                                   Path.GetFileName(bannerPath));
            }
        }
    }

    public static void CreateBanners()
    {
        if (BuildPiece.TryGetPrefab("piece_banner01", out GameObject piece_banner01))
        {
            var woodBeamMats = piece_banner01.transform.Find("woodbeam").GetComponent<MeshRenderer>().sharedMaterials;
            var bannerMat = piece_banner01.transform.Find("default").gameObject.GetComponent<MeshRenderer>().sharedMaterials[0];

            var bannerPiece = piece_banner01.GetComponent<Piece>();
            var bannerWear = piece_banner01.GetComponent<WearNTear>();

            var floats = bannerMat.GetPropertyNames(MaterialPropertyType.Float);
            var floatProps = new Dictionary<string, float>();
            foreach (var prop in floats)
            {
                var value = bannerMat.GetFloat(prop);
                floatProps.Add(prop, value);
            }
            
            for (var index = 0; index < configurations.Count; ++index)
            {
                var dat = configurations[index];
                if (!textures.TryGetValue(dat.image, out Texture2D tex)) continue;

                if (BuildPiece._scene.m_prefabs.Exists(p => p.name == dat.id) ||
                    BuildPiece.registeredPieces.Exists(bp => bp.Prefab.name == dat.id))
                {
                    CustomBannersPlugin.CustomBannersLogger.LogWarning(dat.id + " already exists, skipping");
                    continue;
                }

                var prefab = Object.Instantiate(sourcePrefab, CustomBannersPlugin.m_root.transform);
                prefab.name = dat.id;

                var piece = prefab.GetComponent<Piece>();
                piece.m_name = "$piece_" + dat.id;
                piece.m_description = "$piece_" + dat.id + "_desc";
                piece.m_placeEffect = bannerPiece.m_placeEffect;

                var wnt = prefab.GetComponent<WearNTear>();
                wnt.m_hitEffect = bannerWear.m_hitEffect;
                wnt.m_destroyedEffect = bannerWear.m_destroyedEffect;

                prefab.transform.Find("woodbeam").GetComponent<MeshRenderer>().sharedMaterials = woodBeamMats;

                var bannerRenderer = prefab.transform.Find("default").GetComponent<MeshRenderer>();
                var material = new Material(bannerRenderer.sharedMaterials[0]);
                
                material.shader = bannerMat.shader;
                foreach (KeyValuePair<string, float> kvp in floatProps)
                {
                    material.SetFloat(kvp.Key, kvp.Value);
                }
                
                material.mainTexture = tex;
                bannerRenderer.sharedMaterials = [material];

                BuildPiece build = new BuildPiece(prefab);
                build.Category.Set("Banners");
                build.RequiredItems.Requirements.AddRange(dat.requirements);
                build.Crafting.Set(CraftingTable.Workbench);

                if (!dat.name.ContainsKey("English"))
                {
                    dat.name.Add("English", dat.id);
                }

                foreach (var kvp in dat.name)
                {
                    build.Name.addForLang(kvp.Key, kvp.Value);
                }

                if (!dat.description.ContainsKey("English"))
                {
                    dat.description.Add("English", "");
                }

                foreach (var kvp in dat.description)
                {
                    build.Description.addForLang(kvp.Key, kvp.Value);
                }

                if (dat.icon != null && textures.TryGetValue(dat.icon, out Texture2D iconTex))
                {
                    var sprite = Sprite.Create(iconTex, new Rect(0, 0, 128, 128), Vector2.zero);
                    piece.m_icon = sprite;
                }
                else
                {
                    build.Snapshot();
                }
            }
        }        
        
    }
    
    private static (int width, int height) GetPngSize(byte[] bytes)
    {
        if (bytes.Length < 24)
            throw new InvalidDataException("Not a valid PNG");

        int width  = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        int height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        return (width, height);
    }
}