# NoDecay
Scales or disables decay of items, and deployables 

[Download](https://code.remod.org/NoDecay.cs)

**No Decay** nullifies or scales down/up any decay damage applied to any item in game (except of small stashes). Each building tier has a different multiplier,  so do all other entities.

The default configuration does **NOT** affect *Twig decay* but nullifies all damage on all other items.

**As of version 1.0.34** you can optionally also check for the presence of a deployed tool cupboard.  Set requireCupboard to true.  This will check for an attached cupboard for building blocks and a nearby cupboard for entities.
For entities, use "cupboardCheckEntity: true" and "cupboardRange: number" to configure how far the entities can be from a cupboard before they will decay.  The default is 30 game meters (?), which may or may not be enough for your needs.  Adjust as desired.

Note, the default is cupboardCheckEntity: false, which will skip checking for cupboards in range of entities.  It will still check for blocks attached to cupboards, which should be more accurate.
## Configuration

```json
{
  "Debug": {
    "outputToRcon": false,
    "outputMundane": false
  },
    "Global": {
    "blockCupboardResources": false,
    "blockCupboardWood": false,
    "blockCupboardStone": false,
    "blockCupboardMetal": false,
    "blockCupboardArmor": false,
    "requireCupboard": false,
    "cupboardCheckEntity": false,
    "cupboardRange": 30.0,
    "usePermission": false,
    "DestroyOnZero": false
  },
  "Mutipliers": {
    "armoredMultiplier": 0.0,
    "baloonMultiplier": 0.0,
    "barricadesMultiplier": 0.0,
    "bbqMultiplier": 0.0,
    "boatMultiplier": 0.0,
    "boxMultiplier": 0.0,
    "campfireMultiplier": 0.0,
    "deployablesMultiplier": 0.0,
    "furnaceMultiplier": 0.0,
    "highStoneWallMultiplier": 0.0,
    "highWoodWallMultiplier": 0.0,
    "minicopterMultiplier": 0.0,
    "scrapcopterMultiplier": 0.0,
    "samMultiplier": 0.0,
    "sedanMultiplier": 0.0,
    "sheetMultiplier": 0.0,
    "stoneMultiplier": 0.0,
    "trapMultiplier": 0.0,
    "twigMultiplier": 1.0,
    "watchtowerMultiplier": 0.0,
    "woodMultiplier": 0.0,
    "horseMultiplier": 0.0
  }
}
```

The default configuration above disables decay for all but twig.  For each multiplier, set to 1 for normal decay, 0 for no decay, and somewhere in between for reduced decay.

Set usePermission to true to require the nodecay.use permission to prevent decay.  If false, all players are protected by default.

If "blockCupboardResources" is set to true, blocks stone, frags, and hqm from being added to a cupboard.

If "blockCupboardWood" is set to true, blocks wood from being added to a cupboard.

  - Use both of these to prevent all building materials from being added to cupboards.  Players will still get the Building Decaying warning but will not waste resources on upkeep since they are not necessary with NoDecay.
 
  - Use only blockCupboardWood to block wood and therefore upkeep on twig.  You may also set the following instead of blockCupboardResources:

If "blockCupboardStone" is set to true, blocks stone from being added to a cupboard.

If "blockCupboardMetal" is set to true, blocks metal frags from being added to a cupboard.

If "blockCupboardArmor" is set to true, blocks HQM from being added to a cupboard.
 
 Set requireCupboard to true to check for a cupboard to prevent decay.

 Set cupboardCheckEntity to also check for entities in range of cupboards (i.e. not just foundations, etc.  This should work on doors and high walls, etc.

 Set cupboardRange to a desired value for the cupboardCheckEntity range.  If too high, may affect other user's stuff.  If set too low it may not protect external items if out of range.
 
 Set DestroyOnZero to true to enable destroying entities when health is zero.  This is most likely needed due to a bug in the Feb 2020 Rust.

## Permissions

If the "usePermission" flag is set to true, the following permission is required to enable NoDecay for a user:

nodecay.use

## Credits

- **Deicide666ra** and **Piarb**, the original authors of this plugin
- **Diesel**, for helping maintain the plugin
