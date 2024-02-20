# Custom Banners

Plugin allows to create infinite amount of custom banners

The plugin is published with 4 example folders.

In order to successfully add custom banners into the game, you will need to create folders with 3 dinstict files:
- texture.png
- icon.png
- banner.yml

The plugin will read these 3 files, and generater the necessary information to create a new banner prefab.

## Server Sync

If plugin is loaded on server, the yml file is read and shared with clients in order to dictate the recipe.

Server requires to have the texture.png and icon.png as well.

Recipes cannot be manipulated during run-time.

## Example YML

```yml
prefab_name: piece_banner_bear
display_name: Banner Bear
recipe:
- m_prefabName: FineWood
  m_recover: true
  m_amount: 2
  m_amountPerLevel: 1
  m_extraAmountOnlyOneIngredient: 0
- m_prefabName: LeatherScraps
  m_recover: true
  m_amount: 2
  m_amountPerLevel: 1
  m_extraAmountOnlyOneIngredient: 0
- m_prefabName: Coal
  m_recover: true
  m_amount: 4
  m_amountPerLevel: 1
  m_extraAmountOnlyOneIngredient: 0
- m_prefabName: Chitin
  m_recover: true
  m_amount: 2
  m_amountPerLevel: 1
  m_extraAmountOnlyOneIngredient: 0
```

![Imgur](https://i.imgur.com/eG7Zivz.png)

## Contact information
For Questions or Comments, find <span style="color:orange">Rusty</span> in the Odin Plus Team Discord

[![https://i.imgur.com/XXP6HCU.png](https://i.imgur.com/XXP6HCU.png)](https://discord.gg/v89DHnpvwS)

Or come find me at the [Modding Corner](https://discord.gg/fB8aHSfA8B)

##
If you enjoy this mod and want to support me:
[PayPal](https://paypal.me/mpei)

<span>
<img src="https://i.imgur.com/rbNygUc.png" alt="" width="150">
<img src="https://i.imgur.com/VZfZR0k.png" alt="https://www.buymeacoffee.com/peimalcolm2" width="150">
</span>
