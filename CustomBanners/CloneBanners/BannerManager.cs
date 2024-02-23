using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using CustomBanners.Managers;
using ServerSync;
using UnityEngine;
using YamlDotNet.Serialization;

namespace CustomBanners.CloneBanners;

public static class BannerManager
{
    private static readonly string FolderName = Paths.ConfigPath + Path.DirectorySeparatorChar + "CustomBanners";
    private static readonly string ExampleFileName = FolderName + Path.DirectorySeparatorChar + "Example.yml";
    private static readonly string TutorialFileName = FolderName + Path.DirectorySeparatorChar + "Tutorial.md";

    private static readonly CustomSyncedValue<string> ServerSyncedBannerData = new(CustomBannersPlugin.ConfigSync, "ServerBannerData", "");

    public static readonly List<BannerData> TempBanners = new();

    private static readonly List<string> tutorial = new()
    {
        "Custom Banners Tutorial",
        "In order to successfully add custom banners into the game, ",
        "you will need to create folders with 3 distinct files",
        "- texture.png",
        "- icon.png",
        "- banner.yml",
        "The plugin will read these 3 files, and generate the necessary information to, ",
        "create a new banner prefab, ",
        "",
        "Brought to you by ~ RustyMods ~"
    };

    public static void PrepareBanners()
    {
        if (!Directory.Exists(FolderName)) Directory.CreateDirectory(FolderName);
        if (!File.Exists(ExampleFileName))
        {
            ISerializer serializer = new SerializerBuilder().Build();
            string data = serializer.Serialize(new YmlData()
            {
                prefab_name = "PrefabName",
                display_name = "Display Name",
                recipe = new List<BannerIngredient>()
                {
                    new BannerIngredient()
                    {
                        m_prefabName = "FineWood",
                        m_recover = true,
                        m_amount = 2,
                    },
                    new BannerIngredient()
                    {
                        m_prefabName = "LeatherScraps",
                        m_recover = true,
                        m_amount = 2,
                    },
                    new BannerIngredient()
                    {
                        m_prefabName = "Coal",
                        m_recover = true,
                        m_amount = 4,
                    }
                }
            });
            File.WriteAllText(ExampleFileName, data);
        }

        if (!File.Exists(TutorialFileName))
        {
            File.WriteAllLines(TutorialFileName, tutorial);
        }
        
        string[] folders = Directory.GetDirectories(FolderName);
        SpriteManager.CustomIcons.Clear();
        TextureManager.RegisteredTextures.Clear();
        TempBanners.Clear();
        foreach (string folderName in folders)
        {
            string[] files = Directory.GetFiles(folderName);
            if (files.Length != 3)
            {
                CustomBannersPlugin.CustomBannersLogger.LogDebug("Failed to find all required files in " + folderName);
                continue;
            }
            
            Dictionary<string, Texture?> textures = new();
            Sprite? sprite = null;
            YmlData data = new YmlData();
            bool success = true;
            
            foreach (string fileName in files)
            {
                if (fileName.EndsWith("texture.png"))
                {
                    Texture? main = TextureManager.RegisterTexture(fileName);
                    if (main == null)
                    {
                        CustomBannersPlugin.CustomBannersLogger.LogDebug("Failed to register texture " + fileName);
                        success = false;
                    }
                    else
                    {
                        textures.Add("main", main);
                    }
                }

                if (fileName.EndsWith("icon.png"))
                {
                    sprite = SpriteManager.RegisterSprite(fileName);
                    if (sprite == null)
                    {
                        CustomBannersPlugin.CustomBannersLogger.LogDebug("Failed to register icon " + fileName);
                        success = false;
                    };
                }

                if (fileName.EndsWith(".yml"))
                {
                    try
                    {
                        IDeserializer deserializer = new DeserializerBuilder().Build();
                        data = deserializer.Deserialize<YmlData>(File.ReadAllText(fileName));
                    }
                    catch
                    {
                        CustomBannersPlugin.CustomBannersLogger.LogDebug("Failed to deserialize data from " + fileName);
                        success = false;
                    }
                }
            }
            
            if (!success)
            {
                CustomBannersPlugin.CustomBannersLogger.LogDebug("Failed to get all required information from " + folderName);
                continue;
            }
            if (!SpriteManager.CustomIcons.ContainsKey(folderName)) SpriteManager.CustomIcons.Add(folderName, sprite);
            if(!TextureManager.RegisteredTextures.ContainsKey(folderName)) TextureManager.RegisteredTextures.Add(folderName, textures);
            Piece.PieceCategory category = Piece.PieceCategory.Furniture;
            try
            {
                object cat = Enum.Parse(typeof(Piece.PieceCategory), data.category);
                if (cat is Piece.PieceCategory pieceCategory)
                {
                    category = pieceCategory;
                }
            }
            catch (ArgumentException)
            {
                CustomBannersPlugin.CustomBannersLogger.LogDebug("Failed to set category: " + data.category);
            }
            BannerData bannerData = new BannerData()
            {
                m_prefabName = data.prefab_name,
                m_displayName = data.display_name,
                m_description = data.description,
                m_texturesName = folderName,
                m_recipe = data.recipe,
                m_altBanner = data.alt_banner,
                m_category = category
            };
            if (!TempBanners.Contains(bannerData)) TempBanners.Add(bannerData);
        }

        if (ZNet.instance.IsServer())
        {
            ISerializer serializer = new SerializerBuilder().Build();
            string data = serializer.Serialize(TempBanners);
            ServerSyncedBannerData.Value = data;
        }
        else
        {
            ServerSyncedBannerData.ValueChanged += OnServerBannerDataChange;
        }
    }
    private static void OnServerBannerDataChange()
    {
        try
        {
            IDeserializer deserializer = new DeserializerBuilder().Build();
            List<BannerData> ServerBannerData =
                deserializer.Deserialize<List<BannerData>>(ServerSyncedBannerData.Value);

            foreach (BannerData bannerData in TempBanners)
            {
                BannerData ServerData = ServerBannerData.Find(x => x.m_prefabName == bannerData.m_prefabName);
                if (ServerData == null) continue;
                bannerData.m_recipe = ServerData.m_recipe;
            }
        }
        catch
        {
            return;
        }
    }
}