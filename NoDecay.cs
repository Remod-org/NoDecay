#region License (GPL v2)
/*
    NoDecay - Scales or disables decay of items for Rust by Facepunch
    Copyright (c) 2020 RFC1920 <desolationoutpostpve@gmail.com>

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License v2.0.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/
#endregion License (GPL v2)
using HarmonyLib;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoDecay", "RFC1920", "1.0.95", ResourceId = 1160)]
    //Original Credit to Deicide666ra/Piarb and Diesel_42o
    //Thanks to Deicide666ra for allowing me to continue his work on this plugin
    [Description("Scales or disables decay of items")]
    internal class NoDecay : RustPlugin
    {
        private ConfigData configData;
        private bool enabled = true;

        #region main
        private Dictionary<string, long> lastConnected = new();
        private List<ulong> disabled = new();
        private Dictionary<string, List<string>> entityinfo = new();

        private int targetLayer = LayerMask.GetMask("Construction", "Construction Trigger", "Trigger", "Deployed");
        private const string permNoDecayUse = "nodecay.use";
        private const string permNoDecayAdmin = "nodecay.admin";
        private const string TCOVR = "nodecay.overlay";

        [PluginReference]
        private readonly Plugin ZoneManager, GridAPI;

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

        [AutoPatch]
        [HarmonyPatch(typeof(BuildingPrivlidge), "PurchaseUpkeepTime", new Type[] { typeof(DecayEntity), typeof(float) })]
        public static class UpkeepPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref float __result, ref DecayEntity entity, ref float deltaTime)
            {
                if ((bool)Interface.Call("NoDecayGet", entity?.OwnerID, true))
                {
                    __result = 0;
                    return false;
                }
                return true;
            }
        }

        [AutoPatch]
        [HarmonyPatch(typeof(BuildingBlock), "DamageWallpaper", new Type[] { typeof(float), typeof(int) })]
        public static class PaperPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(BuildingBlock __instance, float totalDamage, int side)
            {
                if (side == 0)
                {
                    Interface.Call("NoDecayLogHook", $"Disabling wallpaper decay for {__instance.net.ID.Value}:{__instance?.OwnerID}");
                    return false;
                }
                return true;
            }
        }

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

                foreach (RidableHorse2 horse in Resources.FindObjectsOfTypeAll<RidableHorse2>())
                {
                    if (horse.net == null) continue;
                    if (horse.IsHitched() || horse.IsDestroyed) continue;
                    FieldInfo nextDecayTime = typeof(RidableHorse2).GetField("nextDecayTime", (BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
                    float nextDecayTimeValue = (float)nextDecayTime?.GetValue(horse);

                    if (newdecaytime > 0)
                    {
                        DoLog($"Adding {Math.Floor(newdecaytime)} minutes of decay time to horse {horse.net.ID}, now {Math.Floor(180f + newdecaytime)} minutes", true);
                        if (nextDecayTimeValue < Time.time)
                        {
                            nextDecayTime.SetValue(horse, Time.time + 5f);
                        }
                        nextDecayTime.SetValue(horse, nextDecayTimeValue + newdecaytime);
                    }
                    else
                    {
                        DoLog($"Subtracting {Math.Abs(Math.Floor(newdecaytime))} minutes of decay time from horse {horse.net.ID}, now {Math.Floor(180f + newdecaytime)} minutes", true);
                        nextDecayTime.SetValue(horse, nextDecayTimeValue + newdecaytime);
                    }
                    //horse.SetDecayActive(true);
                }
            }
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (!player.userID.IsSteamId()) return null;
            if (!configData.Global.disableLootWarning) return null;
            if (!permission.UserHasPermission(player.UserIDString, permNoDecayUse) && configData.Global.usePermission) return null;
            if (container == null) return null;
            BuildingPrivlidge privs = container.GetComponentInParent<BuildingPrivlidge>();
            if (privs != null && configData.Global.EnableGui) TcOverlay(player);
            return null;
        }

        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if (!player.userID.IsSteamId()) return;
            CuiHelper.DestroyUi(player, TCOVR);
        }

        private void TcOverlay(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, TCOVR);

            CuiElementContainer container = UI.Container(TCOVR, UI.Color("3E3C37", 1f), "0.651 0.604", "0.946 0.6365", true, "Overlay");
            UI.Label(ref container, TCOVR, UI.Color("#cacaca", 1f), Lang("protby"), 14, "0 0", "1 1");

            CuiHelper.AddUi(player, container);
        }

        private void Loaded() => LoadConfigVariables();

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
                            DoLog($"TC owner {owner} has NoDecay permission!");
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                if (disabled.Contains(buildingPrivilege.OwnerID))
                {
                    DoLog($"TC owner {buildingPrivilege.OwnerID} has disabled NoDecay.");
                    return;
                }

                saveInfo.msg.buildingPrivilege.protectedMinutes = configData.Global.protectedDisplayTime;
                saveInfo.msg.buildingPrivilege.upkeepPeriodMinutes = configData.Global.protectedDisplayTime;
            }
        }

        private void OnUserConnected(IPlayer player) => OnUserDisconnected(player);

        private void OnUserDisconnected(IPlayer player)
        {
            lastConnected[player.Id] = ToEpochTime(DateTime.UtcNow);
            SaveData();
        }

        private void LoadData()
        {
            entityinfo = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<string, List<string>>>(Name + "/entityinfo");
            lastConnected = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<string, long>>(Name + "/lastconnected");
            disabled = Interface.GetMod().DataFileSystem.ReadObject<List<ulong>>(Name + "/disabled");
        }

        private void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject(Name + "/entityinfo", entityinfo);
            Interface.GetMod().DataFileSystem.WriteObject(Name + "/lastconnected", lastConnected);
            Interface.GetMod().DataFileSystem.WriteObject(Name + "/disabled", disabled);
        }

        // Heal damage to entities over time since there may be no other way with decay disabled.
        private object OnDecayDamage(DecayEntity entity)
        {
            if (!enabled) return null;
            if (!(configData.Global.healBuildings || configData.Global.healEntities)) return null;
            //float single = Time.time - entity.lastDecayTick;

            if (entity == null) return null;
            if (entity?.OwnerID == 0) return null;
            if (entity.health == entity.MaxHealth()) return null;
            if (!CheckPerm(entity, entity?.OwnerID.ToString())) return null;

            float entityHealAmount = entity.MaxHealth() * configData.Global.healPercentage;

            if (entity is BuildingBlock && configData.Global.healBuildings)
            {
                DoLog($"Healing damage to {entity?.ShortPrefabName}/{(entity as BuildingBlock)?.grade} owned by {entity?.OwnerID} (amount={entityHealAmount}). Current health: {entity.health}");
                entity.Heal(entityHealAmount);
                return true;
            }
            else if (configData.Global.healEntities)
            {
                DoLog($"Healing damage to {entity?.ShortPrefabName} owned by {entity?.OwnerID} (amount={entityHealAmount}). Current health {entity.health}");
                entity.Heal(entityHealAmount);
                return true;
            }
            return null;
        }

        //private object OnDecayHeal(DecayEntity entity)

        private bool CheckPerm(BaseCombatEntity entity, string owner)
        {
            string entity_name = entity.LookupPrefab().name.ToLower();
            ulong bpOwner = 0;

            if (configData.Global.usePermission)
            {
                if (permission.UserHasPermission(owner, permNoDecayUse) || owner == "0")
                {
                    if (owner != "0")
                    {
                        DoLog($"{entity_name} owner {owner} has NoDecay permission!");
                    }
                }
                else if (configData.Global.useTCOwnerToCheckPermission)
                {
                    BuildingPrivlidge bp = entity?.GetBuildingPrivilege();
                    if (bp?.OwnerID != 0 && permission.UserHasPermission(bp?.OwnerID.ToString(), permNoDecayUse))
                    {
                        bpOwner = bp.OwnerID;
                        DoLog($"{entity_name} TC owner {bp?.OwnerID} has NoDecay permission!");
                    }
                }
                else
                {
                    DoLog($"{entity_name} owner {owner} does NOT have NoDecay permission.  Standard decay in effect.");
                    return false;
                }

                if (disabled.Contains(entity.OwnerID))
                {
                    DoLog($"Entity owner {entity.OwnerID} has disabled NoDecay.");
                    return false;
                }
                else if (bpOwner > 0 && disabled.Contains(bpOwner))
                {
                    DoLog($"Entity TC owner {bpOwner} has disabled NoDecay.");
                    return false;
                }
            }
            if (configData.Global.protectedDays > 0 && entity.OwnerID > 0)
            {
                long lc;
                lastConnected.TryGetValue(entity.OwnerID.ToString(), out lc);
                if (lc > 0)
                {
                    long now = ToEpochTime(DateTime.UtcNow);
                    float days = Math.Abs((now - lc) / 86400);
                    bool friend = false;
                    if (configData.Global.useBPAuthListForProtectedDays)
                    {
                        BuildingPrivlidge bp = entity?.GetBuildingPrivilege();
                        if (bp != null)
                        {
                            foreach (ProtoBuf.PlayerNameID p in bp.authorizedPlayers)
                            {
                                if (p?.userid == entity.OwnerID) continue;
                                long lastcon;
                                lastConnected.TryGetValue(entity.OwnerID.ToString(), out lastcon);
                                days = Math.Abs((now - lastcon) / 86400);
                                if (days <= configData.Global.protectedDays)
                                {
                                    friend = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (days > configData.Global.protectedDays)
                    {
                        DoLog($"Allowing decay for owner offline for {configData.Global.protectedDays} days");
                        return false;
                    }
                    else if (friend)
                    {
                        DoLog($"Friend authorized on local TC was last connected {days} days ago and is still protected...");
                    }
                    else
                    {
                        DoLog($"Owner was last connected {days} days ago and is still protected...");
                    }
                }
            }
            return true;
        }

        //private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        private object OnEntityTakeDamage(DecayEntity entity, HitInfo hitInfo)
        {
            if (!enabled) return null;
            if (entity == null || hitInfo == null) return null;
            if (hitInfo.damageTypes.GetMajorityDamageType() != Rust.DamageType.Decay) return null;

            float damageAmount = 0f;
            DateTime tick = DateTime.Now;
            string entity_name = entity.LookupPrefab().name.ToLower();
            //Puts($"Decay Entity: {entity_name}");
            string owner = entity.OwnerID.ToString();
            bool mundane = false;
            bool isBlock = false;

            if (!CheckPerm(entity, owner)) return null;

            try
            {
                float before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);

                if (entity is BuildingBlock)
                {
                    damageAmount = ProcessBuildingDamage(entity, before);
                    isBlock = true;
                }
                else if (entity.GetComponent<ModularCar>() != null)
                {
                    ModularCarGarage garage = entity.GetComponentInParent<ModularCarGarage>();
                    if (garage != null && configData.Global.protectVehicleOnLift)
                    {
                        return true;
                    }
                }
                else
                {
                    // Main check for non-building entities/deployables
                    KeyValuePair<string, List<string>> entity_type = entityinfo.FirstOrDefault(x => x.Value.Contains(entity_name));
                    if (!entity_type.Equals(default(KeyValuePair<string, List<string>>)))
                    {
                        if (entity_type.Key.Equals("vehicle") || entity_type.Key.Equals("boat") || entity_type.Key.Equals("balloon") || entity_type.Key.Equals("horse"))
                        {
                            mundane = true;
                        }
                        DoLog($"Found {entity_name} listed in {entity_type.Key}", mundane);
                        if (configData.multipliers.ContainsKey(entity_type.Key))
                        {
                            damageAmount = before * configData.multipliers[entity_type.Key];
                        }
                    }
                }

                // Check non-building entities for cupboard in range
                if (configData.Global.requireCupboard && configData.Global.cupboardCheckEntity && !isBlock)
                {
                    // Verify that we should check for a cupboard and ensure that one exists.
                    // If so, multiplier will be set to entityCupboardMultiplier.
                    DoLog("NoDecay checking for local cupboard.", mundane);

                    if (CheckCupboardEntity(entity, mundane))
                    {
                        damageAmount = before * configData.multipliers["entityCupboard"];
                    }
                }

                string pos = "";
                string zones = "";
                bool inzone = false;

                string[] zonedata = GetEntityZones(entity);
                if (zonedata.Length > 0)
                {
                    inzone = true;
                    bool skipCat = false;
                    string cat = entityinfo.FirstOrDefault(y => y.Value.Contains(entity_name)).Key;
                    if (configData.Global.overrideZoneManager.Count > 0 && configData.Global.overrideZoneManager.Contains(cat))
                    {
                        DoLog($"Skipping zone check for {entity_name} based on category {cat} exclusion.");
                        skipCat = true;
                    }

                    if (ZoneManager && configData.Global.honorZoneManagerFlag && !skipCat)
                    {
                        foreach (string zoneId in zonedata)
                        {
                            if ((bool)ZoneManager?.Call("HasFlag", zoneId, "nodecay"))
                            {
                                DoLog($"{entity_name} in zone {zoneId}, which has the nodecay flag.  Setting damage to 0.");
                                damageAmount = 0f;
                            }
                        }
                    }
                }

                if (configData.Debug.logPosition)
                {
                    pos = $" at {PositionToGrid(entity.transform.position)} {entity.transform.position}";
                    zones = string.Join(",", zonedata);
                }

                NextTick(() =>
                {
                    DoLog($"Decay [{entity_name}{pos} - {entity.net.ID}] before: {before} after: {damageAmount}, item health {entity.health}", mundane);
                    if (inzone)
                    {
                        DoLog($"Decay [{entity_name}] in ZoneManager zone(s): {zones}", mundane);
                    }

                    entity.health -= damageAmount;
                    if (entity.health == 0 && configData.Global.DestroyOnZero)
                    {
                        DoLog($"Entity {entity_name}{pos} completely decayed - destroying!", mundane);
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

        //private void OnEntitySpawned(RidableHorse2 horse)
        //{
        //    // Workaround for no decay on horses, even if set to decay here
        //    if (horse == null) return;
        //    if (horse.net == null) return;

        //    if (configData.multipliers["horse"] > 0)
        //    {
        //        float newdecaytime = (180f / configData.multipliers["horse"]) - 180f;
        //        FieldInfo nextDecayTime = typeof(RidableHorse2).GetField("nextDecayTime", (BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
        //        float nextDecayTimeValue = (float)nextDecayTime?.GetValue(horse);

        //        if (newdecaytime > 0)
        //        {
        //            DoLog($"Adding {Math.Floor(newdecaytime)} minutes of decay time to horse {horse.net.ID}, now {Math.Floor(180f + newdecaytime)} minutes", true);
        //            if (nextDecayTimeValue < Time.time)
        //            {
        //                nextDecayTime.SetValue(horse, Time.time + 5f);
        //            }
        //            nextDecayTime.SetValue(horse, nextDecayTimeValue + newdecaytime);
        //        }
        //        else
        //        {
        //            DoLog($"Subtracting {Math.Abs(Math.Floor(newdecaytime))} minutes of decay time from horse {horse.net.ID}, now {Math.Floor(180f + newdecaytime)} minutes", true);
        //            nextDecayTime.SetValue(horse, nextDecayTimeValue + newdecaytime);
        //        }
        //        //horse.SetDecayActive(true);
        //    }
        //}

        // Workaround for car chassis that won't die
        private void OnEntityDeath(ModularCar car, HitInfo hitinfo)
        {
            DoLog("Car died!  Checking for associated parts...");
            List<BaseEntity> ents = new();
            Vis.Entities(car.transform.position, 1f, ents);
            foreach (BaseEntity ent in ents)
            {
                if (ent == null) continue;
                if (ent.name.Contains("module_car_spawned") && !ent.IsDestroyed)
                {
                    DoLog($"Killing {ent.ShortPrefabName}");
                    ent?.Kill(BaseNetworkable.DestroyMode.Gib);
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
            entityinfo["building"] = new List<string>();
            entityinfo["campfire"] = new List<string>();
            entityinfo["deployables"] = new List<string>();
            entityinfo["furnace"] = new List<string>();
            entityinfo["horse"] = new List<string>();
            entityinfo["minicopter"] = new List<string>();
            entityinfo["attackcopter"] = new List<string>();
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

            List<string> names = new();
            foreach (BaseCombatEntity ent in Resources.FindObjectsOfTypeAll<BaseCombatEntity>())
            //foreach (BaseCombatEntity ent in UnityEngine.Object.FindObjectsOfType<BaseCombatEntity>())
            {
                string entity_name = ent.ShortPrefabName?.ToLower();
                DoLog($"Checking {entity_name}");
                if (entity_name == "cupboard.tool.deployed") continue;
                if (entity_name == null) continue;
                if (names.Contains(entity_name)) continue; // Saves 20-30 seconds of processing time.
                names.Add(entity_name);

                if (entity_name.Contains("campfire") || entity_name.Contains("skull_fire_pit"))
                {
                    entityinfo["campfire"].Add(entity_name);
                }
                else if (entity_name.Contains("box") || entity_name.Contains("coffin"))
                {
                    entityinfo["box"].Add(entity_name);
                }
                else if (entity_name.Contains("shutter") ||
                         (entity_name.Contains("door") && !entity_name.Contains("doorway")) ||
                         entity_name.Contains("hatch") || entity_name.Contains("garagedoor") ||
                         entity_name.Contains("bars") || entity_name.Contains("netting") ||
                         entity_name.Contains("cell") || entity_name.Contains("fence") ||
                         entity_name.Contains("reinforced") || entity_name.Contains("composter") ||
                         entity_name.Contains("workbench") || entity_name.Contains("shopfront") ||
                         entity_name.Contains("grill") || entity_name.Contains("wall.window.bars"))
                {
                    entityinfo["building"].Add(entity_name);
                }
                else if (entity_name.Contains("deployed") || entity_name.Contains("speaker") ||
                         entity_name.Contains("strobe") || entity_name.Contains("fog") ||
                         entity_name.Contains("graveyard") || entity_name.Contains("candle") ||
                         entity_name.Contains("hatchet") || entity_name.Contains("jackolantern"))
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
                else if (entity_name.Contains("attackhelicopter"))
                {
                    entityinfo["attackcopter"].Add(entity_name);
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
                        entity_name.Contains("snowmobile") ||
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
            bool isHighWall = block.LookupPrefab().name.Contains("wall.external");
            bool isHighGate = block.LookupPrefab().name.Contains("gates.external");

            string type = null;
            bool hascup = true; // Assume true (has cupboard or we don't require one)

            DoLog($"NoDecay checking for block damage to {block.LookupPrefab().name}");

            // Verify that we should check for a cupboard and ensure that one exists.
            // If not, multiplier will be standard of 1.0f (hascup true).
            if (configData.Global.requireCupboard)
            {
                DoLog("NoDecay checking for local cupboard.");
                hascup = CheckCupboardBlock(block, entity.LookupPrefab().name, block.grade.ToString().ToLower());
            }
            else
            {
                DoLog("NoDecay not checking for local cupboard.");
            }

            switch (block.grade)
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
                    DoLog($"Decay ({type}) has unknown grade type.");
                    type = "unknown";
                    break;
            }

            float damageAmount = before * multiplier;

            DoLog($"Decay ({type}) before: {before} after: {damageAmount}");
            return damageAmount;
        }

        public BuildingPrivlidge GetBuildingPrivilege(BuildingManager.Building building, BuildingBlock block = null)
        {
            BuildingPrivlidge buildingPrivlidge = null;
            if (building.HasBuildingPrivileges())
            {
                for (int i = 0; i < building.buildingPrivileges.Count; i++)
                {
                    BuildingPrivlidge item = building.buildingPrivileges[i];
                    if (!(item == null) && item.IsOlderThan(buildingPrivlidge))
                    {
                        DoLog("CheckCupboardBlock:     Found block connected to cupboard!");
                        buildingPrivlidge = item;
                    }
                }
            }
            else if (configData.Global.cupboardRange > 0)
            {
                // Disconnected building with no TC, but possibly in cupboard range
                List<BuildingPrivlidge> cups = new();
                Vis.Entities(block.transform.position, configData.Global.cupboardRange, cups, targetLayer);
                foreach (BuildingPrivlidge cup in cups)
                {
                    foreach (ProtoBuf.PlayerNameID p in cup.authorizedPlayers.ToArray())
                    {
                        if (p.userid == block.OwnerID)
                        {
                            DoLog("CheckCupboardBlock:     Found block in range of cupboard!");
                            return cup;
                        }
                    }
                }
            }
            return buildingPrivlidge;
        }

        // Check that a building block is owned by/attached to a cupboard
        private bool CheckCupboardBlock(BuildingBlock block, string ename = "unknown", string grade = "")
        {
            BuildingManager.Building building = block.GetBuilding();
            DoLog($"CheckCupboardBlock:   Checking for cupboard connected to {grade} {ename}.");

            if (building != null)
            {
                // cupboard overlap.  Block safe from decay.
                //if (building.GetDominatingBuildingPrivilege() == null)
                if (GetBuildingPrivilege(building, block) == null)
                {
                    DoLog("CheckCupboardBlock:     Block NOT owned by cupboard!");
                    return false;
                }

                DoLog("CheckCupboardBlock:     Block owned by cupboard!");
                return true;
            }
            else
            {
                DoLog("CheckCupboardBlock:     Unable to find cupboard.");
                return false;
            }
        }

        // Non-block entity check
        private bool CheckCupboardEntity(BaseEntity entity, bool mundane = false)
        {
            if (configData.Global.useCupboardRange)
            {
                // This is the old way using cupboard distance instead of BP.  It's less efficient but some may have made use of this range concept, so here it is.
                List<BuildingPrivlidge> cups = new();
                Vis.Entities(entity.transform.position, configData.Global.cupboardRange, cups, targetLayer);

                DoLog($"CheckCupboardEntity:   Checking for cupboard within {configData.Global.cupboardRange}m of {entity.ShortPrefabName}.", mundane);

                if (cups.Count > 0)
                {
                    // cupboard overlap.  Entity safe from decay.
                    DoLog("CheckCupboardEntity:     Found entity layer in range of cupboard!", mundane);
                    return true;
                }

                DoLog("CheckCupboardEntity:     Unable to find entity layer in range of cupboard.", mundane);
                return false;
            }

            // New method of simply checking for the entity's building privilege.
            DoLog($"CheckCupboardEntity:   Checking for building privilege for {entity.ShortPrefabName}.", mundane);
            BuildingPrivlidge tc = entity.GetBuildingPrivilege();

            if (tc != null)
            {
                // cupboard overlap.  Entity safe from decay.
                DoLog("CheckCupboardEntity:     Found entity layer in range of cupboard!", mundane);
                return true;
            }

            DoLog("CheckCupboardEntity:     Unable to find entity layer in range of cupboard.", mundane);
            return false;
        }

        // Prevent players from adding building resources to cupboard if so configured
        //private object CanMoveItem(Item item, PlayerInventory inventory, uint targetContainer, int targetSlot)
        private ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            BaseEntity cup = container?.entityOwner;
            if (cup == null) return null;
            if (cup is not BuildingPrivlidge) return null;

            if (!(configData.Global.blockCupboardResources || configData.Global.blockCupboardWood)) return null;

            string res = item?.info?.shortname;
            DoLog($"Player trying to add {res} to a cupboard!");
            if (res.Equals("wood") && configData.Global.blockCupboardWood)
            {
                DoLog($"Player blocked from adding {res} to a cupboard!");
                return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
            }
            else if (configData.Global.blockCupboardResources)
            {
                if (
                    (res.Equals("stones") && configData.Global.blockCupboardStone)
                    || (res.Equals("metal.fragments") && configData.Global.blockCupboardMetal)
                    || (res.Equals("metal.refined") && configData.Global.blockCupboardArmor))
                {
                    DoLog($"Player blocked from adding {res} to a cupboard!");
                    return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
                }
            }

            return null;
        }
        #endregion

        #region command
        [Command("nodecay")]
        private void CmdInfo(IPlayer iplayer, string command, string[] args)
        {
            if ((permission.UserHasPermission(iplayer.Id, permNoDecayAdmin) && args.Length > 0) || iplayer.IsServer)
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
                        info += "\n\tarmored: " + configData.multipliers["armored"].ToString();
                        info += "\n\tballoon: " + configData.multipliers["balloon"].ToString();
                        info += "\n\tbarricade: " + configData.multipliers["barricade"].ToString();
                        info += "\n\tbbq: " + configData.multipliers["bbq"].ToString();
                        info += "\n\tboat: " + configData.multipliers["boat"].ToString();
                        info += "\n\tbox: " + configData.multipliers["box"].ToString();
                        info += "\n\tbuilding: " + configData.multipliers["building"].ToString();
                        info += "\n\tcampfire" + configData.multipliers["campfire"].ToString();
                        info += "\n\tdeployables: " + configData.multipliers["deployables"].ToString();
                        info += "\n\tentityCupboard: " + configData.multipliers["entityCupboard"].ToString();
                        info += "\n\tfurnace: " + configData.multipliers["furnace"].ToString();
                        info += "\n\thighWoodWall: " + configData.multipliers["highWoodWall"].ToString();
                        info += "\n\thighStoneWall: " + configData.multipliers["highStoneWall"].ToString();
                        info += "\n\thorse: " + configData.multipliers["horse"].ToString();
                        info += "\n\tminicopter: " + configData.multipliers["minicopter"].ToString();
                        info += "\n\tattackcopter: " + configData.multipliers["attackcopter"].ToString();
                        info += "\n\tscrapcopter: " + configData.multipliers["scrapcopter"].ToString();
                        info += "\n\tsam: " + configData.multipliers["sam"].ToString();
                        info += "\n\tsedan: " + configData.multipliers["sedan"].ToString();
                        info += "\n\tsheet: " + configData.multipliers["sheet"].ToString();
                        info += "\n\tstone: " + configData.multipliers["stone"].ToString();
                        info += "\n\ttrap: " + configData.multipliers["trap"].ToString();
                        info += "\n\ttwig: " + configData.multipliers["twig"].ToString();
                        info += "\n\tvehicle: " + configData.multipliers["vehicle"].ToString();
                        info += "\n\twatchtower: " + configData.multipliers["watchtower"].ToString();
                        info += "\n\twater: " + configData.multipliers["water"].ToString();
                        info += "\n\twood: " + configData.multipliers["wood"].ToString();

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
        // Can also check EnableUpkeep value for use with allowing/blocking upkeep cost
        private bool NoDecayGet(ulong playerid = 0, bool checkupkeep = false)
        {
            DoLog($"NoDecayGet called with playerid={playerid}, checkupkeep={checkupkeep}");
            if (playerid > 0)
            {
                switch (checkupkeep)
                {
                    case true:
                        DoLog($"Returning player {playerid} enabled status of {!disabled.Contains(playerid)} to caller.  EnableUpkeep is {configData.Global.EnableUpkeep}.");
                        return enabled && !disabled.Contains(playerid) && !configData.Global.EnableUpkeep;
                    case false:
                        DoLog($"Returning player {playerid} enabled status of {!disabled.Contains(playerid)} to caller.");
                        return enabled && !disabled.Contains(playerid);
                }
            }

            switch (checkupkeep)
            {
                case true:
                    DoLog($"Returning global enabled status of {enabled} to caller.  EnableUpkeep is {configData.Global.EnableUpkeep}.");
                    return enabled && !configData.Global.EnableUpkeep;
                case false:
                default:
                    DoLog($"Returning global enabled status of {enabled} to caller.");
                    return enabled;
            }
        }

        // Sets player status if playerid > 0
        // Sets global status if playerid == 0
        private object NoDecaySet(ulong playerid = 0, bool status = true)
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

        private void DisableMe()
        {
            if (!configData.Global.respondToActivationHooks) return;
            enabled = false;
            Puts($"{Name} disabled");
        }

        private void EnableMe()
        {
            if (!configData.Global.respondToActivationHooks) return;
            enabled = true;
            Puts($"{Name} enabled");
        }
        #endregion

        #region helpers
        // From PlayerDatabase
        private static long ToEpochTime(DateTime dateTime)
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
                string[] g = (string[])GridAPI.CallHook("GetGrid", position);
                return string.Concat(g);
            }
            else
            {
                // From GrTeleport for display only
                Vector2 r = new((World.Size / 2) + position.x, (World.Size / 2) + position.z);
                float x = Mathf.Floor(r.x / 146.3f) % 26;
                float z = Mathf.Floor(World.Size / 146.3f) - Mathf.Floor(r.y / 146.3f);

                return $"{(char)('A' + x)}{z - 1}";
            }
        }

        [HookMethod("SendHelpText")]
        private void SendHelpText(BasePlayer player)
        {
            StringBuilder sb = new();
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

        private void NoDecayLogHook(string message)
        {
            DoLog(message);
        }

        public void DoLog(string message, bool mundane = false)
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
            public bool useTCOwnerToCheckPermission;
            public bool useBPAuthListForProtectedDays;
            public bool EnableGui;
            public bool EnableUpkeep;
            public bool requireCupboard;
            public bool cupboardCheckEntity;
            public float protectedDays;
            public float cupboardRange;
            public bool useCupboardRange;
            public bool DestroyOnZero;
            public bool honorZoneManagerFlag;
            public bool blockCupboardResources;
            public bool blockCupboardWood;
            public bool blockCupboardStone;
            public bool blockCupboardMetal;
            public bool blockCupboardArmor;
            public bool healEntities;
            public bool healBuildings;
            public float healPercentage;
            public bool disableWarning;
            public bool disableLootWarning;
            public bool protectVehicleOnLift;
            public float protectedDisplayTime;
            public double warningTime;
            public List<string> overrideZoneManager = new();
            public bool respondToActivationHooks;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file");
            configData = new ConfigData
            {
                Debug = new Debug(),
                Global = new Global()
                {
                    useBPAuthListForProtectedDays = false,
                    useTCOwnerToCheckPermission = false,
                    EnableGui = true,
                    protectedDays = 0,
                    cupboardRange = 30f,
                    DestroyOnZero = true,
                    disableWarning = true,
                    protectVehicleOnLift = true,
                    healEntities = false,
                    healBuildings = false,
                    healPercentage = 0.01f,
                    protectedDisplayTime = 44000,
                    warningTime = 10,
                    overrideZoneManager = new List<string>() { "vehicle", "balloon" },
                    respondToActivationHooks = false
                },
                multipliers = new SortedDictionary<string, float>()
                {
                    { "armored", 0f },
                    { "balloon", 0f },
                    { "barricade", 0f },
                    { "bbq", 0f },
                    { "boat", 0f },
                    { "building", 0f },
                    { "box", 0f },
                    { "campfire", 0f },
                    { "entityCupboard", 0f },
                    { "furnace", 0f },
                    { "highWoodWall", 0f },
                    { "highStoneWall", 0f },
                    { "horse", 0f },
                    { "minicopter", 0f },
                    { "attackcopter", 0f },
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

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < new VersionNumber(1, 0, 73))
            {
                configData.multipliers.Add("water", 0f);
            }
            if (configData.Version < new VersionNumber(1, 0, 76))
            {
                configData.Global.honorZoneManagerFlag = false;
            }
            if (configData.Version < new VersionNumber(1, 0, 77))
            {
                configData.Global.overrideZoneManager = new List<string>() { "vehicle", "balloon" };
            }
            if (configData.Version < new VersionNumber(1, 0, 81) && configData.Global.protectedDisplayTime == 4400)
            {
                configData.Global.protectedDisplayTime = 44000;
            }
            if (!configData.multipliers.ContainsKey("attackcopter"))
            {
                configData.multipliers.Add("attackcopter", 0);
            }
            if (configData.Version < new VersionNumber(1, 0, 87))
            {
                configData.multipliers.Add("building", 0);
                UpdateEnts();
            }

            if (configData.Global.healPercentage == 0 || configData.Global.healPercentage >= 1)
            {
                configData.Global.healPercentage = 0.01f;
            }

            if (configData.Version < new VersionNumber(1, 0, 94))
            {
                configData.Global.EnableGui = true;
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

            public static void Button(ref CuiElementContainer container, string panel, string color, string text, int size, string min, string max, string command, TextAnchor align = TextAnchor.MiddleCenter, string tcolor = "FFFFFF")
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
