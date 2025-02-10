#region License
/*
Copyright RFC1920 <desolationoutpostpve@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation
files (the "Software"), to use the software subject to the following restrictions:

This copyright and permission notice shall be included in all copies or substantial portions of the Software.

The software must remain unmodified from the version(s) released by the author.

The software may not be redistributed or sold partially or in total without approval from the author.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR
IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
#endregion License
using System;
using System.Collections.Generic;
using System.Text;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoDecay", "RFC1920", "1.0.63", ResourceId = 1160)]
    //Original Credit to Deicide666ra/Piarb and Diesel_42o
    //Thanks to Deicide666ra for allowing me to continue his work on this plugin
    //Thanks to Steenamaroo for his help and support
    [Description("Scales or disables decay of items")]
    class NoDecay : RustPlugin
    {
        private ConfigData configData;
        private bool enabled = true;

        #region main
        private Dictionary<string, long> lastConnected = new Dictionary<string, long>();
        private Dictionary<string, List<string>> entityinfo = new Dictionary<string, List<string>>();

        [PluginReference]
        private readonly Plugin JPipes;

        void Init()
        {
            permission.RegisterPermission("nodecay.use", this);
            permission.RegisterPermission("nodecay.admin", this);
            LoadData();
            if (entityinfo.Count == 0) UpdateEnts();
        }

        void Loaded() => LoadConfigValues();

        private void OnEntitySaved(BuildingPrivlidge buildingPrivilege, BaseNetworkable.SaveInfo saveInfo)
        {
            if (configData.Global.disableWarning)
            {
                if (configData.Global.usePermission)
                {
                    var owner = buildingPrivilege.OwnerID.ToString();
                    if (permission.UserHasPermission(owner, "nodecay.use") || owner == "0")
                    {
                        if (owner != "0")
                        {
                            OutputRcon($"TC owner {owner} has NoDecay permission!");
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                saveInfo.msg.buildingPrivilege.protectedMinutes = configData.Global.protectedDisplayTime;
                saveInfo.msg.buildingPrivilege.upkeepPeriodMinutes = configData.Global.protectedDisplayTime;
            }
        }

        void OnUserConnected(IPlayer player) => OnUserDisconnected(player);
        void OnUserDisconnected(IPlayer player)
        {
            long lc = 0;
            lastConnected.TryGetValue(player.Id, out lc);
            if(lc > 0)
            {
                lastConnected[player.Id] = ToEpochTime(DateTime.UtcNow);
            }
            else
            {
                lastConnected.Add(player.Id, ToEpochTime(DateTime.UtcNow));
            }
            SaveData();
        }

        private void LoadData()
        {
            entityinfo = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, List<string>>>(Name + "/entityinfo");
            lastConnected = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, long>>(Name + "/lastconnected");
        }
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name + "/entityinfo", entityinfo);
            Interface.Oxide.DataFileSystem.WriteObject(Name + "/lastconnected", lastConnected);
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (!enabled) return null;
            if (entity == null || hitInfo == null) return null;
            if (!hitInfo.damageTypes.Has(Rust.DamageType.Decay)) return null;

            float damageAmount = 0f;
            DateTime tick = DateTime.Now;
            string entity_name = entity.LookupPrefab().name;
//            Puts($"Decay Entity: {entity_name}");
            string owner = entity.OwnerID.ToString();
            bool mundane = false;
            bool isBlock = false;

            if (configData.Global.usePermission)
            {
                if (permission.UserHasPermission(owner, "nodecay.use") || owner == "0")
                {
                    if (owner != "0")
                    {
                        OutputRcon($"{entity_name} owner {owner} has NoDecay permission!");
                    }
                }
                else
                {
                    OutputRcon($"{entity_name} owner {owner} does NOT have NoDecay permission.  Standard decay in effect.");
                    return null;
                }
            }
            if(configData.Global.protectedDays > 0 && entity.OwnerID > 0)
            {
                long lc = 0;
                lastConnected.TryGetValue(entity.OwnerID.ToString(), out lc);
                if (lc > 0)
                {
                    long now = ToEpochTime(DateTime.UtcNow);
                    float days = Math.Abs((now - lc) / 86400);
                    if (days > configData.Global.protectedDays)
                    {
                        OutputRcon($"Allowing decay for owner offline for {configData.Global.protectedDays.ToString()} days");
                        return null;
                    }
                    else
                    {
                        OutputRcon($"Owner was last connected {days.ToString()} days ago and is still protected...");
                    }
                }
            }

            try
            {
                float before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);

                if (entity is BuildingBlock)
                {
                    if (configData.Global.useJPipes)
                    {
                        if ((bool)JPipes?.Call("IsPipe", entity))
                        {
                            if ((bool)JPipes?.Call("IsNoDecayEnabled"))
                            {
                                OutputRcon("Found a JPipe with nodecay enabled");
                                hitInfo.damageTypes.Scale(Rust.DamageType.Decay, 0f);
                                return null;
                            }
                        }
                    }

                    damageAmount = ProcessBuildingDamage(entity, before);
                    isBlock = true;
                }
                else if (entity is ModularCar)
                {
                    var garage = entity.GetComponentInParent<ModularCarGarage>();
                    if (garage != null && configData.Global.protectVehicleOnLift)
                    {
                        return null;
                    }
                }
                else
                {
                    // Main check for non-building entities/deployables
                    foreach (KeyValuePair<string, List<string>> entities in entityinfo)
                    {
                        if (entities.Value.Contains(entity_name))
                        {
                            damageAmount = before * configData.multipliers[entities.Key];
                            break;
                        }
                    }
                }

                // Check non-building entities for cupboard in range
                if (configData.Global.requireCupboard && configData.Global.cupboardCheckEntity && !isBlock)
                {
                    // Verify that we should check for a cupboard and ensure that one exists.
                    // If so, multiplier will be set to entityCupboardMultiplier.
                    OutputRcon($"NoDecay checking for local cupboard.", mundane);

                    if (CheckCupboardEntity(entity, mundane))
                    {
                        damageAmount = before * configData.multipliers["entityCupboard"];
                    }
                }

                NextTick(() =>
                {
                    OutputRcon($"Decay ({entity_name}) before: {before} after: {damageAmount}, item health {entity.health.ToString()}", mundane);
                    entity.health -= damageAmount;
                    if(entity.health == 0 && configData.Global.DestroyOnZero)
                    {
                        OutputRcon($"Entity {entity_name} completely decayed - destroying!", mundane);
                        if(entity == null) return;
                        entity.Kill(BaseNetworkable.DestroyMode.Gib);
                    }
                });
                return true; // Cancels this hook for any of the entities above unless unsupported (for decay only).
            }
            finally
            {
                double ms = (DateTime.Now - tick).TotalMilliseconds;
                if(ms > configData.Global.warningTime || configData.Debug.outputMundane) Puts($"NoDecay.OnEntityTakeDamage on {entity_name} took {ms} ms to execute.");
            }
        }

        // Workaround for car chassis that won't die
        private void OnEntityDeath(ModularCar car, HitInfo hitinfo)
        {
            OutputRcon("Car died!  Checking for associated parts...");
            List<BaseEntity> ents = new List<BaseEntity>();
            Vis.Entities(car.transform.position, 1f, ents);
            foreach(var ent in ents)
            {
                if(ent.name.Contains("module_car_spawned") && !ent.IsDestroyed)
                {
                    OutputRcon($"Killing {ent.ShortPrefabName}");
                    ent.Kill(BaseNetworkable.DestroyMode.Gib);
                }
            }
        }

        private void OnNewSave()
        {
            UpdateEnts();
        }

        private void UpdateEnts()
        {
            entityinfo["balloon"] = new List<string>();
            entityinfo["barricade"] = new List<string>();
            entityinfo["bbq"] = new List<string>();
            entityinfo["boat"] = new List<string>();
            entityinfo["box"] = new List<string>();
            entityinfo["campfire"] = new List<string>();
            entityinfo["deployables"] = new List<string>();
            entityinfo["furnace"] = new List<string>();
            entityinfo["horse"] = new List<string>();
            entityinfo["minicopter"] = new List<string>();
            entityinfo["sam"] = new List<string>();
            entityinfo["scrapcopter"] = new List<string>();
            entityinfo["sedan"] = new List<string>();
            entityinfo["trap"] = new List<string>();
            entityinfo["vehicle"] = new List<string>();
            entityinfo["watchtower"] = new List<string>();
            entityinfo["stonewall"] = new List<string>();
            entityinfo["woodwall"] = new List<string>();
            entityinfo["mining"] = new List<string>();

            List<string> names = new List<string>();
            foreach (var ent in Resources.FindObjectsOfTypeAll<BaseCombatEntity>())
            {
                string entity_name = ent.ShortPrefabName.ToLower();
                if (entity_name == "cupboard.tool.deployed") continue;
                if (entity_name == null) continue;
                if (names.Contains(entity_name)) continue; // Saves 20-30 seconds of processing time.
                names.Add(entity_name);
                //OutputRcon($"Checking {entity_name}");

                if (entity_name.Contains("campfire") || entity_name.Contains("skull_fire_pit"))
                {
                    entityinfo["campfire"].Add(entity_name);
                }
                else if (entity_name.Contains("box") || entity_name.Contains("coffin"))
                {
                    entityinfo["box"].Add(entity_name);
                }
                else if (entity_name.Contains("deployed") || entity_name.Contains("shutter") ||
                         (entity_name.Contains("door") && !entity_name.Contains("doorway")) ||
                         entity_name.Contains("reinforced") || entity_name.Contains("shopfront") ||
                         entity_name.Contains("bars") || entity_name.Contains("netting") ||
                         entity_name.Contains("hatch") || entity_name.Contains("garagedoor") ||
                         entity_name.Contains("cell") || entity_name.Contains("fence") ||
                         entity_name.Contains("grill") || entity_name.Contains("speaker") ||
                         entity_name.Contains("strobe") || entity_name.Contains("strobe") ||
                         entity_name.Contains("fog") || entity_name.Contains("shopfront") ||
                         entity_name.Contains("wall.window.bars") ||
                         entity_name.Contains("candle") || entity_name.Contains("hatchet") ||
                         entity_name.Contains("graveyard") || entity_name.Contains("water") ||
                         entity_name.Contains("jackolantern") || entity_name.Contains("composter") ||
                         entity_name.Contains("workbench"))
                {
                    entityinfo["deployables"].Add(entity_name);
                }
                else if (entity_name.Contains("furnace"))
                {
                    entityinfo["furnace"].Add(entity_name);
                }
                else if (entity_name.Contains("sedan"))
                {
                    entityinfo["sedan"].Add(entity_name);
                }
                else if (entity_name.Contains("sam_static"))
                {
                    entityinfo["sam"].Add(entity_name);
                }
                else if (entity_name.Contains("balloon"))
                {
                    entityinfo["balloon"].Add(entity_name);
                }
                else if (entity_name.Contains("bbq"))
                {
                    entityinfo["bbq"].Add(entity_name);
                }
                else if (entity_name.Contains("watchtower"))
                {
                    entityinfo["watchtower"].Add(entity_name);
                }
                else if (entity_name.Contains("beartrap") || entity_name.Contains("landmine") || entity_name.Contains("spikes.floor"))
                {
                    entityinfo["trap"].Add(entity_name);
                }
                else if (entity_name.Contains("barricade"))
                {
                    entityinfo["barricade"].Add(entity_name);
                }
                else if (entity_name.Contains("external.high.stone"))
                {
                    entityinfo["stonewall"].Add(entity_name);
                }
                else if (entity_name.Contains("external.high.wood") || entity_name.Contains("external.high.ice") || entity_name.Contains("icewall"))
                {
                    entityinfo["woodwall"].Add(entity_name);
                }
                else if (entity_name.Contains("mining"))
                {
                    entityinfo["mining"].Add(entity_name);
                }
                else if (entity_name.Contains("rowboat") || entity_name.Contains("rhib") || entity_name.Contains("kayak"))
                {
                    entityinfo["boat"].Add(entity_name);
                }
                else if (entity_name.Contains("minicopter"))
                {
                    entityinfo["minicopter"].Add(entity_name);
                }
                else if (entity_name.Contains("horse"))
                {
                    entityinfo["horse"].Add(entity_name);
                }
                else if (entity_name.Contains("scraptransport"))
                {
                    entityinfo["scrapcopter"].Add(entity_name);
                }
                else if (entity_name.Contains("vehicle") ||
                        entity_name.Contains("chassis_") ||
                        entity_name.Contains("1module_") ||
                        entity_name.Contains("2module_") ||
                        entity_name.Contains("3module_") ||
                        entity_name.Contains("4module_"))
                {
                    entityinfo["vehicle"].Add(entity_name);
                }
            }
            SaveData();
        }


        private float ProcessBuildingDamage(BaseEntity entity, float before)
        {
            var block = entity as BuildingBlock;
            float multiplier = 1.0f;
            float damageAmount = 1.0f;
            bool isHighWall = block.LookupPrefab().name.Contains("wall.external");
            bool isHighGate = block.LookupPrefab().name.Contains("gates.external");

            string type = null;
            bool hascup = true; // Assume true (has cupboard or we don't require one)

            OutputRcon($"NoDecay checking for block damage to {block.LookupPrefab().name}");

            // Verify that we should check for a cupboard and ensure that one exists.
            // If not, multiplier will be standard of 1.0f (hascup true).
            if(configData.Global.requireCupboard == true)
            {

                OutputRcon($"NoDecay checking for local cupboard.");
                hascup = CheckCupboardBlock(block, entity.LookupPrefab().name, block.grade.ToString().ToLower());
            }
            else
            {
                OutputRcon($"NoDecay not checking for local cupboard.");
            }

            switch(block.grade)
            {
                case BuildingGrade.Enum.Twigs:
                    if(hascup) multiplier = configData.multipliers["twig"];
                    type = "twig";
                    break;
                case BuildingGrade.Enum.Wood:
                    if(isHighWall)
                    {
                        if(hascup) multiplier = configData.multipliers["highWoodWall"];
                        type = "high wood wall";
                    }
                    else if(isHighGate)
                    {
                        if(hascup) multiplier = configData.multipliers["highWoodWall"];
                        type = "high wood gate";
                    }
                    else
                    {
                        if(hascup) multiplier = configData.multipliers["wood"];
                        type = "wood";
                    }
                    break;
                case BuildingGrade.Enum.Stone:
                    if(isHighWall)
                    {
                        if(hascup) multiplier = configData.multipliers["highStoneWall"];
                        type = "high stone wall";
                    }
                    else if(isHighGate)
                    {
                        if(hascup) multiplier = configData.multipliers["highStoneWall"];
                        type = "high stone gate";
                    }
                    else
                    {
                        if(hascup) multiplier = configData.multipliers["stone"];
                        type = "stone";
                    }
                    break;
                case BuildingGrade.Enum.Metal:
                    if(hascup) multiplier = configData.multipliers["sheet"];
                    type = "sheet";
                    break;
                case BuildingGrade.Enum.TopTier:
                    if(hascup) multiplier = configData.multipliers["armored"];
                    type = "armored";
                    break;
                default:
                    OutputRcon($"Decay ({type}) has unknown grade type.");
                    type = "unknown";
                    break;
            }

            damageAmount = before * multiplier;

            OutputRcon($"Decay ({type}) before: {before} after: {damageAmount}");
            return damageAmount;
        }

        // Check that a building block is owned by/attached to a cupboard
        private bool CheckCupboardBlock(BuildingBlock block, string ename = "unknown", string grade = "")
        {
            BuildingManager.Building building = block.GetBuilding();

            OutputRcon($"CheckCupboardBlock:   Checking for cupboard connected to {grade} {ename}.");

            if(building != null)
            {
                // cupboard overlap.  Block safe from decay.
                if(building.GetDominatingBuildingPrivilege() == null)
                {
                    OutputRcon($"CheckCupboardBlock:     Block NOT owned by cupboard!");
                    return false;
                }

                OutputRcon($"CheckCupboardBlock:     Block owned by cupboard!");
                return true;
            }
            else
            {
                OutputRcon($"CheckCupboardBlock:     Unable to find cupboard.");
            }
            return false;
        }

        // Non-block entity check
        bool CheckCupboardEntity(BaseEntity entity, bool mundane = false)
        {
            int targetLayer = LayerMask.GetMask("Construction", "Construction Trigger", "Trigger", "Deployed");
            List<BuildingPrivlidge> cups = new List<BuildingPrivlidge>();
            Vis.Entities(entity.transform.position, configData.Global.cupboardRange, cups, targetLayer);

            OutputRcon($"CheckCupboardEntity:   Checking for cupboard within {configData.Global.cupboardRange.ToString()}m of {entity.ShortPrefabName}.", mundane);

            if(cups.Count > 0)
            {
                // cupboard overlap.  Entity safe from decay.
                OutputRcon($"CheckCupboardEntity:     Found entity layer in range of cupboard!", mundane);
                return true;
            }

            OutputRcon($"CheckCupboardEntity:     Unable to find entity layer in range of cupboard.", mundane);
            return false;
        }

        // Prevent players from adding building resources to cupboard if so configured
        private object CanMoveItem(Item item, PlayerInventory inventory, uint targetContainer, int targetSlot)
        {
            if(item == null) return null;
            if(targetContainer == 0) return null;
            if(targetSlot == 0) return null;
            ItemContainer container = inventory.FindContainer(targetContainer);

            if (!(configData.Global.blockCupboardResources || configData.Global.blockCupboardWood)) return null;
            if (!(configData.Global.blockCupboardStone || configData.Global.blockCupboardMetal || configData.Global.blockCupboardArmor)) return null;

            try
            {
                var cup = container.entityOwner as BaseEntity;
                if (!cup.name.Contains("cupboard.tool")) return null;

                string res = item?.info?.shortname;
                if (res.Contains("wood") && configData.Global.blockCupboardWood)
                {
                    OutputRcon($"Player tried to add {res} to a cupboard!");
                    return false;
                }
                else if ((res.Contains("stones") || res.Contains("metal.frag") || res.Contains("metal.refined")) && configData.Global.blockCupboardResources)
                {
                    OutputRcon($"Player tried to add {res} to a cupboard!");
                    return false;
                }
                else if (
                    (res.Contains("stones") && configData.Global.blockCupboardStone)
                    || (res.Contains("metal.frag") && configData.Global.blockCupboardMetal)
                    || (res.Contains("metal.refined") && configData.Global.blockCupboardArmor))
                {
                    OutputRcon($"Player tried to add {res} to a cupboard!");
                    return false;
                }
            }
            catch { }
            return null;
        }
        #endregion

        #region command
        [ChatCommand("nodecay")]
        void CmdInfo(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "nodecay.admin")) return;
            if(args.Length > 0)
            {
                if(args[0] == "enable")
                {
                    enabled = !enabled;
                    SendReply(player, $"NoDecay enabled set to {enabled.ToString()}");
                }
                else if(args[0] == "log")
                {
                    configData.Debug.outputToRcon = !configData.Debug.outputToRcon;
                    SendReply(player, $"Debug logging set to {configData.Debug.outputToRcon.ToString()}");
                }
                else if (args[0] == "update")
                {
                    UpdateEnts();
                }
                else if(args[0] == "info")
                {
                    string info = "NoDecay current settings:\n  Multipliers:";
                    info += "\n\tarmored: " + configData.multipliers["armored"];ToString();
                    info += "\n\tballoon: " + configData.multipliers["balloon"];ToString();
                    info += "\n\tbarricade: " + configData.multipliers["barricade"];ToString();
                    info += "\n\tbbq: " + configData.multipliers["bbq"];ToString();
                    info += "\n\tboat: " + configData.multipliers["boat"];ToString();
                    info += "\n\tbox: " + configData.multipliers["box"];ToString();
                    info += "\n\tcampfire" + configData.multipliers["campfire"];ToString();
                    info += "\n\tdeployables: " + configData.multipliers["deployables"];ToString();
                    info += "\n\tentityCupboard: " + configData.multipliers["entityCupboard"];ToString();
                    info += "\n\tfurnace: " + configData.multipliers["furnace"];ToString();
                    info += "\n\thighWoodWall: " + configData.multipliers["highWoodWall"];ToString();
                    info += "\n\thighStoneWall: " + configData.multipliers["highStoneWall"];ToString();
                    info += "\n\thorse: " + configData.multipliers["horse"];ToString();
                    info += "\n\tminicopter: " + configData.multipliers["minicopter"];ToString();
                    info += "\n\tsam: " + configData.multipliers["sam"];ToString();
                    info += "\n\tscrapcopter: " + configData.multipliers["scrapcopter"];ToString();
                    info += "\n\tsedan: " + configData.multipliers["sedan"];ToString();
                    info += "\n\tsheet: " + configData.multipliers["sheet"];ToString();
                    info += "\n\tstone: " + configData.multipliers["stone"];ToString();
                    info += "\n\ttrap: " + configData.multipliers["trap"];ToString();
                    info += "\n\ttwig: " + configData.multipliers["twig"];ToString();
                    info += "\n\tvehicle: " + configData.multipliers["vehicle"];ToString();
                    info += "\n\twatchtower: " + configData.multipliers["watchtower"];ToString();
                    info += "\n\twood: " + configData.multipliers["wood"];ToString();

                    info += "\n\n\tEnabled: " + enabled.ToString();
                    info += "\n\tdisableWarning: " + configData.Global.disableWarning.ToString();
                    info += "\n\tprotectedDays: " + configData.Global.protectedDays.ToString();
                    info += "\n\tprotectVehicleOnLift: " + configData.Global.protectVehicleOnLift.ToString();
                    info += "\n\tusePermission: " + configData.Global.usePermission.ToString();
                    info += "\n\trequireCupboard: " + configData.Global.requireCupboard.ToString();
                    info += "\n\tCupboardEntity: " + configData.Global.cupboardCheckEntity.ToString();
                    info += "\n\tcupboardRange: " + configData.Global.cupboardRange.ToString();
                    info += "\n\tblockCupboardResources: " + configData.Global.blockCupboardResources.ToString();
                    info += "\n\tblockCupboardWood: " + configData.Global.blockCupboardWood.ToString();
                    info += "\n\tblockCupboardStone: " + configData.Global.blockCupboardStone.ToString();
                    info += "\n\tblockCupboardMetal: " + configData.Global.blockCupboardMetal.ToString();
                    info += "\n\tblockCupboardArmor: " + configData.Global.blockCupboardArmor.ToString();

                    SendReply(player, info);
                    info = null;
                }
            }
        }
        #endregion

        #region helpers
        // From PlayerDatabase
        private long ToEpochTime(DateTime dateTime)
        {
            var date = dateTime.ToUniversalTime();
            var ticks = date.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, 0).Ticks;
            var ts = ticks / TimeSpan.TicksPerSecond;
            return ts;
        }

        [HookMethod("SendHelpText")]
        private void SendHelpText(BasePlayer player)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<color=#05eb59>" + Name + " " + Version + "</color> · Controls decay\n");
            sb.Append("  · ").AppendLine($"twig={configData.multipliers["twig"]} - campfire={configData.multipliers["campfire"]}");
            sb.Append("  · ").Append($"wood ={ configData.multipliers["wood"]} - stone ={ configData.multipliers["stone"]} - sheet ={ configData.multipliers["sheet"]} - armored ={ configData.multipliers["armored"]}\n");

            if(configData.Global.requireCupboard == true)
            {
                if(configData.Global.cupboardCheckEntity == true)
                {
                    string range = configData.Global.cupboardRange.ToString();
                    sb.Append("  · ").Append($"cupboard check ={ true } - entity range ={ range }");
                }
                else
                {
                    sb.Append("  · ").Append($"cupboard check ={ true } - entity check ={ false }");
                }
            }
            else
            {
                sb.Append("  · ").Append($"cupboard check ={ false }");
            }
            player.ChatMessage(sb.ToString());
        }

        // Just here to cleanup the code a bit
        private void OutputRcon(string message, bool mundane = false)
        {
            if(configData.Debug.outputToRcon)
            {
                if(!mundane) Puts($"{message}");
                else if(mundane && configData.Debug.outputMundane) Puts($"{message}");
            }
        }
        #endregion

        #region config
        private class ConfigData
        {
            public Debug Debug = new Debug();
            public Global Global = new Global();
            public SortedDictionary<string, float> multipliers;
//            public Multipliers Mutipliers = new Multipliers(); // Temporary from old configs
            public Multipliers Multipliers = new Multipliers();
            public VersionNumber Version;
        }

        private class Debug
        {
            public bool outputToRcon;
            public bool outputMundane;
        }

        private class Global
        {
            public bool usePermission = false;
            public bool requireCupboard = false;
            public bool cupboardCheckEntity = false;
            public float protectedDays = 0;
            public float cupboardRange = 30f;
            public bool DestroyOnZero = true;
            public bool useJPipes = false;
            public bool blockCupboardResources = false;
            public bool blockCupboardWood = false;
            public bool blockCupboardStone = false;
            public bool blockCupboardMetal = false;
            public bool blockCupboardArmor = false;
            public bool disableWarning = true;
            public bool protectVehicleOnLift = true;
            public float protectedDisplayTime = 4400;
            public double warningTime = 10;
        }

        // Legacy
        private class Multipliers
        {
            public float entityCupboardMultiplier = 0f;
            public float twigMultiplier = 1.0f;
            public float woodMultiplier = 0f;
            public float stoneMultiplier = 0f;
            public float sheetMultiplier = 0f;
            public float armoredMultiplier = 0f;
            public float baloonMultiplier = 0f;
            public float barricadeMultiplier = 0f;
            public float bbqMultiplier = 0f;
            public float boatMultiplier = 0f;
            public float boxMultiplier = 0f;
            public float campfireMultiplier = 0f;
            public float deployablesMultiplier = 0f;
            public float furnaceMultiplier = 0f;
            public float highWoodWallMultiplier = 0f;
            public float highStoneWallMultiplier = 0f;
            public float horseMultiplier = 0f;
            public float minicopterMultiplier = 0f;
            public float samMultiplier = 0f;
            public float scrapcopterMultiplier = 0f;
            public float sedanMultiplier = 0f;
            public float trapMultiplier = 0f;
            public float vehicleMultiplier = 0f;
            public float watchtowerMultiplier = 0f;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file");
            configData = new ConfigData
            {
                Version = Version,
                multipliers = new SortedDictionary<string, float>()
                {
                    { "armored", 0f },
                    { "balloon", 0f },
                    { "barricade", 0f },
                    { "bbq", 0f },
                    { "boat", 0f },
                    { "box", 0f },
                    { "campfire", 0f },
                    { "entityCupboard", 0f },
                    { "furnace", 0f },
                    { "highWoodWall", 0f },
                    { "highStoneWall", 0f },
                    { "horse", 0f },
                    { "minicopter", 0f },
                    { "mining", 0f },
                    { "sam", 0f },
                    { "scrapcopter", 0f },
                    { "sedan", 0f },
                    { "sheet", 0f },
                    { "stone", 0f },
                    { "twig", 1.0f },
                    { "trap", 0f },
                    { "vehicle", 0f },
                    { "watchtower", 0f },
                    { "wood", 0f },
                    { "deployables", 0.1f } // For all others not listed
                }
            };
            SaveConfig(configData);
        }

        void LoadConfigValues()
        {
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < new VersionNumber(1, 0, 63))
            {
                configData.multipliers = new SortedDictionary<string, float>()
                {
                    { "entityCupboard", configData.Multipliers.entityCupboardMultiplier },
                    { "twig", configData.Multipliers.twigMultiplier },
                    { "wood", configData.Multipliers.woodMultiplier },
                    { "stone", configData.Multipliers.stoneMultiplier },
                    { "sheet", configData.Multipliers.sheetMultiplier },
                    { "armored", configData.Multipliers.armoredMultiplier },
                    { "balloon", configData.Multipliers.baloonMultiplier },
                    { "barricade", configData.Multipliers.barricadeMultiplier },
                    { "bbq", configData.Multipliers.bbqMultiplier },
                    { "boat", configData.Multipliers.boatMultiplier },
                    { "box", configData.Multipliers.boxMultiplier },
                    { "campfire", configData.Multipliers.campfireMultiplier },
                    { "furnace", configData.Multipliers.furnaceMultiplier },
                    { "highWoodWall", configData.Multipliers.highWoodWallMultiplier },
                    { "highStoneWall", configData.Multipliers.highStoneWallMultiplier },
                    { "horse", configData.Multipliers.horseMultiplier },
                    { "minicopter", configData.Multipliers.minicopterMultiplier },
                    { "sam", configData.Multipliers.samMultiplier },
                    { "scrapcopter", configData.Multipliers.scrapcopterMultiplier },
                    { "sedan", configData.Multipliers.sedanMultiplier },
                    { "trap", configData.Multipliers.trapMultiplier },
                    { "vehicle", configData.Multipliers.vehicleMultiplier },
                    { "watchtower", configData.Multipliers.watchtowerMultiplier },
                    { "deployables", configData.Multipliers.deployablesMultiplier } // For all others not listed
                };
                configData.Multipliers = null;
            }
            configData.Version = Version;

            SaveConfig(configData);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion
    }
}
