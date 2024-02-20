using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CustomBanners.Managers;

public static class SpriteManager
{
    public static readonly Dictionary<string, Sprite?> CustomIcons = new();
    
    public static Sprite? RegisterSprite(string fileName)
    {
        if (!File.Exists(fileName)) return null;

        byte[] fileData = File.ReadAllBytes(fileName);
        Texture2D texture = new Texture2D(4, 4);

        if (texture.LoadImage(fileData))
        {
            texture.name = fileName;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
        }

        return null;
    }
}