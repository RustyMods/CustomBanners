# Custom Banners

Plugin allows to create custom banners.

## Need a dedicated server ?
- Use this link: https://www.survivalservers.com/r/rustymods/valheim
- I'll get a 20% commission. Thanks.


## 1.1.0 update
Lost the original project, so everything has been remade. Files designed around previous versions will not work.

## How to
1. Navigate to BepInEx/config/CustomBanners.
2. The plugin includes two example banners to use as templates.
3. Copy the example .png and .yml files for your new banner.
4. Edit the .yml file to configure your banner's name, description, texture, and build requirements.
5. Open the .png file in your preferred image editor (e.g., Photoshop) and create your banner texture.
6. (Optional) Create a custom icon texture. Icons must have a square aspect ratio (e.g., 128×128).

Each custom banner requires a .png texture and a .yml configuration file.

## YML Format

```yml
# Unique prefab ID. Banners with duplicate IDs will be skipped.
id: piece_custom_banner_default_example

# Localized name
name: 
  English: Custom Banner
  French: Bannière Personnalisée
  
# Localized description
description: 
  English: ""

# PNG texture filename without the extension
image: example

# Optional icon texture. Leave empty to generate an icon automatically.
icon: ""

# Build requirements
requirements:
  - itemName: FineWood
    amount: 10
    recover: true
  - itemName: Coal
    amount: 2
    recover: true
  - itemName: LeatherScraps
    amount: 2
    recover: true
```

## Texture Format
![](https://raw.githubusercontent.com/RustyMods/CustomBanners/refs/heads/master/CustomBanners/Examples/bird.png)


![](https://raw.githubusercontent.com/RustyMods/CustomBanners/refs/heads/master/CustomBanners/Screenshots/Screenshot%202026-10-09%20084627.png)

##
If you enjoy this mod and want to support me:
[PayPal](https://paypal.me/mpei)

<span>
<img src="https://i.imgur.com/rbNygUc.png" alt="" width="150">
<img src="https://i.imgur.com/VZfZR0k.png" alt="https://www.buymeacoffee.com/peimalcolm2" width="150">
</span>
