using System;
using System.Collections.Generic;

namespace CustomBanners.CloneBanners;

[Serializable]
public class YmlData
{
    public string prefab_name = null!;
    public string display_name = null!;
    public List<BannerIngredient> recipe = new();
    public bool alt_banner = false;
}

[Serializable]
public class BannerData
{
    public string m_prefabName = null!;
    public string m_displayName = null!;
    public string m_texturesName = null!;
    public List<BannerIngredient> m_recipe = null!;
    public bool m_altBanner = false;
}
[Serializable]
public class BannerIngredient
{
    public string m_prefabName = null!;
    public bool m_recover;
    public int m_amount;
    public int m_amountPerLevel = 1;
    public int m_extraAmountOnlyOneIngredient = 0;
}