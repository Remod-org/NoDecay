using System;
using System.Collections.Generic;
using System.Text;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoDecay", "RFC1920", "1.0.44", ResourceId = 1160)]
    //Original Credit to Deicide666ra/Piarb and Diesel_42o
    // Thanks to Deicide666ra for allowing me to continue his work on this plugin
    [Description("Scales or disables decay of items")]
    class NoDecay : RustPlugin
    {
        private float c_twigMultiplier;
        private float c_woodMultiplier;
        private float c_stoneMultiplier;
        private float c_sheetMultiplier;
        private float c_armoredMultiplier;

        private float c_campfireMultiplier;
        private float c_highWoodWallMultiplier;
        private float c_highStoneWallMultiplier;
        private float c_barricadeMultiplier;
        private float c_trapMultiplier;
        private float c_deployablesMultiplier;
        private float c_boxMultiplier;
        private float c_sedanMultiplier;
        private float c_samMultiplier;
        private float c_baloonMultiplier;
        private float c_furnaceMultiplier;
        private float c_bbqMultiplier;
        private float c_boatMultiplier;
        private float c_minicopterMultiplier;
        private float c_scrapcopterMultiplier;
        private float c_watchtowerMultiplier;
        private float c_horseMultiplier;

        private bool c_outputToRcon;
        private bool c_outputMundane;
        private bool c_usePermission;
        private bool c_requireCupboard;
        private bool c_CupboardEntity;
        private bool c_DestroyOnZero;
        private bool c_blockCupboardResources;
        private bool c_blockCupboardWood;
        private bool c_blockCupboardStone;
        private bool c_blockCupboardMetal;
        private bool c_blockCupboardArmor;
        private float c_cupboardRange;

        private bool g_configChanged;

        void Loaded() => LoadConfigValues();

        protected override void LoadDefaultConfig() => Puts("New configuration file created.");

        void Init()
        {
            permission.RegisterPermission("nodecay.use", this);
        }

        void LoadConfigValues()
        {
            c_twigMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "twigMultiplier", 1.0));
            c_woodMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "woodMultiplier", 0.0));
            c_stoneMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "stoneMultiplier", 0.0));
            c_sheetMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "sheetMultiplier", 0.0));
            c_armoredMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "armoredMultiplier", 0.0));

            c_deployablesMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "deployablesMultiplier", 0.0));
            c_watchtowerMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "watchtowerMultiplier", 0.0));
            c_horseMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "horseMultiplier", 0.0));
            c_boxMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "boxMultiplier", 0.0));
            c_sedanMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "sedanMultiplier", 0.0));
            c_baloonMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "baloonMultiplier", 0.0));
            c_furnaceMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "furnaceMultiplier", 0.0));
            c_bbqMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "bbqMultiplier", 0.0));
            c_samMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "samMultiplier", 0.0));
            c_campfireMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "campfireMultiplier", 0.0));
            c_barricadeMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "barricadesMultiplier", 0.0));
            c_trapMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "trapMultiplier", 0.0));
            c_highWoodWallMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "highWoodWallMultiplier", 0.0));
            c_highStoneWallMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "highStoneWallMultiplier", 0.0));
            c_boatMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "boatMultiplier", 0.0));
            c_minicopterMultiplier  = Convert.ToSingle(GetConfigValue("Mutipliers", "minicopterMultiplier", 0.0));
            c_scrapcopterMultiplier = Convert.ToSingle(GetConfigValue("Mutipliers", "scrapcopterMultiplier", 0.0));

            c_outputToRcon  = Convert.ToBoolean(GetConfigValue("Debug", "outputToRcon", false));
            c_outputMundane = Convert.ToBoolean(GetConfigValue("Debug", "outputMundane", false));
            c_usePermission = Convert.ToBoolean(GetConfigValue("Global", "usePermission", false));
            c_DestroyOnZero = Convert.ToBoolean(GetConfigValue("Global", "DestroyOnZero", false));
            c_blockCupboardResources = Convert.ToBoolean(GetConfigValue("Global", "blockCupboardResources", false));
            c_blockCupboardWood  = Convert.ToBoolean(GetConfigValue("Global", "blockCupboardWood", false));
            c_blockCupboardStone = Convert.ToBoolean(GetConfigValue("Global", "blockCupboardStone", false));
            c_blockCupboardMetal = Convert.ToBoolean(GetConfigValue("Global", "blockCupboardMetal", false));
            c_blockCupboardArmor = Convert.ToBoolean(GetConfigValue("Global", "blockCupboardArmor", false));

            try
            {
                c_requireCupboard = Convert.ToBoolean(GetConfigValue("Global", "requireCupboard", false));
                c_CupboardEntity  = Convert.ToBoolean(GetConfigValue("Global", "cupboardCheckEntity", false));
                c_cupboardRange   = Convert.ToSingle(GetConfigValue("Global", "cupboardRange", 30.0));
            }
            catch
            {
                c_requireCupboard = false;
                c_CupboardEntity  = false;
                c_cupboardRange   = 30f;
            }

            if(g_configChanged)
            {
                Puts("Configuration file updated.");
                SaveConfig();
            }
        }

        object GetConfigValue(string category, string setting, object defaultValue)
        {
            Dictionary<string, object> data = Config[category] as Dictionary<string, object>;
            object value;

            if(data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
                g_configChanged = true;
            }

            if(data.TryGetValue(setting, out value)) return value;
            value = defaultValue;
            data[setting] = value;
            g_configChanged = true;
            return value;
        }

        [HookMethod("SendHelpText")]
        private void SendHelpText(BasePlayer player)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<color=#05eb59>" + Name + " " + Version + "</color> · Controls decay\n");
            sb.Append("  · ").AppendLine($"twig={c_twigMultiplier} - campfire={c_campfireMultiplier}");
            sb.Append("  · ").Append($"wood ={ c_woodMultiplier} - stone ={ c_stoneMultiplier} - sheet ={ c_sheetMultiplier} - armored ={ c_armoredMultiplier}\n");

            if(c_requireCupboard == true)
            {
                if(c_CupboardEntity == true)
                {
                    string range = c_cupboardRange.ToString();
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
            if(c_outputToRcon)
            {
                if(!mundane) Puts($"{message}");
                else if(mundane && c_outputMundane) Puts($"{message}");
            }
        }

        // Prevent players from adding building resources to cupboard
        private object CanMoveItem(Item item, PlayerInventory inventory, uint targetContainer, int targetSlot)
        {
            if(!(c_blockCupboardResources || c_blockCupboardWood)) return null;
            if(!(c_blockCupboardStone || c_blockCupboardMetal || c_blockCupboardArmor)) return null;
            if(item == null) return null;
            if(targetContainer == null) return null;

            ItemContainer container = inventory.FindContainer(targetContainer);

            try
            {
                var cup = container.entityOwner as BaseEntity;

                if(cup.name.Contains("cupboard.tool"))
                {
                    string res = item.info.shortname;
                    if(res.Contains("wood") && c_blockCupboardWood)
                    {
                        OutputRcon($"Player tried to add {res} to a cupboard!");
                        return false;
                    }
                    else if((res.Contains("stones") || res.Contains("metal.frag") || res.Contains("metal.refined")) && c_blockCupboardResources)
                    {
                        OutputRcon($"Player tried to add {res} to a cupboard!");
                        return false;
                    }
                    else if(
                        (res.Contains("stones") && c_blockCupboardStone)
                        || (res.Contains("metal.frag") && c_blockCupboardMetal)
                        || (res.Contains("metal.refined") && c_blockCupboardArmor))
                    {
                        OutputRcon($"Player tried to add {res} to a cupboard!");
                        return false;
                    }
                }
            }
            catch {}
            return null;
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if(entity == null || hitInfo == null) return null;
            if(!hitInfo.damageTypes.Has(Rust.DamageType.Decay)) return null;

            float damageAmount = 0f;
            DateTime tick = DateTime.Now;
            string entity_name = entity.LookupPrefab().name;
            string owner = entity.OwnerID.ToString();
            bool mundane = false;

            if(c_usePermission)
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
                var block = entity as BuildingBlock;
                float before = hitInfo.damageTypes.Get(Rust.DamageType.Decay);

                if(block != null)
                {
                    damageAmount = ProcessBuildingDamage(block, entity, before);
                }
                else if(entity_name == "campfire" || entity_name == "skull_fire_pit")
                {
                    damageAmount = before * c_campfireMultiplier;
                }
                else if(entity_name == "box.wooden.large" ||
                        entity_name == "woodbox_deployed" ||
                        entity_name == "CoffinStorage")
                {
                    damageAmount = before * c_boxMultiplier;
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
                    if(c_requireCupboard && c_CupboardEntity)
                    {
                        // Verify that we should check for a cupboard and ensure that one exists.
                        // If not, multiplier will be standard of 1.0f.
                        OutputRcon($"NoDecay checking for local cupboard.");

                        if(CheckCupboardEntity(entity))
                        {
                            damageAmount = before * c_deployablesMultiplier;
                        }
                    }
                    else
                    {
                        damageAmount = before * c_deployablesMultiplier;
                    }

                    OutputRcon($"Decay({entity_name}) before: {before} after: {damageAmount}");
                }
                else if(entity_name.Contains("furnace"))
                {
                    damageAmount = before * c_furnaceMultiplier;
                }
                else if(entity_name.Contains("sedan"))
                {
                    damageAmount = before * c_sedanMultiplier;
                }
                else if(entity_name == "SAM_Static")
                {
                    damageAmount = before * c_samMultiplier;
                }
                else if(entity_name == "HotAirBalloon")
                {
                    damageAmount = before * c_baloonMultiplier;
                    mundane = true;
                }
                else if(entity_name == "BBQ.Deployed")
                {
                    damageAmount = before * c_bbqMultiplier;
                }
                else if(entity_name.Contains("watchtower"))
                {
                    damageAmount = before * c_watchtowerMultiplier;
                }
                else if(entity_name == "WaterBarrel" ||
                        entity_name == "jackolantern.angry" ||
                        entity_name == "jackolantern.happy" ||
                        entity_name == "water_catcher_small" ||
                        entity_name == "water_catcher_large")
                {
                    damageAmount = before * c_deployablesMultiplier;
                }
                else if(entity_name == "beartrap" ||
                        entity_name == "landmine" ||
                        entity_name == "spikes.floor")
                {
                    damageAmount = before * c_trapMultiplier;
                }
                else if(entity_name.Contains("barricade"))
                {
                    damageAmount = before * c_barricadeMultiplier;
                }
                else if(entity_name == "gates.external.high.stone")
                {
                    damageAmount = before * c_highStoneWallMultiplier;
                }
                else if(entity_name == "gates.external.high.wood")
                {
                    damageAmount = before * c_highWoodWallMultiplier;
                }
                else if(entity_name == "wall.external.high.stone")
                {
                    damageAmount = before * c_highStoneWallMultiplier;
                }
                else if(entity_name == "wall.external.high.wood")
                {
                    damageAmount = before * c_highWoodWallMultiplier;
                }
                else if(entity_name == "mining.pumpjack")
                {
                    damageAmount = 0.0f;
                }
                else if(entity_name == "Rowboat" || entity_name == "RHIB")
                {
                    damageAmount = before * c_boatMultiplier;
                    mundane = true;
                }
                else if(entity_name == "minicopter.entity")
                {
                    damageAmount = before * c_minicopterMultiplier;
                    mundane = true;
                }
                else if(entity_name.Contains("TestRidableHorse"))
                {
                    damageAmount = before * c_horseMultiplier;
                    mundane = true;
                }
                else if(entity_name == "ScrapTransportHelicopter")
                {
                    damageAmount = before * c_scrapcopterMultiplier;
                    mundane = true;
                }
                else
                {
                    Puts($"Unsupported decaying entity detected: {entity_name} --- please notify author");
                    return null;
                }

                NextTick(() =>
                {
                    OutputRcon($"Decay ({entity_name}) before: {before} after: {damageAmount}, item health {entity.health.ToString()}", mundane);
                    entity.health -= damageAmount;
                    if(entity.health == 0 && c_DestroyOnZero)
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
                if(ms > 10 || c_outputMundane) Puts($"NoDecay.OnEntityTakeDamage on {entity_name} took {ms} ms to execute.");
            }
            return null;
        }

        private float ProcessBuildingDamage(BuildingBlock block, BaseEntity entity, float before)
        {
            float multiplier = 1.0f;
            float damageAmount = 1.0f;
            bool isHighWall = block.LookupPrefab().name.Contains("wall.external");
            bool isHighGate = block.LookupPrefab().name.Contains("gates.external");

            string type = null;
            bool hascup = true; // Assume true (has cupboard or we don't require one)

            OutputRcon($"NoDecay checking for block damage to {block.LookupPrefab().name}");

            // Verify that we should check for a cupboard and ensure that one exists.
            // If not, multiplier will be standard of 1.0f (hascup true).
            if(c_requireCupboard == true)
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
                    if(hascup)
                        multiplier = c_twigMultiplier;
                    type = "twig";
                    break;
                case BuildingGrade.Enum.Wood:
                    if(isHighWall)
                    {
                        if(hascup)
                            multiplier = c_highWoodWallMultiplier;
                        type = "high wood wall";
                    }
                    else if(isHighGate)
                    {
                        if(hascup)
                            multiplier = c_highWoodWallMultiplier;
                        type = "high wood gate";
                    }
                    else
                    {
                        if(hascup)
                            multiplier = c_woodMultiplier;
                        type = "wood";
                    }
                    break;
                case BuildingGrade.Enum.Stone:
                    if(isHighWall)
                    {
                        if(hascup)
                            multiplier = c_highStoneWallMultiplier;
                        type = "high stone wall";
                    }
                    else if(isHighGate)
                    {
                        if(hascup)
                            multiplier = c_highStoneWallMultiplier;
                        type = "high stone gate";
                    }
                    else
                    {
                        if(hascup)
                            multiplier = c_stoneMultiplier;
                        type = "stone";
                    }
                    break;
                case BuildingGrade.Enum.Metal:
                    if(hascup)
                        multiplier = c_sheetMultiplier;
                    type = "sheet";
                    break;
                case BuildingGrade.Enum.TopTier:
                    if(hascup)
                        multiplier = c_armoredMultiplier;
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
        bool CheckCupboardEntity(BaseEntity entity)
        {
            int targetLayer = LayerMask.GetMask("Construction", "Construction Trigger", "Trigger", "Deployed");
            List<BuildingPrivlidge> cups = new List<BuildingPrivlidge>();
            Vis.Entities<BuildingPrivlidge>(entity.transform.position, c_cupboardRange, cups, targetLayer);

            OutputRcon($"CheckCupboardEntity:   Checking for cupboard within {c_cupboardRange.ToString()}m of {entity.ShortPrefabName}.");

            if(cups.Count > 0)
            {
                // cupboard overlap.  Entity safe from decay.
                OutputRcon($"CheckCupboardEntity:     Found entity layer in range of cupboard!");
                return true;
            }

            OutputRcon($"CheckCupboardEntity:     Unable to find entity layer in range of cupboard.");
            return false;
        }
    }
}
