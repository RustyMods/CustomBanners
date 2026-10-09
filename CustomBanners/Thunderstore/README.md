# Custom Banners

Plugin allows to create custom banners.

## 1.1.0 update
Lost the original project, so everything has been remade. Files designed around previous versions will not work.

## How to
1. Find the CustomBanner folder under `BepinEx/config/CustomBanners`
2. The plugin ships with 2 example banners
3. You will need 2 files (`.png` and `.yml`) to create a custom banner
4. Use the provided example files (copy & paste) and edit them
5. Open the png file with your image editing software of your choice (ie. Photoshop) and create your texture
6. optional: create an icon texture, must be a square ratio (ie. 128x128)

## YML Format

```yml
## id must be unique, if another prefab has the same id, plugin will skip it
id: piece_custom_banner_default_example
## localized name in English
name: custom banner
## localized description in English
description: ""
## provide texture PNG name without extension
image: example
## icon is optional, if not provided, plugin will generate icon
icon: "" 
## build requirements
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
## If banner has been successfully created, configurations will be available to edit while in-game under RustyMods.CustomBanners.cfg
## This file defines the initial state of the prefab, editing file while in-game won't do anything
```

![](https://i.imgur.com/4lNh6Jb.png)


##
If you enjoy this mod and want to support me:
[PayPal](https://paypal.me/mpei)

<span>
<img src="https://i.imgur.com/rbNygUc.png" alt="" width="150">
<img src="https://i.imgur.com/VZfZR0k.png" alt="https://www.buymeacoffee.com/peimalcolm2" width="150">
</span>
