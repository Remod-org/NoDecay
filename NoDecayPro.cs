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
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoDecay Pro", "RFC1920", "1.0.2")]
    [Description("Scales or disables decay of items with decay.upkeep disabled globally.")]
    class NoDecayPro : RustPlugin
    {
        #region main
        private Dictionary<string, long> lastConnected = new Dictionary<string, long>();
        private Dictionary<string, List<string>> entityinfo = new Dictionary<string, List<string>>();

        [PluginReference]
        private readonly Plugin JPipes;
        public static NoDecayPro Instance = null;

        private ConfigData configData;
        private bool enabled = true;

        void Init()
        {
            Instance = this;
            permission.RegisterPermission("nodecaypro.use", this);
            permission.RegisterPermission("nodecaypro.admin", this);
            LoadData();
            if (entityinfo.Count == 0) UpdateEnts();
        }

        void Loaded()
        {
            LoadConfigValues();
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "decay.upkeep 0");

            var ents = UnityEngine.Object.FindObjectsOfType<BaseEntity>();
            foreach(var ent in ents)
            {
                if (ent.OwnerID == 0) continue;
                var gd = ent.GetComponentInChildren<GlobalDecay>();
                OutputRcon($"Restarting GlobalDecay object for {ent.name}");
                try
                {
                    UnityEngine.Object.Destroy(gd);
                    var obj = ent.gameObject.AddComponent<GlobalDecay>();
                }
                catch { }
            }
        }

        void Unload()
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "decay.upkeep 1");

            var ents = UnityEngine.Object.FindObjectsOfType<BaseEntity>();
            foreach(var ent in ents)
            {
                if (ent.OwnerID == 0) continue;
                var gd = ent.GetComponentInChildren<GlobalDecay>();
                try
                {
                    gd.enabled = false;
                }
                catch { }
            }
        }

        void OnEntitySpawned(BaseEntity entity)
        {
            if (entity.OwnerID == 0) return;
            var obj = entity.gameObject.AddComponent<GlobalDecay>();
        }

        private void OnEntitySaved(BuildingPrivlidge buildingPrivilege, BaseNetworkable.SaveInfo saveInfo)
        {
            if (configData.Global.disableWarning)
            {
                if (configData.Global.usePermission)
                {
                    var owner = buildingPrivilege.OwnerID.ToString();
                    if (permission.UserHasPermission(owner, "nodecaypro.use") || owner == "0")
                    {
                        if (owner != "0")
                        {
                            OutputRcon($"TC owner {owner} has NoDecayPro permission!", true);
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

        private float ProcessBuildingDamage(BaseEntity entity, float before)
        {
            var block = entity as BuildingBlock;
            float multiplier = 1.0f;
            float damageAmount = 1.0f;
            bool isHighWall = block.LookupPrefab().name.Contains("wall.external");
            bool isHighGate = block.LookupPrefab().name.Contains("gates.external");

            string type = null;
            bool hascup = true; // Assume true (has cupboard or we don't require one)

            OutputRcon($"NoDecayPro checking for block damage to {block.LookupPrefab().name}");

            // Verify that we should check for a cupboard and ensure that one exists.
            // If not, multiplier will be standard of 1.0f (hascup true).
            if(configData.Global.requireCupboard == true)
            {

                OutputRcon($"NoDecayPro checking for local cupboard.");
                hascup = CheckCupboardBlock(block, entity.LookupPrefab().name, block.grade.ToString().ToLower());
            }
            else
            {
                OutputRcon($"NoDecayPro NOT checking for local cupboard.");
            }

            switch(block.grade)
            {
                case BuildingGrade.Enum.Twigs:
                    if(hascup) multiplier = configData.Multipliers["twig"];
                    type = "twig";
                    break;
                case BuildingGrade.Enum.Wood:
                    if(isHighWall)
                    {
                        if(hascup) multiplier = configData.Multipliers["highWoodWall"];
                        type = "high wood wall";
                    }
                    else if(isHighGate)
                    {
                        if(hascup) multiplier = configData.Multipliers["highWoodWall"];
                        type = "high wood gate";
                    }
                    else
                    {
                        if(hascup) multiplier = configData.Multipliers["wood"];
                        type = "wood";
                    }
                    break;
                case BuildingGrade.Enum.Stone:
                    if(isHighWall)
                    {
                        if(hascup) multiplier = configData.Multipliers["highStoneWall"];
                        type = "high stone wall";
                    }
                    else if(isHighGate)
                    {
                        if(hascup) multiplier = configData.Multipliers["highStoneWall"];
                        type = "high stone gate";
                    }
                    else
                    {
                        if(hascup) multiplier = configData.Multipliers["stone"];
                        type = "stone";
                    }
                    break;
                case BuildingGrade.Enum.Metal:
                    if(hascup) multiplier = configData.Multipliers["sheet"];
                    type = "sheet";
                    break;
                case BuildingGrade.Enum.TopTier:
                    if(hascup) multiplier = configData.Multipliers["armored"];
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
            if (!permission.UserHasPermission(player.UserIDString, "nodecaypro.admin")) return;
            if(args.Length > 0)
            {
                if(args[0] == "enable")
                {
                    enabled = !enabled;
                    SendReply(player, $"NoDecayPro enabled set to {enabled.ToString()}");
                }
                else if(args[0] == "log" || args[0] == "debug")
                {
                    configData.Debug.outputToRcon = !configData.Debug.outputToRcon;
                    SendReply(player, $"Debug logging set to {configData.Debug.outputToRcon.ToString()}");
                    SaveConfig();
                }
                else if(args[0] == "info")
                {
                    string info = "NoDecayPro current settings";
                    info += "\n\tentityCupboardMultiplier: " + configData.Multipliers["entityCupboard"].ToString();
                    info += "\n\ttwigMultiplier: " + configData.Multipliers["twig"].ToString();
                    info += "\n\twoodMultiplier: " + configData.Multipliers["wood"].ToString();
                    info += "\n\tstoneMultiplier: " + configData.Multipliers["stone"].ToString();
                    info += "\n\tsheetMultiplier: " + configData.Multipliers["sheet"].ToString();
                    info += "\n\tarmoredMultiplier: " + configData.Multipliers["armored"].ToString();

                    info += "\n\tballoonMultiplier: " + configData.Multipliers["balloon"].ToString();
                    info += "\n\tbarricadeMultiplier: " + configData.Multipliers["barricade"].ToString();
                    info += "\n\tbbqMultiplier: " + configData.Multipliers["bbq"].ToString();
                    info += "\n\tboatMultiplier: " + configData.Multipliers["boat"].ToString();
                    info += "\n\tboxMultiplier: " + configData.Multipliers["box"].ToString();
                    info += "\n\tcampfireMultiplier " + configData.Multipliers["campfire"].ToString();
                    info += "\n\tdeployablesMultiplier: " + configData.Multipliers["deployables"].ToString();
                    info += "\n\tfurnaceMultiplier: " + configData.Multipliers["furnace"].ToString();
                    info += "\n\thighWoodWallMultiplier: " + configData.Multipliers["highWoodWall"].ToString();
                    info += "\n\thighStoneWallMultiplier: " + configData.Multipliers["highStoneWall"].ToString();
                    info += "\n\thorseMultiplier: " + configData.Multipliers["horse"].ToString();
                    info += "\n\tminicopterMultiplier: " + configData.Multipliers["minicopter"].ToString();
                    info += "\n\tsamMultiplier: " + configData.Multipliers["sam"].ToString();
                    info += "\n\tscrapcopterMultiplier: " + configData.Multipliers["scrapcopter"].ToString();
                    info += "\n\tsedanMultiplier: " + configData.Multipliers["sedan"].ToString();
                    info += "\n\ttrapMultiplier: " + configData.Multipliers["trap"].ToString();
                    info += "\n\tvehicleMultiplier: " + configData.Multipliers["vehicle"].ToString();
                    info += "\n\twatchtowerMultiplier: " + configData.Multipliers["watchtower"].ToString();

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
            sb.Append("  · ").AppendLine($"twig={configData.Multipliers["twig"]} - campfire={configData.Multipliers["campfire"]}");
            sb.Append("  · ").Append($"wood ={ configData.Multipliers["wood"]} - stone ={ configData.Multipliers["stone"]} - sheet ={ configData.Multipliers["sheet"]} - armored ={ configData.Multipliers["armored"]}\n");

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
                if (!mundane)
                {
                    Interface.Oxide.LogDebug($"{message}");
                }
                else if (mundane && configData.Debug.outputMundane)
                {
                    Interface.Oxide.LogDebug($"{message}");
                }
            }
        }
        #endregion

        #region config
        private class ConfigData
        {
            public Debug Debug = new Debug();
            public Global Global = new Global();
            public VersionNumber Version;

            public Dictionary<string, float> Multipliers;
//            public List<string> blocks = new List<string>() { "twig", "wood", "stone", "sheet", "armored" };
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
            public float decaytick = 300f;
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

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file");
            configData = new ConfigData
            {
                Version = Version,
                Multipliers = new Dictionary<string, float>()
                {
                    { "entityCupboard", 0f },
                    { "twig", 1.0f },
                    { "wood", 0f },
                    { "stone", 0f },
                    { "sheet", 0f },
                    { "armored", 0f },
                    { "balloon", 0f },
                    { "barricade", 0f },
                    { "bbq", 0f },
                    { "boat", 0f },
                    { "box", 0f },
                    { "campfire", 0f },
                    { "furnace", 0f },
                    { "highWoodWall", 0f },
                    { "highStoneWall", 0f },
                    { "horse", 0f },
                    { "minicopter", 0f },
                    { "sam", 0f },
                    { "scrapcopter", 0f },
                    { "sedan", 0f },
                    { "trap", 0f },
                    { "vehicle", 0f },
                    { "watchtower", 0f },
                    { "deployables", 0.1f } // For all others not listed
                }
            };
            SaveConfig(configData);
        }

        void LoadConfigValues()
        {
            configData = Config.ReadObject<ConfigData>();
            configData.Version = Version;

            SaveConfig(configData);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

        class GlobalDecay : MonoBehaviour
        {
            private BaseCombatEntity entity;
            private HitInfo hitinfo;
            private bool enable = true;

            public void Start()
            {
                useGUILayout = false;
            }
            public void Awake()
            {
                entity = GetComponentInParent<BaseCombatEntity>();
                if (entity == null) return;
                if (entity.OwnerID == 0) return;
                hitinfo = new HitInfo();
                hitinfo.damageTypes = new DamageTypeList();
                hitinfo.damageTypes.Add(DamageType.Decay, 1f);
                InvokeRepeating("ProcessDecay", 0, Instance.configData.Global.decaytick);
            }

            public void OnDisable()
            {
                Instance.OutputRcon($"Killing GlobalDecay object for {entity.name}");
                CancelInvoke("ProcessDecay");
                Destroy(this);
            }

            void ProcessDecay()
            {
                if (entity == null) return;

                float damageAmount = 0f;
                DateTime tick = DateTime.Now;
                string entity_name = entity.LookupPrefab().name;
                Instance.OutputRcon($"ProcessDecay called for {entity_name}");

                Instance.OutputRcon($"Processing decay for {entity_name}...");

                string owner = entity.OwnerID.ToString();
                bool mundane = false;
                bool isBlock = false;

                if (Instance.configData.Global.usePermission)
                {
                    if (Instance.permission.UserHasPermission(owner, "nodecaypro.use") || owner == "0")
                    {
                        if (owner != "0")
                        {
                            Instance.OutputRcon($"{entity_name} owner {owner} has NoDecayPro permission!");
                        }
                    }
                    else
                    {
                        Instance.OutputRcon($"{entity_name} owner {owner} does NOT have NoDecayPro permission.  Standard decay in effect.");
                        return;
                    }
                }
                if (Instance.configData.Global.protectedDays > 0 && entity.OwnerID > 0)
                {
                    long lc = 0;
                    Instance.lastConnected.TryGetValue(entity.OwnerID.ToString(), out lc);
                    if (lc > 0)
                    {
                        long now = Instance.ToEpochTime(DateTime.UtcNow);
                        float days = Math.Abs((now - lc) / 86400);
                        if (days > Instance.configData.Global.protectedDays)
                        {
                            Instance.OutputRcon($"Allowing decay for owner offline for {Instance.configData.Global.protectedDays.ToString()} days");
                            return;
                        }
                        else
                        {
                            Instance.OutputRcon($"Owner was last connected {days.ToString()} days ago and is still protected...");
                        }
                    }
                }

                try
                {
                    float before = hitinfo.damageTypes.Get(DamageType.Decay);

                    if (entity is BuildingBlock)
                    {
                        if (Instance.configData.Global.useJPipes)
                        {
                            if ((bool)Instance.JPipes?.Call("IsPipe", entity))
                            {
                                if ((bool)Instance.JPipes?.Call("IsNoDecayEnabled"))
                                {
                                    Instance.OutputRcon("Found a JPipe with nodecay enabled");
                                    hitinfo.damageTypes.Scale(DamageType.Decay, 0f);
                                    return;
                                }
                            }
                        }

                        damageAmount = Instance.ProcessBuildingDamage(entity, before);
                        Instance.OutputRcon($"Decay for {entity_name} was {before.ToString()} now {damageAmount.ToString()}");
                        isBlock = true;
                    }
                    else if (entity is ModularCar)
                    {
                        var garage = entity.GetComponentInParent<ModularCarGarage>();
                        if (garage != null && Instance.configData.Global.protectVehicleOnLift)
                        {
                            return;
                        }
                    }
                    else
                    {
                        // Main check for non-building entities/deployables
                        foreach (KeyValuePair<string, List<string>> entities in Instance.entityinfo)
                        {
                            if(entities.Value.Contains(entity_name))
                            {
                                damageAmount = before * Instance.configData.Multipliers[entities.Key];
                            }
                        }
                    }

                    // Check non-building entities for cupboard in range
                    if (Instance.configData.Global.requireCupboard && Instance.configData.Global.cupboardCheckEntity && !isBlock)
                    {
                        // Verify that we should check for a cupboard and ensure that one exists.
                        // If so, multiplier will be set to entityCupboard Multiplier.
                        Instance.OutputRcon($"NoDecayPro checking for local cupboard for {entity_name}.", mundane);

                        if (Instance.CheckCupboardEntity(entity, mundane))
                        {
                            damageAmount = before * Instance.configData.Multipliers["entityCupboard"];
                        }
                    }

                    Instance.OutputRcon($"Decay ({entity_name}) before: {before} after: {damageAmount}, item health {entity.health.ToString()}", mundane);
                    entity.health -= damageAmount;
                    if (entity.health == 0 && Instance.configData.Global.DestroyOnZero)
                    {
                        Instance.OutputRcon($"Entity {entity_name} completely decayed - destroying!", mundane);
                        if (entity == null) return;
                        entity.Kill(BaseNetworkable.DestroyMode.Gib);
                    }
                }
                finally
                {
                    double ms = (DateTime.Now - tick).TotalMilliseconds;
                    if (ms > Instance.configData.Global.warningTime || Instance.configData.Debug.outputMundane)
                    {
                        Interface.Oxide.LogWarning($"NoDecay.OnEntityTakeDamage on {entity_name} took {ms} ms to execute.");
                    }
                }
            }
        }
    }
}
