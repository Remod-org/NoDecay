## NoDecay for Rust (official)
Scales or disables decay of items, and deployables 

This is the official release of NoDecay.  Any other versions out there are forks or otherwise unrelated.

**No Decay** nullifies or scales down/up any decay damage applied to any item in game (except of small stashes). Each building tier has a different multiplier,  so do all other entities.

The default configuration does **NOT** affect *Twig decay* but nullifies all damage on all other items.

 **As of version 1.0.94** A new config, EnableUpkeep was added.  If false, and the plugin is enabled, and the player has not disabled NoDecay, TC resources will not be used for upkeep.  This is a long-requested feature and is the reason why blocking resource storage was added years ago.  Set to true for the old default.  We may see issues with this, so I am hoping for feedback one way or the other.

 **As of version 1.0.89** items can heal.  Thanks to jozzo402 on github for the suggestion.

 **As of version 1.0.68** users can enable or disable NoDecay for their owned entities

 **As of version 1.0.34** you can optionally also check for the presence of a deployed tool cupboard. Set requireCupboard to true. This will check for an attached cupboard for building blocks and a nearby cupboard for entities. For entities, use "cupboardCheckEntity: true" and "cupboardRange: number" to configure how far the entities can be from a cupboard before they will decay. The default is 30 game meters (?), which may or may not be enough for your needs. Adjust as desired.

 Note, the default is cupboardCheckEntity: false, which will skip checking for cupboards in range of entities. It will still check for blocks attached to cupboards, which should be more accurate.

### Configuration

<b>NOTE: The long-standing misspelling of Multipliers was fixed as of 1.0.46.  As of 1.0.86, Multipliers has been removed as well.  Only multipliers was actually in use since 1.0.63 but due to having the leave that upgrade in place, Multipliers was also still there.  Sorry for the confusion.</b>

New for 1.0.87 - non building block building parts such as doorways and doors, etc., now use the new building multiplier, which defaults to 0.  Previously, they were classified as deployables and were decaying at 10% by default.  Admins should run 'nodecay update' to reclassify them correctly, or wait until wipe.


```json
{
  "Debug": {
    "outputToRcon": false,
    "outputMundane": false,
    "logPosition": false
  },
  "Global": {
    "usePermission": false,
    "useTCOwnerToCheckPermission": false,
    "useBPAuthListForProtectedDays": false,
    "EnableGUI": true,
    "EnableUpkeep": false,
    "requireCupboard": false,
    "cupboardCheckEntity": false,
    "protectedDays": 0.0,
    "cupboardRange": 30.0,
    "useCupboardRange": false,
    "DestroyOnZero": true,
    "honorZoneManagerFlag": false,
    "blockCupboardResources": false,
    "blockCupboardWood": false,
    "blockCupboardStone": false,
    "blockCupboardMetal": false,
    "blockCupboardArmor": false,
    "healBuildings": false,
    "healEntities": false,
    "healPercentage": 0.01,
    "disableWarning": true,
    "disableLootWarning": false,
    "protectVehicleOnLift": true,
    "protectedDisplayTime": 44000.0,
    "warningTime": 10.0,
    "overrideZoneManager": [
      "vehicle",
      "balloon"
    ],
    "respondToActivationHooks": false
  },
  "multipliers": {
    "armored": 0.0,
    "attackcopter": 0.0,
    "balloon": 0.0,
    "barricade": 0.0,
    "bbq": 0.0,
    "boat": 0.0,
    "box": 0.0,
    "building": 0.0,
    "campfire": 0.0,
    "deployables": 0.1,
    "entityCupboard": 0.0,
    "furnace": 0.0,
    "highStoneWall": 0.0,
    "highWoodWall": 0.0,
    "horse": 0.0,
    "minicopter": 0.0,
    "mining": 0.0,
    "sam": 0.0,
    "scrapcopter": 0.0,
    "sedan": 0.0,
    "sheet": 0.0,
    "stone": 0.0,
    "trap": 0.0,
    "twig": 1.0,
    "vehicle": 0.0,
    "watchtower": 0.0,
    "water": 0.0,
    "wood": 0.0
  },
  "Version": {
    "Major": 1,
    "Minor": 0,
    "Patch": 94
  }
}
```

The default configuration above disables decay for all but twig.  For each multiplier, set to 1 for normal decay, 0 for no decay, and somewhere in between for reduced decay.

Set usePermission to true to require the nodecay.use permission to prevent decay.  If false, all players are protected by default.

If "useTCOwnerToCheckPermission" is set to true, check building privilege owner in addition to entity owner for decay item.

If "useBPAuthListForProtectedDays" is set to true, check for online players authed to a TC instead of simply TC owner (for protected days).

If "blockCupboardResources" is set to true, blocks stone, frags, and hqm from being added to a cupboard.

If "blockCupboardWood" is set to true, blocks wood from being added to a cupboard.

  - Use both of these to prevent all building materials from being added to cupboards.  Players will still get the Building Decaying warning but will not waste resources on upkeep since they are not necessary with NoDecay.
 
  - Use only blockCupboardWood to block wood and therefore upkeep on twig.  You may also set the following instead of blockCupboardResources:

If "blockCupboardStone" is set to true, blocks stone from being added to a cupboard.

If "blockCupboardMetal" is set to true, blocks metal frags from being added to a cupboard.

If "blockCupboardArmor" is set to true, blocks HQM from being added to a cupboard.
 
If "healBuildings" is set to true, damaged building blocks will be healed over time similar to what would occur with typical upkeep resources when present in a TC.

If "healEntities" is true, damaged entities will be healed over time.

"healPercentage" determines how quickly entities and blocks will recover from damage.  Every decay.tick (default 600 seconds), NoDecay will heal the item by a percentage of max health.  If max health of an item is 500, and healthPercentage is 0.01, the item health will increase by 5 every decay.tick.

Set EnableGUI to true to enable the previously always on overlay for the tool cupboard loot panel.

Set EnableUpkeep to true to use stored resources for upkeep.  If false, players can keep resources in their TC without them being consumed. 

Set requireCupboard to true to check for a cupboard to prevent decay.

Set cupboardCheckEntity to also check for entities in range of cupboards (i.e. not just foundations, etc.  This should work on doors and high walls, etc.

 Set cupboardRange to a desired value for the cupboardCheckEntity range.  If too high, may affect other user's stuff.  If set too low it may not protect external items if out of range.
  - NOTE: as of 1.0.80 this will always be used to check for buildings without a TC but in range of a cupboard.  In that case, the block owner will be checked to verify that the owner is listed in the authorizedPlayers list on the TC.

 Use "entityCupboardMultiplier" to set the amount of decay for entities in cupboard range.

 Set useCupboardRange to false to ignore the range setting above and simply use the building privilege for the entity.  This is likely more efficient and is the default as of 1.0.65.

 Set DestroyOnZero to true to enable destroying entities when health is zero.

 Set disableWarning to true to disable the "Building Decaying" warning.  This will be set to a default of 4400 minutes (73 hours) based on the value of protectedDisplayTime.  73 hours is enough to hit the default value shown for more than 72 hours of protection without NoDecay.  A warning will still be shown when viewing the contents of the TC.  But, as always, the building is protected anyway since that's what NoDecay is for.  Players may need to periodically open their TC to disable the warning again every couple of days.

 Set honorZoneManagerFlag if you have ZoneManager installed and wish to honor the NoDecay flag on ZoneManager zones.  This should, at least for NoDecay, skip all decay within a matching zone with that flag set.

 Set protectVehicleOnLift true if you want to prevent decay for vehicles on a lift.  This should bypass the vehicleMultiplier.

 If protectedDays is set to any value other than zero, player buildings, etc. will only be protected if the user has been online sometime within that number of days.

 Set warningTime to a number greater than the default of 10.0 (ms) to limit the warnings fired off due to time to execute.  If your logs are consistently being filled with messages like the following:

     "(17:04:31) | [NoDecay] NoDecay.OnEntityTakeDamage on Rowboat took 15.04 ms to execute."

#### A Few Notes About multipliers, decay.tick, etc.
   For any config file multiplier, you can set to 0 to disable decay for that item, 1 for normal decay, or a higher number for faster decay.  In other words, anything below 1 is slower down to 0 which is no decay.  Anything above 1 increases the rate of decay for that item and, yes, you can set numbers higher than 1.

   Decay is implemented by Rust based on the decay.tick value which defaults to 300 (5 minutes).  This specifies how often decay is processed on your server.

   The warning from Rust about Building Decaying cannot be bypassed at this time except by stocking a TC with the appropriate materials.  If a player adds materials to his TC, NoDecay will be bypassed altogether for their building, and normal upkeep costs will apply.  There are server variables available to adjust cost and decay rates, but that is outside of the scope of what NoDecay is intended to do and may also affect its operation.

### Permissions

    - nodecay.use   -- Required for NoDecay to work for a user, if the usePermission flag is set to true.
    - nodecay.admin -- Required to use the /nodecay commands below

### Commands
These commands work for any user regardless of permission:

    - `nodecay ?` -- For users to show current global as well as personal status for
        enable/disable of NoDecay
    - `nodecay off` -- For users to set their status as disabled.  In this case, decay
        will be standard for this user's owned items
    - `nodecay on` -- For users to set their status as enabled.  In this case, decay
        will be managed by NoDecay for this user's owned items

These commands only work for users with the nodecay.admin permission:

    - `nodecay log` -- Toggle logging of debug info to oxide log and rcon
    - `nodecay info` -- Display current configuration
        (must still set manually in config and reload)
    - `nodecay enable` -- Toggle global enable status
    - `nodecay update` -- Update the list of entities.  This is normally run in the
        background at each wipe for newly-introduced items.

### Developers
A couple of hooks have been implemented:

    - private bool NoDecayGet(ulong playerid=0)
        - Returns global enabled status if playerid == 0
        - Returns player status if playerid > 0

    - private object NoDecaySet(ulong playerid=0, bool status=true)
        - Sets global status if playerid == 0
        - Sets player status if playerid > 0

### Credits
    - **Deicide666ra** and **Piarb**, the original authors of this plugin
    - **Diesel**, for helping maintain the plugin

Thanks to Deicide666ra, the original author of this plugin, for his permission to continue his work.
