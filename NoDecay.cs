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
using System.Linq;
using System.Reflection;
using System.Text;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoDecay", "RFC1920", "1.0.52", ResourceId = 1160)]
    //Original Credit to Deicide666ra/Piarb and Diesel_42o
    //Thanks to Deicide666ra for allowing me to continue his work on this plugin
    //Thanks to Steenamaroo for his help and support
    [Description("Scales or disables decay of items")]
    class NoDecay : RustPlugin
    {
        private ConfigData configData;
        private bool enabled = true;

        #region main
        public static readonly FieldInfo nextProtectedCalcTime = typeof(BuildingPrivlidge).GetField("nextProtectedCalcTime", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        public static readonly FieldInfo cachedProtectedMinutes = typeof(BuildingPrivlidge).GetField("cachedProtectedMinutes", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        [PluginReference]
        private readonly Plugin JPipes;//, MyMiniCopter;

        void Init()
        {
            permission.RegisterPermission("nodecay.use", this);
            permission.RegisterPermission("nodecay.admin", this);
        }

        void OnServerInitialized()
        {
            if (!configData.Global.disableWarning) return;
            //private readonly string ndc = "D2AA9A0376A43CCA72EA06868DCF820C";
            foreach (BuildingPrivlidge priv in BaseNetworkable.serverEntities.OfType<BuildingPrivlidge>())
            {
                if (priv == null) return;
                if ((permission.UserHasPermission(priv.OwnerID.ToString(), "nodecay.use") && configData.Global.usePermission) || !configData.Global.usePermission)
                {
                    nextProtectedCalcTime.SetValue(priv, Time.realtimeSinceStartup * 2f);
                    cachedProtectedMinutes.SetValue(priv, 1000000000);
                    priv.SendNetworkUpdateImmediate();
                }
            }
        }

        void Loaded() => LoadConfigValues();

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (!enabled) return null;
            if(entity == null || hitInfo == null) return null;
            if(!hitInfo.damageTypes.Has(Rust.DamageType.Decay)) return null;

            float damageAmount = 0f;
            DateTime tick = DateTime.Now;
            string entity_name = entity.LookupPrefab().name;
            string owner = entity.OwnerID.ToString();
            bool mundane = false;
            bool isBlock = false;

            if(configData.Global.usePermission)
            {
                if(permission.UserHasPermission(owner, "nodecay.use") || owner == "0")
                {
                    if(owner != "0")
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

            try
            {
                float before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);

                if(entity is BuildingBlock)
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
                else if(entity_name == "campfire" || entity_name == "skull_fire_pit")
                {
                    damageAmount = before * configData.Multipliers.campfireMultiplier;
                }
                else if(entity_name == "box.wooden.large" ||
                     entity_name == "woodbox_deployed" ||
                     entity_name == "CoffinStorage")
                {
                    damageAmount = before * configData.Multipliers.boxMultiplier;
                }
                else if(entity_name.Contains("deployed") ||
                     entity_name.Contains("reinforced") ||
                     entity_name.Contains("shopfront") ||
                     entity_name.Contains("bars") ||
                     entity_name.Contains("shutter") ||
                     entity_name.Contains("netting") ||
                     (entity_name.Contains("door") && !entity_name.Contains("doorway")) ||
                     entity_name.Contains("hatch") ||
                     entity_name.Contains("garagedoor") ||
                     entity_name.Contains("cell") ||
                     entity_name.Contains("fence") ||
                     entity_name.Contains("grill") ||
                     entity_name.Contains("Candle") ||
                     entity_name.Contains("candle") ||
                     entity_name.Contains("Strobe") ||
                     entity_name.Contains("speaker") ||
                     entity_name.Contains("Fog") ||
                     entity_name.Contains("composter") ||
                     entity_name.Contains("Graveyard"))
                {
                    damageAmount = before * configData.Multipliers.deployablesMultiplier;
                }
                else if(entity_name.Contains("furnace"))
                {
                    damageAmount = before * configData.Multipliers.furnaceMultiplier;
                }
                else if(entity_name.Contains("sedan"))
                {
                    damageAmount = before * configData.Multipliers.sedanMultiplier;
                }
                else if(entity_name == "SAM_Static")
                {
                    damageAmount = before * configData.Multipliers.samMultiplier;
                }
                else if(entity_name == "HotAirBalloon")
                {
                    damageAmount = before * configData.Multipliers.baloonMultiplier;
                    mundane = true;
                }
                else if(entity_name == "BBQ.Deployed")
                {
                    damageAmount = before * configData.Multipliers.bbqMultiplier;
                }
                else if(entity_name.Contains("watchtower"))
                {
                    damageAmount = before * configData.Multipliers.watchtowerMultiplier;
                }
                else if(entity_name == "WaterBarrel" ||
                        entity_name.Contains("jackolantern") ||
                        entity_name.Contains("water_catcher"))
                {
                    damageAmount = before * configData.Multipliers.deployablesMultiplier;
                }
                else if(entity_name == "beartrap" ||
                        entity_name == "landmine" ||
                        entity_name == "spikes.floor")
                {
                    damageAmount = before * configData.Multipliers.trapMultiplier;
                }
                else if(entity_name.Contains("barricade"))
                {
                    damageAmount = before * configData.Multipliers.barricadeMultiplier;
                }
                else if(entity_name == "gates.external.high.stone" || entity_name == "wall.external.high.stone")
                {
                    damageAmount = before * configData.Multipliers.highStoneWallMultiplier;
                }
                else if(entity_name == "gates.external.high.wood" || entity_name == "wall.external.high.wood")
                {
                    damageAmount = before * configData.Multipliers.highWoodWallMultiplier;
                }
                else if(entity_name == "mining.pumpjack")
                {
                    damageAmount = 0.0f;
                }
                else if(entity_name == "Rowboat" || entity_name == "RHIB")
                {
                    damageAmount = before * configData.Multipliers.boatMultiplier;
                    mundane = true;
                }
                else if(entity_name == "minicopter.entity")
                {
                    //if (MyMiniCopter) return null;
                    damageAmount = before * configData.Multipliers.minicopterMultiplier;
                    mundane = true;
                }
                else if(entity_name.Contains("TestRidableHorse"))
                {
                    damageAmount = before * configData.Multipliers.horseMultiplier;
                    mundane = true;
                }
                else if(entity_name == "ScrapTransportHelicopter")
                {
                    damageAmount = before * configData.Multipliers.scrapcopterMultiplier;
                    mundane = true;
                }
                else if(entity_name == "BaseVehicle" ||
                    entity.name.Contains("vehicle.chassis") ||
                    entity.name.Contains("chassis_") ||
                    entity.name.Contains("1module_") ||
                    entity.name.Contains("2module_") ||
                    entity.name.Contains("3module_") ||
                    entity.name.Contains("4module_"))
                {
                    damageAmount = before * configData.Multipliers.vehicleMultiplier;
                    mundane = true;
                }
                else
                {
                    Puts($"Unsupported decaying entity detected: {entity_name} --- please notify author.");
                    return null;
                }

                // Check non-building entities for cupboard in range
                if (configData.Global.requireCupboard && configData.Global.cupboardCheckEntity && !isBlock)
                {
                    // Verify that we should check for a cupboard and ensure that one exists.
                    // If so, multiplier will be set to entityCupboardMultiplier.
                    OutputRcon($"NoDecay checking for local cupboard.", mundane);

                    if (CheckCupboardEntity(entity, mundane))
                    {
                        damageAmount = before * configData.Multipliers.entityCupboardMultiplier;
                    }
                }

                NextTick(() =>
                {
                    OutputRcon($"Decay ({entity_name}) before: {before} after: {damageAmount}, item health {entity.health.ToString()}", mundane);
                    entity.health -= damageAmount;
                    if(entity.health == 0 && configData.Global.DestroyOnZero)
                    {
                        OutputRcon($"Entity completely decayed - destroying!", mundane);
                        if(entity == null) return;
                        entity.Kill(BaseNetworkable.DestroyMode.Gib);
                    }
                });
                return true; // Cancels this hook for any of the entities above unless unsupported (for decay only).
            }
            finally
            {
                double ms = (DateTime.Now - tick).TotalMilliseconds;
                if(ms > 10 || configData.Debug.outputMundane) Puts($"NoDecay.OnEntityTakeDamage on {entity_name} took {ms} ms to execute.");
            }
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
                    if(hascup) multiplier = configData.Multipliers.twigMultiplier;
                    type = "twig";
                    break;
                case BuildingGrade.Enum.Wood:
                    if(isHighWall)
                    {
                        if(hascup) multiplier = configData.Multipliers.highWoodWallMultiplier;
                        type = "high wood wall";
                    }
                    else if(isHighGate)
                    {
                        if(hascup) multiplier = configData.Multipliers.highWoodWallMultiplier;
                        type = "high wood gate";
                    }
                    else
                    {
                        if(hascup) multiplier = configData.Multipliers.woodMultiplier;
                        type = "wood";
                    }
                    break;
                case BuildingGrade.Enum.Stone:
                    if(isHighWall)
                    {
                        if(hascup) multiplier = configData.Multipliers.highStoneWallMultiplier;
                        type = "high stone wall";
                    }
                    else if(isHighGate)
                    {
                        if(hascup) multiplier = configData.Multipliers.highStoneWallMultiplier;
                        type = "high stone gate";
                    }
                    else
                    {
                        if(hascup) multiplier = configData.Multipliers.stoneMultiplier;
                        type = "stone";
                    }
                    break;
                case BuildingGrade.Enum.Metal:
                    if(hascup) multiplier = configData.Multipliers.sheetMultiplier;
                    type = "sheet";
                    break;
                case BuildingGrade.Enum.TopTier:
                    if(hascup) multiplier = configData.Multipliers.armoredMultiplier;
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
            Vis.Entities<BuildingPrivlidge>(entity.transform.position, configData.Global.cupboardRange, cups, targetLayer);

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

        void OnEntitySpawned(BuildingPrivlidge priv)
        {
            OutputRcon("OnEntitySpawned called for TC");
            if (!configData.Global.disableWarning) return;
            timer.Repeat(0.05f, 40, () =>
            {
                if (priv == null) return;
                if ((permission.UserHasPermission(priv.OwnerID.ToString(), "nodecay.use") && configData.Global.usePermission) || !configData.Global.usePermission)
                {
                    nextProtectedCalcTime.SetValue(priv, Time.realtimeSinceStartup * 2f);
                    cachedProtectedMinutes.SetValue(priv, 65535);
                    priv.SendNetworkUpdateImmediate();
                }
            });
        }

        // Prevent players from adding building resources to cupboard if so configured
        private object CanMoveItem(Item item, PlayerInventory inventory, uint targetContainer, int targetSlot)
        {
            if(item == null) return null;
            if(targetContainer == 0) return null;
            if(targetSlot == 0) return null;
            ItemContainer container = inventory.FindContainer(targetContainer);

            if (configData.Global.disableWarning)
            {
                timer.Repeat(0.05f, 40, () =>
                {
                    BuildingPrivlidge priv = item?.parent?.entityOwner?.GetComponent<BuildingPrivlidge>();
                    if (priv == null) return;
                    if ((permission.UserHasPermission(priv.OwnerID.ToString(), "nodecay.use") && configData.Global.usePermission) || !configData.Global.usePermission)
                    {
                        nextProtectedCalcTime.SetValue(priv, Time.realtimeSinceStartup * 2f);
                        cachedProtectedMinutes.SetValue(priv, 65535);
                        priv.SendNetworkUpdateImmediate();
                    }
                });
            }

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
                else if(args[0] == "info")
                {
                    string info = "NoDecay current settings";
                    info += "\n\tentityCupboardMultiplier: " + configData.Multipliers.entityCupboardMultiplier.ToString();
                    info += "\n\ttwigMultiplier: " + configData.Multipliers.twigMultiplier.ToString();
                    info += "\n\twoodMultiplier: " + configData.Multipliers.woodMultiplier.ToString();
                    info += "\n\tstoneMultiplier: " + configData.Multipliers.stoneMultiplier.ToString();
                    info += "\n\tsheetMultiplier: " + configData.Multipliers.sheetMultiplier.ToString();
                    info += "\n\tarmoredMultiplier: " + configData.Multipliers.armoredMultiplier.ToString();

                    info += "\n\tbaloonMultiplier: " + configData.Multipliers.baloonMultiplier.ToString();
                    info += "\n\tbarricadeMultiplier: " + configData.Multipliers.barricadeMultiplier.ToString();
                    info += "\n\tbbqMultiplier: " + configData.Multipliers.bbqMultiplier.ToString();
                    info += "\n\tboatMultiplier: " + configData.Multipliers.boatMultiplier.ToString();
                    info += "\n\tboxMultiplier: " + configData.Multipliers.boxMultiplier.ToString();
                    info += "\n\tcampfireMultiplier " + configData.Multipliers.campfireMultiplier.ToString();
                    info += "\n\tdeployablesMultiplier: " + configData.Multipliers.deployablesMultiplier.ToString();
                    info += "\n\tfurnaceMultiplier: " + configData.Multipliers.furnaceMultiplier.ToString();
                    info += "\n\thighWoodWallMultiplier: " + configData.Multipliers.highWoodWallMultiplier.ToString();
                    info += "\n\thighStoneWallMultiplier: " + configData.Multipliers.highStoneWallMultiplier.ToString();
                    info += "\n\thorseMultiplier: " + configData.Multipliers.horseMultiplier.ToString();
                    info += "\n\tminicopterMultiplier: " + configData.Multipliers.minicopterMultiplier.ToString();
                    info += "\n\tsamMultiplier: " + configData.Multipliers.samMultiplier.ToString();
                    info += "\n\tscrapcopterMultiplier: " + configData.Multipliers.scrapcopterMultiplier.ToString();
                    info += "\n\tsedanMultiplier: " + configData.Multipliers.sedanMultiplier.ToString();
                    info += "\n\ttrapMultiplier: " + configData.Multipliers.trapMultiplier.ToString();
                    info += "\n\tvehicleMultiplier: " + configData.Multipliers.vehicleMultiplier.ToString();
                    info += "\n\twatchtowerMultiplier: " + configData.Multipliers.watchtowerMultiplier.ToString();

                    info += "\n\n\tEnabled: " + enabled.ToString();
                    info += "\n\tdisableWarning: " + configData.Global.disableWarning.ToString();
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
        [HookMethod("SendHelpText")]
        private void SendHelpText(BasePlayer player)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<color=#05eb59>" + Name + " " + Version + "</color> · Controls decay\n");
            sb.Append("  · ").AppendLine($"twig={configData.Multipliers.twigMultiplier} - campfire={configData.Multipliers.campfireMultiplier}");
            sb.Append("  · ").Append($"wood ={ configData.Multipliers.woodMultiplier} - stone ={ configData.Multipliers.stoneMultiplier} - sheet ={ configData.Multipliers.sheetMultiplier} - armored ={ configData.Multipliers.armoredMultiplier}\n");

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
            public Multipliers Multipliers = new Multipliers();
            public Multipliers Mutipliers = new Multipliers(); // Temporary from old configs
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
            public float cupboardRange = 30f;
            public bool DestroyOnZero = true;
            public bool useJPipes = false;
            public bool blockCupboardResources = false;
            public bool blockCupboardWood = false;
            public bool blockCupboardStone = false;
            public bool blockCupboardMetal = false;
            public bool blockCupboardArmor = false;
            public bool disableWarning = true;
        }

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

        protected override void LoadDefaultConfig() => Puts("New configuration file created.");

        void LoadConfigValues()
        {
            configData = Config.ReadObject<ConfigData>();
            if (configData.Version < new VersionNumber(1, 0, 46) || configData.Version == null)
            {
                Puts("Upgrading config file...");
                configData.Multipliers = configData.Mutipliers;
                configData.Mutipliers = null;
            }
            if (configData.Version < new VersionNumber(1, 0, 47))
            {
                configData.Multipliers.entityCupboardMultiplier = 0f;
            }
            if (configData.Version < new VersionNumber(1, 0, 48))
            {
                configData.Global.disableWarning = true;
            }
            if (configData.Version < new VersionNumber(1, 0, 51))
            {
                configData.Global.useJPipes = false;
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
