using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CustomBanners.Managers;

public static class TextureManager
{
    public static readonly Dictionary<string, Dictionary<string, Texture?>> RegisteredTextures = new();

    public static Texture? RegisterTexture(string fileName)
    {
        if (!File.Exists(fileName)) return null;

        byte[] fileData = File.ReadAllBytes(fileName);
        Texture2D texture = new Texture2D(2, 2);
        if (texture.LoadImage(fileData))
        {
            texture.filterMode = FilterMode.Point;
            texture.name = fileName;
            return texture;
        }
        return null;
    }
}