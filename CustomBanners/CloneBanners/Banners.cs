using System.Collections.Generic;
using CustomBanners.Managers;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBanners.CloneBanners;

public static class Banners
{
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");

    private static bool bannerRan;

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class ZNetSceneAwakePatch
    {
        private static void Postfix(ZNetScene __instance)
        {
            if (!__instance) return;
            BannerManager.PrepareBanners();
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    private static class PlayerOnSpawnedPatch
    {
        private static void Postfix(Player __instance)
        {
            if (!__instance) return;
            InitBanners(ZNetScene.instance);
        }
    }

    private static void InitBanners(ZNetScene instance)
    {
        if (bannerRan) return;
        GameObject originalBanner = instance.GetPrefab("piece_banner01");
        if (!originalBanner) return;
        foreach (BannerData data in BannerManager.TempBanners)
        {
            GameObject prefab = Object.Instantiate(originalBanner, CustomBannersPlugin.Root.transform, false);
            prefab.name = data.m_prefabName;
            if (!prefab.transform.Find("default").TryGetComponent(out MeshRenderer renderer)) continue;
            foreach (Material mat in renderer.materials)
            {
                if (mat.shader.name != "Custom/Vegetation") continue;
                mat.SetTexture(MainTex, TextureManager.RegisteredTextures[data.m_texturesName]["main"]);
                mat.SetTexture(BumpMap, null);
            }

            if (!prefab.TryGetComponent(out Piece component)) continue;
            component.m_icon = SpriteManager.CustomIcons[data.m_texturesName];
            component.name = data.m_prefabName;
            component.m_name = data.m_displayName;

            List<Piece.Requirement> requirements = new();
            for (int index = 0; index < data.m_recipe.Count; index++)
            {
                BannerIngredient item = data.m_recipe[index];
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

            bannerRan = true;
        }
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
    
}