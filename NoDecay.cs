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
using System.Globalization;
using System.Text;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoDecay", "RFC1920", "1.0.73", ResourceId = 1160)]
    //Original Credit to Deicide666ra/Piarb and Diesel_42o
    //Thanks to Deicide666ra for allowing me to continue his work on this plugin
    [Description("Scales or disables decay of items")]
    internal class NoDecay : RustPlugin
    {
        private ConfigData configData;
        private bool enabled = true;

        #region main
        private Dictionary<string, long> lastConnected = new Dictionary<string, long>();
        private List<ulong> disabled = new List<ulong>();
        private Dictionary<string, List<string>> entityinfo = new Dictionary<string, List<string>>();

        private const string permNoDecayUse = "nodecay.use";
        private const string permNoDecayAdmin = "nodecay.admin";
        private const string TCOVR = "nodecay.overlay";

        [PluginReference]
        private readonly Plugin ZoneManager, GridAPI, JPipes;

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));
        private void LMessage(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ndisoff"] = "NoDecay has been turned OFF for your owned entities and buildings",
                ["ndison"] = "NoDecay has been turned ON for your owned entities and buildings",
                ["ndstatus"] = "NoDecay enabled set to {0}",
                ["nddebug"] = "Debug logging set to {0}",
                ["perm"] = "You have permission to use NoDecay.",
                ["noperm"] = "You DO NOT have permission to use NoDecay.",
                ["protby"] = "Protected by NoDecay",
                ["ndsettings"] = "NoDecay current settings:\n  Multipliers:"
            }, this);
        }
        #endregion

        private void Init()
        {
            AddCovalenceCommand("nodecay", "CmdInfo");
            permission.RegisterPermission(permNoDecayUse, this);
            permission.RegisterPermission(permNoDecayAdmin, this);
            LoadData();
            if (entityinfo.Count == 0) UpdateEnts();
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, TCOVR);
            }
        }

        private void OnServerInitialized()
        {
            // Workaround for no decay on horses, even if set to decay here
            if (configData.multipliers["horse"] > 0)
            {
                float newdecaytime = (180f / configData.multipliers["horse"]) - 180f;

                foreach (RidableHorse horse in Resources.FindObjectsOfTypeAll<RidableHorse>())
                {
                    if (horse.net == null) continue;
                    if (horse.IsHitched() || horse.IsDestroyed) continue;

                    if (newdecaytime > 0)
                    {
                        OutputRcon($"Adding {Math.Floor(newdecaytime).ToString()} minutes of decay time to horse {horse.net.ID.ToString()}, now {Math.Floor(180f + newdecaytime).ToString()} minutes", true);
                        horse.AddDecayDelay(newdecaytime);
                    }
                    else
                    {
                        OutputRcon($"Subtracting {Math.Abs(Math.Floor(newdecaytime)).ToString()} minutes of decay time from horse {horse.net.ID.ToString()}, now {Math.Floor(180f + newdecaytime).ToString()} minutes", true);
                        //horse.nextDecayTime = Time.time + newdecaytime;
                        horse.AddDecayDelay(newdecaytime);
                    }

                    horse.SetDecayActive(true);
                }
            }
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (!configData.Global.disableLootWarning) return null;
            if (!permission.UserHasPermission(player.UserIDString, permNoDecayUse) && configData.Global.usePermission) return null;
            if (container == null) return null;
            var privs = container.GetComponentInParent<BuildingPrivlidge>();
            if (privs == null) return null;

            TcOverlay(player, privs);
            return null;
        }

        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if (!configData.Global.disableLootWarning) return;
            if (!permission.UserHasPermission(player.UserIDString, permNoDecayUse) && configData.Global.usePermission) return;
            if (entity == null) return;
            if (entity.GetComponentInParent<BuildingPrivlidge>() == null) return;

            CuiHelper.DestroyUi(player, TCOVR);
        }

        private void TcOverlay(BasePlayer player, BaseEntity entity)
        {
            CuiHelper.DestroyUi(player, TCOVR);

            CuiElementContainer container = UI.Container(TCOVR, UI.Color("3E3C37", 1f), "0.651 0.5", "0.946 0.532", true, "Overlay");
            UI.Label(ref container, TCOVR, UI.Color("#cacaca", 1f), Lang("protby"), 14, "0 0", "1 1");

            CuiHelper.AddUi(player, container);
        }

        private void Loaded() => LoadConfigValues();

        private void OnEntitySaved(BuildingPrivlidge buildingPrivilege, BaseNetworkable.SaveInfo saveInfo)
        {
            if (configData.Global.disableWarning)
            {
                if (configData.Global.usePermission)
                {
                    string owner = buildingPrivilege.OwnerID.ToString();
                    if (permission.UserHasPermission(owner, permNoDecayUse) || owner == "0")
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
                if (disabled.Contains(buildingPrivilege.OwnerID))
                {
                    OutputRcon($"TC owner {buildingPrivilege.OwnerID.ToString()} has disabled NoDecay.");
                    return;
                }

                saveInfo.msg.buildingPrivilege.protectedMinutes = configData.Global.protectedDisplayTime;
                saveInfo.msg.buildingPrivilege.upkeepPeriodMinutes = configData.Global.protectedDisplayTime;
            }
        }

        private void OnUserConnected(IPlayer player) => OnUserDisconnected(player);

        private void OnUserDisconnected(IPlayer player)
        {
            long lc = 0;
            lastConnected.TryGetValue(player.Id, out lc);
            if (lc > 0)
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
            disabled = Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>(Name + "/disabled");
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name + "/entityinfo", entityinfo);
            Interface.Oxide.DataFileSystem.WriteObject(Name + "/lastconnected", lastConnected);
            Interface.Oxide.DataFileSystem.WriteObject(Name + "/disabled", disabled);
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
//            Puts(entity.name);
            if (!enabled) return null;
            if (entity == null || hitInfo == null) return null;
            if (!hitInfo.damageTypes.Has(Rust.DamageType.Decay)) return null;

            float damageAmount = 0f;
            DateTime tick = DateTime.Now;
            string entity_name = entity.LookupPrefab().name.ToLower();
            //Puts($"Decay Entity: {entity_name}");
            string owner = entity.OwnerID.ToString();
            bool mundane = false;
            bool isBlock = false;

            if (configData.Global.usePermission)
            {
                if (permission.UserHasPermission(owner, permNoDecayUse) || owner == "0")
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
                if (disabled.Contains(entity.OwnerID))
                {
                    OutputRcon($"Entity owner {entity.OwnerID.ToString()} has disabled NoDecay.");
                    return null;
                }
            }
            if (configData.Global.protectedDays > 0 && entity.OwnerID > 0)
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
                    if (configData.Global.useJPipes && JPipes && (bool)JPipes?.Call("IsPipe", entity) && (bool)JPipes?.Call("IsNoDecayEnabled"))
                    {
                        OutputRcon("Found a JPipe with nodecay enabled");
                        hitInfo.damageTypes.Scale(Rust.DamageType.Decay, 0f);
                        return null;
                    }

                    damageAmount = ProcessBuildingDamage(entity, before);
                    isBlock = true;
                }
                else if (entity is ModularCar)
                {
                    ModularCarGarage garage = entity.GetComponentInParent<ModularCarGarage>();
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
                        //private Dictionary<string, List<string>> entityinfo = new Dictionary<string, List<string>>();
                        if (entities.Value.Contains(entity_name))
                        {
                            if (entities.Key.Equals("vehicle") || entities.Key.Equals("boat") || entities.Key.Equals("balloon") || entities.Key.Equals("horse"))
                            {
                                mundane = true;
                            }
                            OutputRcon($"Found {entity_name} listed in {entities.Key}", mundane);
                            if (configData.multipliers.ContainsKey(entities.Key))
                            {
                                damageAmount = before * configData.multipliers[entities.Key];
                                break;
                            }
                        }
                    }
                }

                // Check non-building entities for cupboard in range
                if (configData.Global.requireCupboard && configData.Global.cupboardCheckEntity && !isBlock)
                {
                    // Verify that we should check for a cupboard and ensure that one exists.
                    // If so, multiplier will be set to entityCupboardMultiplier.
                    OutputRcon("NoDecay checking for local cupboard.", mundane);

                    if (CheckCupboardEntity(entity, mundane))
                    {
                        damageAmount = before * configData.multipliers["entityCupboard"];
                    }
                }

                string pos = "";
                string zones = "";
                bool inzone = false;
                if (configData.Debug.logPosition)
                {
                    pos = $" at {PositionToGrid(entity.transform.position)} {entity.transform.position.ToString()}";
                    string[] zonedata = GetEntityZones(entity);
                    if (zonedata.Length > 0)
                    {
                        inzone = true;
                        zones = string.Join(",", zonedata);
                    }
                }

                NextTick(() =>
                {
                    OutputRcon($"Decay [{entity_name}{pos} - {entity.net.ID.ToString()}] before: {before} after: {damageAmount}, item health {entity.health.ToString()}", mundane);
                    if (inzone)
                    {
                        OutputRcon($"Decay [{entity_name}] FOUND overlapping ZoneManager zone(s): {zones}", mundane);
                    }
                    entity.health -= damageAmount;
                    if (entity.health == 0 && configData.Global.DestroyOnZero)
                    {
                        OutputRcon($"Entity {entity_name}{pos} completely decayed - destroying!", mundane);
                        if (entity == null) return;
                        entity.Kill(BaseNetworkable.DestroyMode.Gib);
                    }
                });
                return true; // Cancels this hook for any of the entities above unless unsupported (for decay only).
            }
            finally
            {
                double ms = (DateTime.Now - tick).TotalMilliseconds;
                if (ms > configData.Global.warningTime || configData.Debug.outputMundane) Puts($"NoDecay.OnEntityTakeDamage on {entity_name} took {ms} ms to execute.");
            }
        }

        private void OnEntitySpawned(RidableHorse horse)
        {
            // Workaround for no decay on horses, even if set to decay here
            if (horse == null) return;
            if (horse.net == null) return;

            if (configData.multipliers["horse"] > 0)
            {
                float newdecaytime = (180f / configData.multipliers["horse"]) - 180f;
                if (newdecaytime > 0)
                {
                    OutputRcon($"Adding {Math.Floor(newdecaytime).ToString()} minutes of decay time to horse {horse.net.ID.ToString()}, now {Math.Floor(180f + newdecaytime).ToString()} minutes", true);
                    horse.AddDecayDelay(newdecaytime);
                }
                else
                {
                    OutputRcon($"Subtracting {Math.Abs(Math.Floor(newdecaytime)).ToString()} minutes of decay time from horse {horse.net.ID.ToString()}, now {Math.Floor(180f + newdecaytime).ToString()} minutes", true);
                    horse.AddDecayDelay(newdecaytime);
                }
                horse.SetDecayActive(true);
            }
        }

        // Workaround for car chassis that won't die
        private void OnEntityDeath(ModularCar car, HitInfo hitinfo)
        {
            OutputRcon("Car died!  Checking for associated parts...");
            List<BaseEntity> ents = new List<BaseEntity>();
            Vis.Entities(car.transform.position, 1f, ents);
            foreach (BaseEntity ent in ents)
            {
                if (ent.name.Contains("module_car_spawned") && !ent.IsDestroyed)
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
            entityinfo["water"] = new List<string>();
            entityinfo["stonewall"] = new List<string>();
            entityinfo["woodwall"] = new List<string>();
            entityinfo["mining"] = new List<string>();

            List<string> names = new List<string>();
            foreach (BaseCombatEntity ent in Resources.FindObjectsOfTypeAll<BaseCombatEntity>())
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
                         entity_name.Contains("wall.window.bars") || entity_name.Contains("graveyard") ||
                         entity_name.Contains("candle") || entity_name.Contains("hatchet") ||
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
                else if (entity_name.Contains("water_catcher") || entity_name.Equals("waterbarrel"))
                {
                    entityinfo["water"].Add(entity_name);
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
                else if (entity_name.Contains("rowboat") || entity_name.Contains("rhib") || entity_name.Contains("kayak") || entity_name.Contains("submarine"))
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
            BuildingBlock block = entity as BuildingBlock;
            float multiplier = 1.0f;
            float damageAmount = 1.0f;
            bool isHighWall = block.LookupPrefab().name.Contains("wall.external");
            bool isHighGate = block.LookupPrefab().name.Contains("gates.external");

            string type = null;
            bool hascup = true; // Assume true (has cupboard or we don't require one)

            OutputRcon($"NoDecay checking for block damage to {block.LookupPrefab().name}");

            // Verify that we should check for a cupboard and ensure that one exists.
            // If not, multiplier will be standard of 1.0f (hascup true).
            if (configData.Global.requireCupboard)
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
                    if (hascup) multiplier = configData.multipliers["twig"];
                    type = "twig";
                    break;
                case BuildingGrade.Enum.Wood:
                    if (isHighWall)
                    {
                        if (hascup) multiplier = configData.multipliers["highWoodWall"];
                        type = "high wood wall";
                    }
                    else if (isHighGate)
                    {
                        if (hascup) multiplier = configData.multipliers["highWoodWall"];
                        type = "high wood gate";
                    }
                    else
                    {
                        if (hascup) multiplier = configData.multipliers["wood"];
                        type = "wood";
                    }
                    break;
                case BuildingGrade.Enum.Stone:
                    if (isHighWall)
                    {
                        if (hascup) multiplier = configData.multipliers["highStoneWall"];
                        type = "high stone wall";
                    }
                    else if (isHighGate)
                    {
                        if (hascup) multiplier = configData.multipliers["highStoneWall"];
                        type = "high stone gate";
                    }
                    else
                    {
                        if (hascup) multiplier = configData.multipliers["stone"];
                        type = "stone";
                    }
                    break;
                case BuildingGrade.Enum.Metal:
                    if (hascup) multiplier = configData.multipliers["sheet"];
                    type = "sheet";
                    break;
                case BuildingGrade.Enum.TopTier:
                    if (hascup) multiplier = configData.multipliers["armored"];
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

            if (building != null)
            {
                // cupboard overlap.  Block safe from decay.
                if (building.GetDominatingBuildingPrivilege() == null)
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
        private bool CheckCupboardEntity(BaseEntity entity, bool mundane = false)
        {
            if (configData.Global.useCupboardRange)
            {
                // This is the old way using cupboard distance instead of BP.  It's less efficient but some may have made use of this range concept, so here it is.
                int targetLayer = LayerMask.GetMask("Construction", "Construction Trigger", "Trigger", "Deployed");
                List<BuildingPrivlidge> cups = new List<BuildingPrivlidge>();
                Vis.Entities(entity.transform.position, configData.Global.cupboardRange, cups, targetLayer);

                OutputRcon($"CheckCupboardEntity:   Checking for cupboard within {configData.Global.cupboardRange.ToString()}m of {entity.ShortPrefabName}.", mundane);

                if (cups.Count > 0)
                {
                    // cupboard overlap.  Entity safe from decay.
                    OutputRcon($"CheckCupboardEntity:     Found entity layer in range of cupboard!", mundane);
                    return true;
                }

                OutputRcon($"CheckCupboardEntity:     Unable to find entity layer in range of cupboard.", mundane);
                return false;
            }
            else
            {
                // New method of simply checking for the entity's building privilege.
                OutputRcon($"CheckCupboardEntity:   Checking for building privilege for {entity.ShortPrefabName}.", mundane);
                BuildingPrivlidge tc = entity.GetBuildingPrivilege();

                if (tc != null)
                {
                    // cupboard overlap.  Entity safe from decay.
                    OutputRcon($"CheckCupboardEntity:     Found entity layer in range of cupboard!", mundane);
                    return true;
                }

                OutputRcon($"CheckCupboardEntity:     Unable to find entity layer in range of cupboard.", mundane);
                return false;
            }
        }

        // Prevent players from adding building resources to cupboard if so configured
        private object CanMoveItem(Item item, PlayerInventory inventory, uint targetContainer, int targetSlot)
        {
            if (item == null) return null;
            if (targetContainer == 0) return null;
            if (targetSlot == 0) return null;
            ItemContainer container = inventory.FindContainer(targetContainer);

            if (!(configData.Global.blockCupboardResources || configData.Global.blockCupboardWood)) return null;
            if (!(configData.Global.blockCupboardStone || configData.Global.blockCupboardMetal || configData.Global.blockCupboardArmor)) return null;

            try
            {
                BaseEntity cup = container.entityOwner as BaseEntity;
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
        [Command("nodecay")]
        private void CmdInfo(IPlayer iplayer, string command, string[] args)
        {
            if (permission.UserHasPermission(iplayer.Id, permNoDecayAdmin) && args.Length > 0)
            {
                switch (args[0])
                {
                    case "enable":
                        enabled = !enabled;
                        Message(iplayer, "ndstatus", enabled.ToString());
                        SaveConfig();
                        return;
                    case "log":
                        configData.Debug.outputToRcon = !configData.Debug.outputToRcon;
                        Message(iplayer, "nddebug", configData.Debug.outputToRcon.ToString());
                        return;
                    case "update":
                        UpdateEnts();
                        return;
                    case "info":
                        string info = Lang("ndsettings");
                        info += "\n\tarmored: " + configData.multipliers["armored"]; ToString();
                        info += "\n\tballoon: " + configData.multipliers["balloon"]; ToString();
                        info += "\n\tbarricade: " + configData.multipliers["barricade"]; ToString();
                        info += "\n\tbbq: " + configData.multipliers["bbq"]; ToString();
                        info += "\n\tboat: " + configData.multipliers["boat"]; ToString();
                        info += "\n\tbox: " + configData.multipliers["box"]; ToString();
                        info += "\n\tcampfire" + configData.multipliers["campfire"]; ToString();
                        info += "\n\tdeployables: " + configData.multipliers["deployables"]; ToString();
                        info += "\n\tentityCupboard: " + configData.multipliers["entityCupboard"]; ToString();
                        info += "\n\tfurnace: " + configData.multipliers["furnace"]; ToString();
                        info += "\n\thighWoodWall: " + configData.multipliers["highWoodWall"]; ToString();
                        info += "\n\thighStoneWall: " + configData.multipliers["highStoneWall"]; ToString();
                        info += "\n\thorse: " + configData.multipliers["horse"]; ToString();
                        info += "\n\tminicopter: " + configData.multipliers["minicopter"]; ToString();
                        info += "\n\tsam: " + configData.multipliers["sam"]; ToString();
                        info += "\n\tscrapcopter: " + configData.multipliers["scrapcopter"]; ToString();
                        info += "\n\tsedan: " + configData.multipliers["sedan"]; ToString();
                        info += "\n\tsheet: " + configData.multipliers["sheet"]; ToString();
                        info += "\n\tstone: " + configData.multipliers["stone"]; ToString();
                        info += "\n\ttrap: " + configData.multipliers["trap"]; ToString();
                        info += "\n\ttwig: " + configData.multipliers["twig"]; ToString();
                        info += "\n\tvehicle: " + configData.multipliers["vehicle"]; ToString();
                        info += "\n\twatchtower: " + configData.multipliers["watchtower"]; ToString();
                        info += "\n\twater: " + configData.multipliers["water"]; ToString();
                        info += "\n\twood: " + configData.multipliers["wood"]; ToString();

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

                        Message(iplayer, info);
                        info = null;
                        return;
                }
            }
            if (iplayer.Id == "server_console") return;
            if (args.Length > 0)
            {
                bool save = false;
                ulong id = ulong.Parse(iplayer.Id);
                switch (args[0])
                {
                    case "off":
                        if (!disabled.Contains(id))
                        {
                            save = true;
                            disabled.Add(id);
                        }
                        Message(iplayer, "ndstatus", enabled.ToString());
                        if (permission.UserHasPermission(iplayer.Id, permNoDecayUse))
                        {
                            Message(iplayer, "perm");
                        }
                        else
                        {
                            Message(iplayer, "noperm");
                        }
                        Message(iplayer, "ndisoff");
                        break;
                    case "on":
                        if (disabled.Contains(id))
                        {
                            save = true;
                            disabled.Remove(id);
                        }
                        Message(iplayer, "ndstatus", enabled.ToString());
                        if (permission.UserHasPermission(iplayer.Id, permNoDecayUse))
                        {
                            Message(iplayer, "perm");
                        }
                        else
                        {
                            Message(iplayer, "noperm");
                        }
                        Message(iplayer, "ndison");
                        break;
                    case "?":
                        Message(iplayer, "ndstatus", enabled.ToString());
                        if (permission.UserHasPermission(iplayer.Id, permNoDecayUse))
                        {
                            Message(iplayer, "perm");
                        }
                        else
                        {
                            Message(iplayer, "noperm");
                        }
                        if (disabled.Contains(id))
                        {
                            Message(iplayer, "ndisoff");
                        }
                        else
                        {
                            Message(iplayer, "ndison");
                        }
                        break;
                }
                if (save) SaveData();
            }
        }
        #endregion

        #region inbound_hooks
        // Returns player status if playerid > 0
        // Returns global enabled status if playerid == 0
        private bool NoDecayGet(ulong playerid=0)
        {
            if (playerid > 0)
            {
                return !disabled.Contains(playerid);
            }

            return enabled;
        }

        // Sets player status if playerid > 0
        // Sets global status if playerid == 0
        private object NoDecaySet(ulong playerid=0, bool status=true)
        {
            if (playerid > 0)
            {
                if (status)
                {
                    if (disabled.Contains(playerid))
                    {
                        disabled.Remove(playerid);
                    }
                }
                else
                {
                    if (!disabled.Contains(playerid))
                    {
                        disabled.Add(playerid);
                    }
                }
                SaveData();
                return null;
            }
            else
            {
                enabled = status;
                SaveConfig();
            }

            return null;
        }
        #endregion

        #region helpers
        // From PlayerDatabase
        private long ToEpochTime(DateTime dateTime)
        {
            DateTime date = dateTime.ToUniversalTime();
            long ticks = date.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, 0).Ticks;
            return ticks / TimeSpan.TicksPerSecond;
        }

        private string[] GetEntityZones(BaseEntity entity)
        {
            if (entity.IsValid() && ZoneManager)
            {
                return (string[])ZoneManager?.Call("GetEntityZoneIDs", new object[] { entity });
            }
            return new string[0];
        }

        public string PositionToGrid(Vector3 position)
        {
            if (GridAPI != null)
            {
                string[] g = (string[]) GridAPI.CallHook("GetGrid", position);
                return string.Concat(g);
            }
            else
            {
                // From GrTeleport for display only
                Vector2 r = new Vector2((World.Size / 2) + position.x, (World.Size / 2) + position.z);
                float x = Mathf.Floor(r.x / 146.3f) % 26;
                float z = Mathf.Floor(World.Size / 146.3f) - Mathf.Floor(r.y / 146.3f);

                return $"{(char)('A' + x)}{z - 1}";
            }
        }

        [HookMethod("SendHelpText")]
        private void SendHelpText(BasePlayer player)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<color=#05eb59>").Append(Name).Append(' ').Append(Version).Append("</color> · Controls decay\n");
            sb.Append("  · ").Append("twig=").Append(configData.multipliers["twig"]).Append(" - campfire=").Append(configData.multipliers["campfire"]).AppendLine();
            sb.Append("  · ").Append("wood =").Append(configData.multipliers["wood"]).Append(" - stone =").Append(configData.multipliers["stone"]).Append(" - sheet =").Append(configData.multipliers["sheet"]).Append(" - armored =").Append(configData.multipliers["armored"]).Append('\n');

            if (configData.Global.requireCupboard)
            {
                if (configData.Global.cupboardCheckEntity)
                {
                    string range = configData.Global.cupboardRange.ToString();
                    sb.Append("  · ").Append("cupboard check =").Append(true).Append(" - entity range =").Append(range);
                }
                else
                {
                    sb.Append("  · ").Append("cupboard check =").Append(true).Append(" - entity check =").Append(false);
                }
            }
            else
            {
                sb.Append("  · ").Append("cupboard check =").Append(false);
            }
            player.ChatMessage(sb.ToString());
        }

        // Just here to cleanup the code a bit
        private void OutputRcon(string message, bool mundane = false)
        {
            if (configData.Debug.outputToRcon)
            {
                if (!mundane) Puts($"{message}");
                else if (mundane && configData.Debug.outputMundane) Puts($"{message}");
            }
        }
        #endregion

        #region config
        private class ConfigData
        {
            public Debug Debug;
            public Global Global;
            public SortedDictionary<string, float> multipliers;
            public Multipliers Multipliers = new Multipliers();
            public VersionNumber Version;
        }

        private class Debug
        {
            public bool outputToRcon;
            public bool outputMundane;
            public bool logPosition;
        }

        private class Global
        {
            public bool usePermission;
            public bool requireCupboard;
            public bool cupboardCheckEntity;
            public float protectedDays;
            public float cupboardRange;
            public bool useCupboardRange;
            public bool DestroyOnZero;
            public bool useJPipes;
            public bool blockCupboardResources;
            public bool blockCupboardWood;
            public bool blockCupboardStone;
            public bool blockCupboardMetal;
            public bool blockCupboardArmor;
            public bool disableWarning;
            public bool disableLootWarning;
            public bool protectVehicleOnLift;
            public float protectedDisplayTime;
            public double warningTime;
        }

        private class Multipliers
        {
            // Legacy
            public float entityCupboardMultiplier;
            public float twigMultiplier;
            public float woodMultiplier;
            public float stoneMultiplier;
            public float sheetMultiplier;
            public float armoredMultiplier;
            public float baloonMultiplier;
            public float barricadeMultiplier;
            public float bbqMultiplier;
            public float boatMultiplier;
            public float boxMultiplier;
            public float campfireMultiplier;
            public float deployablesMultiplier;
            public float furnaceMultiplier;
            public float highWoodWallMultiplier;
            public float highStoneWallMultiplier;
            public float horseMultiplier;
            public float minicopterMultiplier;
            public float samMultiplier;
            public float scrapcopterMultiplier;
            public float sedanMultiplier;
            public float trapMultiplier;
            public float vehicleMultiplier;
            public float watchtowerMultiplier;
            public float waterMultiplier;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file");
            configData = new ConfigData
            {
                Debug = new Debug(),
                Global = new Global()
                {
                    protectedDays = 0,
                    cupboardRange = 30f,
                    DestroyOnZero = true,
                    disableWarning = true,
                    protectVehicleOnLift = true,
                    protectedDisplayTime = 4400,
                    warningTime = 10
                },
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
                    { "water", 0f },
                    { "wood", 0f },
                    { "deployables", 0.1f } // For all others not listed
                },
                Version = Version
            };
            SaveConfig(configData);
        }

        private void LoadConfigValues()
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
                    { "water", configData.Multipliers.waterMultiplier },
                    { "deployables", configData.Multipliers.deployablesMultiplier } // For all others not listed
                };
                configData.Multipliers = null;
            }

            if (configData.Version < new VersionNumber(1, 0, 73))
            {
                configData.multipliers.Add("water", 0f);
            }
            configData.Version = Version;

            SaveConfig(configData);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

        #region UI 
        public static class UI
        {
            public static CuiElementContainer Container(string panel, string color, string min, string max, bool useCursor = false, string parent = "Overlay")
            {
                return new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = color },
                            RectTransform = {AnchorMin = min, AnchorMax = max},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panel
                    }
                };
            }

            public static void Panel(ref CuiElementContainer container, string panel, string color, string min, string max, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = min, AnchorMax = max },
                    CursorEnabled = cursor
                },
                panel);
            }

            public static void Label(ref CuiElementContainer container, string panel, string color, string text, int size, string min, string max, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = min, AnchorMax = max }
                },
                panel);
            }

            public static void Button(ref CuiElementContainer container, string panel, string color, string text, int size, string min, string max, string command, TextAnchor align = TextAnchor.MiddleCenter, string tcolor="FFFFFF")
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 0f },
                    RectTransform = { AnchorMin = min, AnchorMax = max },
                    Text = { Text = text, FontSize = size, Align = align, Color = Color(tcolor, 1f) }
                },
                panel);
            }

            public static void Input(ref CuiElementContainer container, string panel, string color, string text, int size, string command, string min, string max)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Align = TextAnchor.MiddleLeft,
                            CharsLimit = 30,
                            Color = color,
                            Command = command + text,
                            FontSize = size,
                            IsPassword = false,
                            Text = text
                        },
                        new CuiRectTransformComponent { AnchorMin = min, AnchorMax = max },
                        new CuiNeedsCursorComponent()
                    }
                });
            }

            public static void Icon(ref CuiElementContainer container, string panel, string color, string imageurl, string min, string max)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Url = imageurl,
                            Sprite = "assets/content/textures/generic/fulltransparent.tga",
                            Color = color
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = min,
                            AnchorMax = max
                        }
                    }
                });
            }

            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                {
                    hexColor = hexColor.Substring(1);
                }
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
        #endregion
    }
}
