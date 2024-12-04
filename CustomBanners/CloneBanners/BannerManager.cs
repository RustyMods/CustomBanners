using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using CustomBanners.Managers;
using HarmonyLib;
using PieceManager;
using UnityEngine;
using YamlDotNet.Serialization;
using Object = UnityEngine.Object;

namespace CustomBanners.CloneBanners;

public static class BannerManager
{
    private static readonly string FolderName = Paths.ConfigPath + Path.DirectorySeparatorChar + "CustomBanners";
    private static readonly string ExampleFileName = FolderName + Path.DirectorySeparatorChar + "Example.yml";
    private static readonly string TutorialFileName = FolderName + Path.DirectorySeparatorChar + "Tutorial.md";
    private static readonly List<BannerData> TempBanners = new();
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
    
    private static readonly List<BuildPiece> RegisteredBanners = new();
    private static GameObject? AltBanner;
    private static GameObject? BaseBanner;
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
    private static readonly int EmissiveTex = Shader.PropertyToID("_EmissiveTex");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int AddRain = Shader.PropertyToID("_AddRain");
    private static readonly int Height = Shader.PropertyToID("_Height");
    private static readonly int SwaySpeed = Shader.PropertyToID("_SwaySpeed");
    private static readonly int RippleSpeed = Shader.PropertyToID("_RippleSpeed");
    private static readonly int RippleDistance = Shader.PropertyToID("_RippleDistance");
    private static readonly int RippleDeadzoneMin = Shader.PropertyToID("_RippleDeadzoneMin");
    private static readonly int RippleDeadzoneMax = Shader.PropertyToID("_RippleDeadzoneMax");
    private static readonly int PushDistance = Shader.PropertyToID("_PushDistance");
    private static readonly int PushClothMode = Shader.PropertyToID("_PushClothMode");
    private static readonly int CamCull = Shader.PropertyToID("_CamCull");

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class ZNetScene_Awake_Patch
    {
        private static void Postfix()
        {
            if (MaterialReplacer.CachedShaders.Count <= 0)
            {
                // Get all assetbundles and find the shaders in them
                var assetBundles = Resources.FindObjectsOfTypeAll<AssetBundle>();
                foreach (var bundle in assetBundles)
                {
                    IEnumerable<Shader>? bundleShaders;
                    try
                    {
                        bundleShaders = bundle.isStreamedSceneAssetBundle && bundle
                            ? bundle.GetAllAssetNames().Select(bundle.LoadAsset<Shader>).Where(shader => shader != null)
                            : bundle.LoadAllAssets<Shader>();
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (bundleShaders == null) continue;
                    foreach (var shader in bundleShaders)
                    {
                        MaterialReplacer.CachedShaders.Add(shader);
                    }
                }
            }
            if (CustomBannersPlugin._assets.LoadAsset<GameObject>("piece_custom_banner") is { } altBanner)
            {
                AltBanner = altBanner;
            }
            if (ZNetScene.instance.GetPrefab("piece_banner01") is { } prefab)
            {
                BaseBanner = prefab;
                
                if (AltBanner is not null)
                {
                    Piece? pieceComponent = prefab.GetComponent<Piece>();
                    WearNTear? wearComponent = prefab.GetComponent<WearNTear>();
                    Piece? altPieceComponent = AltBanner.GetComponent<Piece>();
                    WearNTear? altWearComponent = AltBanner.GetComponent<WearNTear>();
                    altPieceComponent.m_placeEffect = pieceComponent.m_placeEffect;
                    altPieceComponent.m_craftingStation = pieceComponent.m_craftingStation;
                    altWearComponent.m_destroyedEffect = wearComponent.m_destroyedEffect;
                    altWearComponent.m_hitEffect = wearComponent.m_hitEffect;
                    altWearComponent.m_switchEffect = wearComponent.m_switchEffect;
                    
                    Transform woodBeam = AltBanner.transform.Find("woodbeam");
                    if (woodBeam)
                    {
                        if (woodBeam.TryGetComponent(out Renderer woodBeamRenderer))
                        {
                            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
                            if (MaterialReplacer.OriginalMaterials.Count <= 0) MaterialReplacer.GetAllMaterials();
                            Material[] newMats = new Material[woodBeamRenderer.sharedMaterials.Length];
                            int i = 0;
                            foreach (var mat in woodBeamRenderer.sharedMaterials)
                            {
                                string replacementString = "_REPLACE_";
                                string matName = mat.name.Replace(" (Instance)", string.Empty).Replace(replacementString, "");
                                if (MaterialReplacer.OriginalMaterials.ContainsKey(matName))
                                {
                                    newMats[i] = MaterialReplacer.OriginalMaterials[matName];
                                }

                                ++i;
                            }

                            woodBeamRenderer.materials = newMats;
                            woodBeamRenderer.sharedMaterials = newMats;
                        }
                    }
                }
            }
            LoadBanners();
        }
    }
    private static void LoadBanners()
    {
        if (AltBanner is null || BaseBanner is null) return;
        foreach (BannerData data in TempBanners)
        {
            if (!TextureManager.RegisteredTextures.TryGetValue(data.m_texturesName, out Dictionary<string, Texture?> textures)) continue;
            if (!textures.TryGetValue("main", out Texture? mainTex)) continue;
            var emissive = textures.TryGetValue("emissive", out Texture? e) ? e : null;
            var normal = textures.TryGetValue("normal", out Texture? n) ? n : null;
            GameObject prefab = Object.Instantiate(data.m_altBanner ? AltBanner : BaseBanner, CustomBannersPlugin.Root.transform, false);
            prefab.name = data.m_prefabName;
            
            if (data.m_altBanner)
            {
                if (!prefab.transform.Find("default").TryGetComponent(out MeshRenderer renderer1)) continue;

                foreach (Material mat in renderer1.materials)
                {
                    mat.shader = MaterialReplacer.GetShaderForType(mat.shader, MaterialReplacer.ShaderType.VegetationShader, mat.shader.name);
                    mat.SetTexture(MainTex, mainTex);
                    mat.SetTexture(BumpMap, normal);
                    mat.SetTexture(EmissiveTex, emissive);
                    if (emissive) mat.SetColor(EmissionColor, Color.white);
                    mat.SetFloat(AddRain, 1f);
                    mat.SetFloat(Height, 15f);
                    mat.SetFloat(SwaySpeed, 15f);
                    mat.SetFloat(RippleSpeed, 50f);
                    mat.SetFloat(RippleDistance, 0.5f);
                    mat.SetFloat(RippleDeadzoneMin, 0f);
                    mat.SetFloat(RippleDeadzoneMax, 0f);
                    mat.SetFloat(PushDistance, 0.4f);
                    mat.SetFloat(PushClothMode, 1f);
                    mat.SetFloat(CamCull, 0f);
                }
            }
            else
            {
                if (!prefab.transform.Find("default").TryGetComponent(out MeshRenderer renderer)) continue;
                foreach (Material mat in renderer.materials)
                {
                    if (mat.shader.name != "Custom/Vegetation") continue;
                    mat.SetTexture(MainTex, mainTex);
                    mat.SetTexture(BumpMap, normal);
                    mat.SetTexture(EmissiveTex, emissive);
                    if (emissive) mat.SetColor(EmissionColor, Color.white);
                }
            }

            Piece? component = prefab.GetComponent<Piece>();
            component.m_name = $"$piece_{prefab.name.ToLower()}";
            component.m_description = $"$piece_{prefab.name.ToLower()}_desc";
            component.m_icon = SpriteManager.CustomIcons[data.m_texturesName];
            BuildPiece piece = new BuildPiece(prefab);
            piece.Name.English(data.m_displayName);
            piece.Description.English(data.m_description);
            piece.Category.Set(data.m_category);
            piece.Crafting.Set(CraftingTable.Workbench);
            foreach (var requirement in data.m_recipe)
            {
                piece.RequiredItems.Add(requirement.m_prefabName, requirement.m_amount, requirement.m_recover);
            }
            if (!ZNetScene.instance.m_prefabs.Contains(prefab)) ZNetScene.instance.m_prefabs.Add(prefab);
            ZNetScene.instance.m_namedPrefabs[prefab.name.GetStableHashCode()] = prefab;
            RegisteredBanners.Add(piece);
        }
        FinalizeBanners();
    }
    private static void FinalizeBanners()
    {
        Assembly? bepinexConfigManager = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "ConfigurationManager");
        Type? configManagerType = bepinexConfigManager?.GetType("ConfigurationManager.ConfigurationManager");
        void ReloadConfigDisplay()
        {
            if (configManagerType?.GetProperty("DisplayingWindow")!.GetValue(BuildPiece.configManager) is true)
            {
                configManagerType.GetMethod("BuildSettingList")!.Invoke(BuildPiece.configManager, Array.Empty<object>());
            }
        }
        foreach (BuildPiece piece in RegisteredBanners)
        {
            piece.activeTools = piece.Tool.Tools.DefaultIfEmpty("Hammer").ToArray();
            if (piece.Category.Category != BuildPieceCategory.Custom)
            {
                piece.Prefab.GetComponent<Piece>().m_category = (Piece.PieceCategory)piece.Category.Category;
            }
            else
            {
                piece.Prefab.GetComponent<Piece>().m_category = PiecePrefabManager.GetCategory(piece.Category.custom);
            }
        }

        if (BuildPiece.ConfigurationEnabled)
        {
            bool SaveOnConfigSet = BuildPiece.plugin.Config.SaveOnConfigSet;
            BuildPiece.plugin.Config.SaveOnConfigSet = false;
            foreach (BuildPiece piece in RegisteredBanners)
            {
                if (piece.SpecialProperties.NoConfig) continue;
                BuildPiece.PieceConfig cfg = BuildPiece.pieceConfigs[piece] = new BuildPiece.PieceConfig();
                Piece piecePrefab = piece.Prefab.GetComponent<Piece>();
                string pieceName = piecePrefab.m_name;
                string englishName = new Regex(@"[=\n\t\\""\'\[\]]*").Replace(BuildPiece.english.Localize(pieceName), "").Trim();
                string localizedName = Localization.instance.Localize(pieceName).Trim();

                int order = 0;

                cfg.category = BuildPiece.config(englishName, "Build Table Category", piece.Category.Category,
                    new ConfigDescription($"Build Category where {localizedName} is available.", null,
                        new BuildPiece.ConfigurationManagerAttributes
                            { Order = --order, Category = localizedName }));
                BuildPiece.ConfigurationManagerAttributes customTableAttributes = new()
                {
                    Order = --order, Browsable = cfg.category.Value == BuildPieceCategory.Custom,
                    Category = localizedName,
                };
                cfg.customCategory = BuildPiece.config(englishName, "Custom Build Category", piece.Category.custom, new ConfigDescription("", null, customTableAttributes));

                void BuildTableConfigChanged(object o, EventArgs e)
                {
                    if (BuildPiece.registeredPieces.Count > 0)
                    {
                        if (cfg.category.Value is BuildPieceCategory.Custom)
                        {
                            piecePrefab.m_category = PiecePrefabManager.GetCategory(cfg.customCategory.Value);
                        }
                        else
                        {
                            piecePrefab.m_category = (Piece.PieceCategory)cfg.category.Value;
                        }

                        if (Hud.instance)
                        {
                            PiecePrefabManager.CategoryRefreshNeeded = true;
                            PiecePrefabManager.CreateCategoryTabs();
                        }
                    }

                    customTableAttributes.Browsable = cfg.category.Value == BuildPieceCategory.Custom;
                    ReloadConfigDisplay();
                }

                cfg.category.SettingChanged += BuildTableConfigChanged;
                cfg.customCategory.SettingChanged += BuildTableConfigChanged;

                if (cfg.category.Value is BuildPieceCategory.Custom)
                {
                    piecePrefab.m_category = PiecePrefabManager.GetCategory(cfg.customCategory.Value);
                }
                else
                {
                    piecePrefab.m_category = (Piece.PieceCategory)cfg.category.Value;
                }

                cfg.tools = BuildPiece.config(englishName, "Tools", string.Join(", ", piece.activeTools),
                    new ConfigDescription($"Comma separated list of tools where {localizedName} is available.", null,
                        customTableAttributes));
                piece.activeTools = cfg.tools.Value.Split(',').Select(s => s.Trim()).ToArray();
                cfg.tools.SettingChanged += (_, _) =>
                {
                    Inventory[] inventories = Player.s_players.Select(p => p.GetInventory())
                        .Concat(Object.FindObjectsOfType<Container>().Select(c => c.GetInventory()))
                        .Where(c => c is not null).ToArray();
                    Dictionary<string, List<PieceTable>> tools = ObjectDB.instance.m_items
                        .Select(p => p.GetComponent<ItemDrop>()).Where(c => c && c.GetComponent<ZNetView>())
                        .Concat(ItemDrop.s_instances)
                        .Select(i =>
                            new KeyValuePair<string, ItemDrop.ItemData>(Utils.GetPrefabName(i.gameObject), i.m_itemData))
                        .Concat(inventories.SelectMany(i => i.GetAllItems()).Select(i =>
                            new KeyValuePair<string, ItemDrop.ItemData>(i.m_dropPrefab.name, i)))
                        .Where(kv => kv.Value.m_shared.m_buildPieces).GroupBy(kv => kv.Key).ToDictionary(g => g.Key,
                            g => g.Select(kv => kv.Value.m_shared.m_buildPieces).Distinct().ToList());

                    foreach (string tool in piece.activeTools)
                    {
                        if (tools.TryGetValue(tool, out List<PieceTable> existingTools))
                        {
                            foreach (PieceTable table in existingTools)
                            {
                                table.m_pieces.Remove(piece.Prefab);
                            }
                        }
                    }

                    piece.activeTools = cfg.tools.Value.Split(',').Select(s => s.Trim()).ToArray();
                    if (ObjectDB.instance)
                    {
                        foreach (string tool in piece.activeTools)
                        {
                            if (tools.TryGetValue(tool, out List<PieceTable> existingTools))
                            {
                                foreach (PieceTable table in existingTools)
                                {
                                    if (!table.m_pieces.Contains(piece.Prefab))
                                    {
                                        table.m_pieces.Add(piece.Prefab);
                                    }
                                }
                            }
                        }

                        if (Player.m_localPlayer && Player.m_localPlayer.m_buildPieces)
                        {
                            PiecePrefabManager.CategoryRefreshNeeded = true;
                            Player.m_localPlayer.SetPlaceMode(Player.m_localPlayer.m_buildPieces);
                        }
                    }
                };

                if (piece.Crafting.Stations.Count > 0)
                {
                    List<BuildPiece.ConfigurationManagerAttributes> hideWhenNoneAttributes = new();

                    cfg.table = BuildPiece.config(englishName, "Crafting Station", piece.Crafting.Stations.First().Table,
                        new ConfigDescription($"Crafting station where {localizedName} is available.", null,
                            new BuildPiece.ConfigurationManagerAttributes { Order = --order }));
                    cfg.customTable = BuildPiece.config(englishName, "Custom Crafting Station",
                        piece.Crafting.Stations.First().custom ?? "",
                        new ConfigDescription("", null, customTableAttributes));

                    void TableConfigChanged(object o, EventArgs e)
                    {
                        if (piece.RequiredItems.Requirements.Count > 0)
                        {
                            switch (cfg.table.Value)
                            {
                                case CraftingTable.None:
                                    piecePrefab.m_craftingStation = null;
                                    break;
                                case CraftingTable.Custom:
                                    piecePrefab.m_craftingStation = ZNetScene.instance.GetPrefab(cfg.customTable.Value)
                                        ?.GetComponent<CraftingStation>();
                                    break;
                                default:
                                    piecePrefab.m_craftingStation = ZNetScene.instance
                                        .GetPrefab(
                                            ((InternalName)typeof(CraftingTable).GetMember(cfg.table.Value.ToString())[0]
                                                .GetCustomAttributes(typeof(InternalName)).First()).internalName)
                                        .GetComponent<CraftingStation>();
                                    break;
                            }
                        }

                        customTableAttributes.Browsable = cfg.table.Value == CraftingTable.Custom;
                        foreach (BuildPiece.ConfigurationManagerAttributes attributes in hideWhenNoneAttributes)
                        {
                            attributes.Browsable = cfg.table.Value != CraftingTable.None;
                        }

                        ReloadConfigDisplay();
                        BuildPiece.plugin.Config.Save();
                    }

                    cfg.table.SettingChanged += TableConfigChanged;
                    cfg.customTable.SettingChanged += TableConfigChanged;

                    BuildPiece.ConfigurationManagerAttributes tableLevelAttributes = new()
                        { Order = --order, Browsable = cfg.table.Value != CraftingTable.None };
                    
                    hideWhenNoneAttributes.Add(tableLevelAttributes);
                }

                ConfigEntry<string> itemConfig(string name, string value, string desc)
                {
                    BuildPiece.ConfigurationManagerAttributes attributes = new() { CustomDrawer = BuildPiece.DrawConfigTable, Order = --order, Category = localizedName };
                    return BuildPiece.config(englishName, name, value, new ConfigDescription(desc, null, attributes));
                }

                cfg.craft = itemConfig("Crafting Costs", new BuildPiece.SerializedRequirements(piece.RequiredItems.Requirements).ToString(), $"Item costs to craft {localizedName}");
                cfg.craft.SettingChanged += (_, _) =>
                {
                    if (ObjectDB.instance && ObjectDB.instance.GetItemPrefab("YmirRemains") != null)
                    {
                        Piece.Requirement[] requirements = BuildPiece.SerializedRequirements.toPieceReqs(new BuildPiece.SerializedRequirements(cfg.craft.Value));
                        piecePrefab.m_resources = requirements;
                        foreach (Piece instantiatedPiece in Object.FindObjectsOfType<Piece>())
                        {
                            if (instantiatedPiece.m_name == pieceName)
                            {
                                instantiatedPiece.m_resources = requirements;
                            }
                        }
                    }
                };
            }
            
            foreach (var piece in RegisteredBanners)
            {
                if (piece.RecipeIsActive is { } enabledCfg)
                {
                    Piece piecePrefab = piece.Prefab.GetComponent<Piece>();
                    void ConfigChanged(object? o, EventArgs? e) => piecePrefab.m_enabled = (int)enabledCfg.BoxedValue != 0;
                    ConfigChanged(null, null);
                    enabledCfg.GetType().GetEvent(nameof(ConfigEntry<int>.SettingChanged)).AddEventHandler(enabledCfg, new EventHandler(ConfigChanged));
                }

                piece.InitializeNewRegisteredPiece(piece);
            }

            foreach (var piece in RegisteredBanners)
            {
                if (!BuildPiece.pieceConfigs.TryGetValue(piece, out BuildPiece.PieceConfig? cfg)) continue;
                piece.Prefab.GetComponent<Piece>().m_resources = BuildPiece.SerializedRequirements.toPieceReqs(new BuildPiece.SerializedRequirements(cfg.craft.Value));

                foreach (CraftingStationConfig station in piece.Crafting.Stations)
                {
                    switch ((cfg == null || piece.Crafting.Stations.Count > 0 ? station.Table : cfg.table.Value))
                    {
                        case CraftingTable.None:
                            piece.Prefab.GetComponent<Piece>().m_craftingStation = null;
                            break;
                        case CraftingTable.Custom
                            when ZNetScene.instance.GetPrefab(cfg == null || piece.Crafting.Stations.Count > 0
                                ? station.custom
                                : cfg.customTable.Value) is { } craftingTable:
                            piece.Prefab.GetComponent<Piece>().m_craftingStation =
                                craftingTable.GetComponent<CraftingStation>();
                            break;
                        case CraftingTable.Custom:
                            Debug.LogWarning($"Custom crafting station '{(cfg == null || piece.Crafting.Stations.Count > 0 ? station.custom : cfg.customTable.Value)}' does not exist");
                            break;
                        default:
                        {
                            if (cfg is { table.Value: CraftingTable.None })
                            {
                                piece.Prefab.GetComponent<Piece>().m_craftingStation = null;
                            }
                            else
                            {
                                piece.Prefab.GetComponent<Piece>().m_craftingStation = ZNetScene.instance
                                    .GetPrefab(((InternalName)typeof(CraftingTable).GetMember(
                                        (cfg == null || piece.Crafting.Stations.Count > 0 ? station.Table : cfg.table.Value)
                                        .ToString())[0].GetCustomAttributes(typeof(InternalName)).First()).internalName)
                                    .GetComponent<CraftingStation>();
                            }

                            break;
                        }
                    }
                }
            }
            if (SaveOnConfigSet)
            {
                BuildPiece.plugin.Config.SaveOnConfigSet = true;
                BuildPiece.plugin.Config.Save();
            }
        }
        
        BuildPiece.registeredPieces.AddRange(RegisteredBanners);
    }
    private static void WriteTutorial()
    {
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
    }
    private static bool GetFiles(string[] files, out Dictionary<string, Texture?> textures, out Sprite? icon, out YmlData data)
    {
        textures = new Dictionary<string, Texture?>();
        icon = null;
        data = new YmlData();
        foreach (var filePath in files)
        {
            if (filePath.EndsWith("texture.png"))
            {
                if (TextureManager.RegisterTexture(filePath) is { } main)
                {
                    textures["main"] = main;
                }
            }
            else if (filePath.EndsWith("icon.png"))
            {
                if (SpriteManager.RegisterSprite(filePath) is { } sprite)
                {
                    icon = sprite;
                }
            }
            else if (filePath.EndsWith("emissive.png"))
            {
                if (TextureManager.RegisterTexture(filePath) is { } emissive)
                {
                    textures["emissive"] = emissive;
                }
            }
            else if (filePath.EndsWith("normal.png"))
            {
                if (TextureManager.RegisterTexture(filePath) is { } normal)
                {
                    textures["normal"] = normal;
                }
            }
            else if (filePath.EndsWith(".yml"))
            {
                var deserializer = new DeserializerBuilder().Build();
                if (deserializer.Deserialize<YmlData>(File.ReadAllText(filePath)) is { } ymlData)
                {
                    data = ymlData;
                }
                else
                {
                    return false;
                }
            }
        }
        return icon != null;
    }
    public static void PrepareBanners()
    {
        if (!Directory.Exists(FolderName)) Directory.CreateDirectory(FolderName);
        WriteTutorial();
        
        string[] folders = Directory.GetDirectories(FolderName);
        SpriteManager.CustomIcons.Clear();
        TextureManager.RegisteredTextures.Clear();
        TempBanners.Clear();
        foreach (string folderName in folders)
        {
            string[] files = Directory.GetFiles(folderName);
            if (files.Length < 3)
            {
                CustomBannersPlugin.CustomBannersLogger.LogDebug("Failed to find all required files in " + folderName);
                continue;
            }

            if (!GetFiles(files, out Dictionary<string, Texture?> textures, out Sprite? icon, out YmlData? data))
            {
                CustomBannersPlugin.CustomBannersLogger.LogDebug("Failed to get all required information from " + folderName);
                continue;
            }

            SpriteManager.CustomIcons[folderName] = icon;
            TextureManager.RegisteredTextures[folderName] = textures;

            BannerData bannerData = new BannerData()
            {
                m_prefabName = data.prefab_name,
                m_displayName = data.display_name,
                m_description = data.description,
                m_texturesName = folderName,
                m_recipe = data.recipe,
                m_altBanner = data.alt_banner,
                m_category = data.category
            };
            if (!TempBanners.Contains(bannerData)) TempBanners.Add(bannerData);
        }
    }
}