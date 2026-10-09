using System;
using System.Collections.Generic;
using PieceManager;

namespace CustomBanners;

[Serializable]
public class BannerData
{
    public string id = null!;
    public string image = null!;
    public string? icon;
    public Dictionary<string, string> name = new();
    public Dictionary<string, string> description = new();
    public List<Requirement> requirements = [];

}