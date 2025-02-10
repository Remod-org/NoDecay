## NoDecay (official)
Scales or disables decay of items, and deployables 

**No Decay** nullifies or scales down/up any decay damage applied to any item in game (except of small stashes). Each building tier has a different multiplier,  so do all other entities.

The default configuration does **NOT** affect *Twig decay* but nullifies all damage on all other items.

**As of version 1.0.34** you can optionally also check for the presence of a deployed tool cupboard.  Set requireCupboard to true.  This will check for an attached cupboard for building blocks and a nearby cupboard for entities.
For entities, use "cupboardCheckEntity: true" and "cupboardRange: number" to configure how far the entities can be from a cupboard before they will decay.  The default is 30 game meters (?), which may or may not be enough for your needs.  Adjust as desired.

Note, the default is cupboardCheckEntity: false, which will skip checking for cupboards in range of entities.  It will still check for blocks attached to cupboards, which should be more accurate.

### Configuration
NOTE: The long-standing misspelling of Multipliers has been fixed as of 1.0.46.  Older configs should be upgraded automatically.

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
    "DestroyOnZero": true,
    "disableWarning": true,
    "protectedDays": 0.0,
    "protectVehicleOnLift": true,
    "warningTime": 10.0
  },
  "Multipliers": {
    "entityCupboardMultiplier": 0.0,
    "twigMultiplier": 1.0,
    "woodMultiplier": 0.0,
    "sheetMultiplier": 0.0,
    "stoneMultiplier": 0.0,
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
    "horseMultiplier": 0.0,
    "minicopterMultiplier": 0.0,
    "samMultiplier": 0.0,
    "scrapcopterMultiplier": 0.0,
    "sedanMultiplier": 0.0,
    "trapMultiplier": 0.0,
    "watchtowerMultiplier": 0.0
  },
  "Mutipliers": null,
  "Version": {
    "Major": 1,
    "Minor": 0,
    "Patch": 46
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
 Use "entityCupboardMultiplier" to set the amount of decay for entities in cupboard range.
 
 Set DestroyOnZero to true to enable destroying entities when health is zero.

 Set useJPipes if you have JPipes installed to ensure no decay for JPipes if NoDecay is configured with zero Multiplier for the JPipe building grade.

 Set protectVehicleOnLift true if you want to prevent decay for vehicles on a lift.  This should bypass the vehicleMultiplier.

 If protectedDays is set to any value other than zero, player buildings, etc. will only be protected if the user has been online sometime within that number of days.

 If usePlayerDatabase is true, and the plugin is available, the last connected time will be taken from that plugin.  Otherwise, we save that information in a new data file, ngpve_lastconnected.
 Set warningTime to a number greater than the default of 10.0 (ms) to limit the warnings fired off due to time to execute.  If your logs are consistently being filled with messages like the following:

     "(17:04:31) | [NoDecay] NoDecay.OnEntityTakeDamage on Rowboat took 15.04 ms to execute."

#### A Few Notes About Multipliers, decay.tick, etc.
   For any config file multiplier, you can set to 0 to disable decay for that item, 1 for normal decay, or a higher number for faster decay.  In other words, anything below 1 is slower down to 0 which is no decay.  Anything above 1 increases the rate of decay for that item and, yes, you can set numbers higher than 1.

   Decay is implemented by Rust based on the decay.tick value which defaults to 300 (5 minutes).  This specifies how often decay is processed on your server.

   The warning from Rust about Building Decaying cannot be bypassed at this time except by stocking a TC with the appropriate materials.  If a player adds materials to his TC, NoDecay will be bypassed altogether for their building, and normal upkeep costs will apply.  There are server variables available to adjust cost and decay rates, but that is outside of the scope of what NoDecay is intended to do and may also affect its operation.

### Permissions
    - nodecay.use   -- Required for NoDecay to work for a user, if the usePermission flag is set to true.
    - nodecay.admin -- Required to use the /nodecay commands below

### Commands
    - `nodecay log` -- Toggle logging of debug info to oxide log and rcon
    - `nodecay logm` -- Toggle logging of mundane debug info to oxide log and rcon
    - `nodecay info` -- Display current configuration (must still set manually and reload)

### Credits
    - **Deicide666ra** and **Piarb**, the original authors of this plugin
    - **Diesel**, for helping maintain the plugin

Thanks to Deicide666ra, the original author of this plugin, for his permission to continue his work.
