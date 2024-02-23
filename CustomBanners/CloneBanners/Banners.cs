using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using CustomBanners.Managers;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBanners.CloneBanners;

public static class Banners
{
    private static readonly List<GameObject> RegisteredBanners = new();
    
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");

    private static GameObject altBanner = null!;

    private static bool bannerRan;

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class ZNetSceneAwakePatch
    {
        private static void Postfix(ZNetScene __instance)
        {
            if (!__instance) return;
            BannerManager.PrepareBanners();
            InitBanners(__instance);
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    private static class PlayerOnSpawnedPatch
    {
        private static void Postfix(Player __instance)
        {
            if (!__instance) return;
            if (!ZNetScene.instance) return;
            SetServerRecipes(ZNetScene.instance);
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
    private static class GameLogoutPatch
    {
        private static void Postfix() => bannerRan = false;
    }

    private static void SetServerRecipes(ZNetScene instance)
    {
        foreach (GameObject prefab in RegisteredBanners)
        {
            BannerData data = BannerManager.TempBanners.Find(x => x.m_prefabName == prefab.name);
            if (data == null) continue;
            if (!prefab.TryGetComponent(out Piece component)) continue;
            List<Piece.Requirement> requirements = new();
            for (int index = 0; index < data.m_recipe.Count; index++)
            {
                BannerIngredient item = data.m_recipe[index];
                
                ConfigEntry<string> IngredientName = CustomBannersPlugin._plugin.config(prefab.name,
                    index + " - Ingredient Name", item.m_prefabName, "Ingredient prefab name");
                IngredientName.SettingChanged += OnSettingsChanged;
                
                GameObject resource = instance.GetPrefab(IngredientName.Value);
                if (!resource) continue;
                if (!resource.TryGetComponent(out ItemDrop itemDrop)) continue;

                ConfigEntry<int> IngredientAmount = CustomBannersPlugin._plugin.config(prefab.name,
                    index + " - Ingredient Amount", item.m_amount, "Set the amount needed to craft");
                IngredientAmount.SettingChanged += OnSettingsChanged;

                ConfigEntry<CustomBannersPlugin.Toggle> IngredientRecover =
                    CustomBannersPlugin._plugin.config(prefab.name, index + " - Ingredient Recover",
                        CustomBannersPlugin.Toggle.On, "If on, upon destroy, player recovers resource ingredient");
                IngredientRecover.SettingChanged += OnSettingsChanged;

                requirements.Add(new Piece.Requirement()
                {
                    m_resItem = itemDrop,
                    m_recover = IngredientRecover.Value is CustomBannersPlugin.Toggle.On,
                    m_amount = IngredientAmount.Value,
                    m_amountPerLevel = item.m_amountPerLevel,
                    m_extraAmountOnlyOneIngredient = item.m_extraAmountOnlyOneIngredient
                });
            }

            component.m_resources = requirements.ToArray();
        }
    }

    private static void InitBanners(ZNetScene instance)
    {
        if (bannerRan) return;
        GameObject originalBanner = instance.GetPrefab("piece_banner01");
        altBanner = CustomBannersPlugin._assets.LoadAsset<GameObject>("piece_custom_banner");
        if (!originalBanner.TryGetComponent(out Piece originalPiece)) return;
        if (!originalBanner.TryGetComponent(out WearNTear originalWear)) return;
        if (!altBanner.TryGetComponent(out Piece customPiece)) return;
        if (!altBanner.TryGetComponent(out WearNTear customWear)) return;

        customPiece.m_placeEffect = originalPiece.m_placeEffect;
        customPiece.m_craftingStation = originalPiece.m_craftingStation;
        customWear.m_destroyedEffect = originalWear.m_destroyedEffect;
        customWear.m_hitEffect = originalWear.m_hitEffect;
        customWear.m_switchEffect = originalWear.m_hitEffect;

        Transform woodBeam = altBanner.transform.Find("woodbeam");
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
        
        if (!originalBanner) return;
        foreach (BannerData data in BannerManager.TempBanners)
        {
            GameObject prefab = data.m_altBanner 
                ? Object.Instantiate(altBanner, CustomBannersPlugin.Root.transform, false) 
                : Object.Instantiate(originalBanner, CustomBannersPlugin.Root.transform, false);
            
            prefab.name = data.m_prefabName;

            if (data.m_altBanner)
            {
                if (!prefab.transform.Find("default").TryGetComponent(out MeshRenderer renderer1)) continue;
                foreach (Material mat in renderer1.materials)
                {
                    mat.shader = Shader.Find("Custom/Vegetation");
                    mat.SetTexture(MainTex, TextureManager.RegisteredTextures[data.m_texturesName]["main"]);
                    mat.SetTexture(BumpMap, null);
                }
            }
            else
            {
                if (!prefab.transform.Find("default").TryGetComponent(out MeshRenderer renderer)) continue;
                foreach (Material mat in renderer.materials)
                {
                    if (mat.shader.name != "Custom/Vegetation") continue;
                    mat.SetTexture(MainTex, TextureManager.RegisteredTextures[data.m_texturesName]["main"]);
                    mat.SetTexture(BumpMap, null);
                }
            }

            if (!prefab.TryGetComponent(out Piece component)) continue;
            component.m_icon = SpriteManager.CustomIcons[data.m_texturesName];
            component.name = data.m_prefabName;
            
            ConfigEntry<string> DisplayName = CustomBannersPlugin._plugin.config(prefab.name, "Display Name", data.m_displayName, "Set the display name");
            component.m_name = DisplayName.Value;
            DisplayName.SettingChanged += OnSettingsChanged;

            ConfigEntry<string> Description =
                CustomBannersPlugin._plugin.config(prefab.name, "Description", data.m_description, "Set description");
            component.m_description = Description.Value;
            Description.SettingChanged += OnSettingsChanged;

            ConfigEntry<Piece.PieceCategory> Category = CustomBannersPlugin._plugin.config(prefab.name, "Category",
                data.m_category, "Set banner category");
            component.m_category = Category.Value;
            Category.SettingChanged += OnSettingsChanged;
            
            List<Piece.Requirement> requirements = new();
            foreach (BannerIngredient item in data.m_recipe)
            {
                GameObject resource = instance.GetPrefab(item.m_prefabName);
                if (!resource) continue;
                if (!resource.TryGetComponent(out ItemDrop itemDrop)) continue;
                requirements.Add(new Piece.Requirement()
                {
                    m_resItem = itemDrop,
                    m_recover = item.m_recover,
                    m_amount = item.m_amount,
                    m_amountPerLevel = item.m_amountPerLevel,
                    m_extraAmountOnlyOneIngredient = item.m_extraAmountOnlyOneIngredient
                });
            }

            component.m_resources = requirements.ToArray();
        
            RegisterBannerToZNetScene(prefab, instance);
            RegisterBannerToPieceTable(instance, prefab);
            
            if (!RegisteredBanners.Contains(prefab)) RegisteredBanners.Add(prefab);
        }
        bannerRan = true;
    }

    private static void RegisterBannerToZNetScene(GameObject prefab, ZNetScene instance)
    {
        if (instance.m_namedPrefabs.ContainsKey(prefab.name.GetStableHashCode())) return;
        if (prefab.GetComponent<ZNetView>())
        {
            instance.m_prefabs.Add(prefab);
        }
        else
        {
            instance.m_nonNetViewPrefabs.Add(prefab);
        }
        instance.m_namedPrefabs.Add(prefab.name.GetStableHashCode(), prefab);
    }

    private static void RegisterBannerToPieceTable(ZNetScene instance, GameObject prefab)
    {
        if (!instance || !prefab) return;
        GameObject Hammer = instance.GetPrefab("Hammer");
        if (!Hammer.TryGetComponent(out ItemDrop component)) return;
        PieceTable pieceTable = component.m_itemData.m_shared.m_buildPieces;
        if (pieceTable.m_pieces.Contains(prefab)) return;
        pieceTable.m_pieces.Add(prefab);
    }

    private static void OnSettingsChanged(object sender, EventArgs e)
    {
        if (sender is ConfigEntry<string> stringConfig)
        {
            string[] definitions = stringConfig.Definition.ToString().Split('.');
            string prefabName = definitions[0];
            GameObject prefab = RegisteredBanners.Find(x => x.name == prefabName);
            if (!prefab) return;

            string configName = definitions[1];
            try
            {
                string[] configNameParts = configName.Split('-');
                if (configNameParts[1].StartsWith(" Ingredient"))
                {
                    string indexString = configNameParts[0].Replace(" ", "");
                    int index = int.Parse(indexString);
                    ResetRecipe(prefabName, index, stringConfig.Value);
                }
            }
            catch (IndexOutOfRangeException)
            {
                if (!prefab.TryGetComponent(out Piece component)) return;
                if (configName.StartsWith("Display Name"))
                {
                    component.m_name = stringConfig.Value;
                }
                else
                {
                    component.m_description = stringConfig.Value;
                }
            }
        }

        if (sender is ConfigEntry<Piece.PieceCategory> categoryConfig)
        {
            string prefabName = categoryConfig.Definition.ToString().Split('.')[0];
            GameObject prefab = RegisteredBanners.Find(x => x.name == prefabName);
            if (!prefab) return;
            if (!prefab.TryGetComponent(out Piece component)) return;
            component.m_category = categoryConfig.Value;
        }

        if (sender is ConfigEntry<int> intConfig)
        {
            string[] definitions = intConfig.Definition.ToString().Split('.');
            string prefabName = definitions[0];
            GameObject prefab = RegisteredBanners.Find(x => x.name == prefabName);
            if (!prefab) return;
            
            string configName = definitions[1];
            string[] configNameParts = configName.Split('-');
            if (configNameParts[1].StartsWith(" Ingredient"))
            {
                string indexString = configNameParts[0].Replace(" ", "");
                int index = int.Parse(indexString);
                ResetRecipe(prefabName, index, intConfig.Value);
            }
        }

        if (sender is ConfigEntry<CustomBannersPlugin.Toggle> toggleConfig)
        {
            string[] definitions = toggleConfig.Definition.ToString().Split('.');
            string prefabName = definitions[0];
            GameObject prefab = RegisteredBanners.Find(x => x.name == prefabName);
            if (!prefab) return;
            
            string configName = definitions[1];
            string[] configNameParts = configName.Split('-');
            if (configNameParts[1].StartsWith(" Ingredient"))
            {
                string indexString = configNameParts[0].Replace(" ", "");
                int index = int.Parse(indexString);
                ResetRecipe(prefabName, index, toggleConfig.Value);
            }
        }
    }
    
    private static void ResetRecipe(string prefabName, int index, string newValue)
    {
        GameObject prefab = RegisteredBanners.Find(x => x.name == prefabName);
        if (!prefab) return;
        if (!prefab.TryGetComponent(out Piece component)) return;
        try
        {
            GameObject newResource = ZNetScene.instance.GetPrefab(newValue);
            if (newResource)
            {
                if (!newResource.TryGetComponent(out ItemDrop newComponent)) return;

                component.m_resources[index].m_resItem = newComponent;
            }
            else
            {
                GameObject defaultResource = ZNetScene.instance.GetPrefab("Wood");
                if (!defaultResource.TryGetComponent(out ItemDrop defaultComponent)) return;

                component.m_resources[index].m_resItem = defaultComponent;
            }
        }
        catch
        {
            return;
        }
    }
    
    private static void ResetRecipe(string prefabName, int index, int newValue)
    {
        GameObject prefab = RegisteredBanners.Find(x => x.name == prefabName);
        if (!prefab) return;
        BannerData data = BannerManager.TempBanners.Find(x => x.m_prefabName == prefab.name);
        if (data == null) return;
        if (!prefab.TryGetComponent(out Piece component)) return;
        try
        {
            component.m_resources[index].m_amount = newValue;
        }
        catch
        {
            return;
        }
    }
    
    private static void ResetRecipe(string prefabName, int index, CustomBannersPlugin.Toggle newValue)
    {
        GameObject prefab = RegisteredBanners.Find(x => x.name == prefabName);
        if (!prefab) return;
        BannerData data = BannerManager.TempBanners.Find(x => x.m_prefabName == prefab.name);
        if (data == null) return;
        if (!prefab.TryGetComponent(out Piece component)) return;
        try
        {
            component.m_resources[index].m_recover = newValue is CustomBannersPlugin.Toggle.On;
        }
        catch
        {
            return;
        }
    }
    

    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Awake))]
    private static class ItemDropAwakePatch
    {
        private static void Postfix(ItemDrop __instance)
        {
            if (!__instance) return;
            if (!__instance.m_itemData.m_shared.m_buildPieces) return;
            PieceTable pieceTable = __instance.m_itemData.m_shared.m_buildPieces;
            foreach (GameObject prefab in RegisteredBanners)
            {
                if (pieceTable.m_pieces.Contains(prefab)) continue;
                pieceTable.m_pieces.Add(prefab);
            }
        }
    }
}