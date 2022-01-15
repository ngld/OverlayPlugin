using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace RainbowMage.OverlayPlugin.EventSources
{
    [Serializable]
    public class BuiltinEventConfig
    {
        public event EventHandler UpdateIntervalChanged;
        public event EventHandler EnmityIntervalChanged;
        public event EventHandler SortKeyChanged;
        public event EventHandler SortDescChanged;
        public event EventHandler UpdateDpsDuringImportChanged;
        public event EventHandler EndEncounterAfterWipeChanged;
        public event EventHandler EndEncounterOutOfCombatChanged;
        public event EventHandler LogLinesChanged;

        private int updateInterval;
        public int UpdateInterval {
            get
            {
                return this.updateInterval;
            }
            set
            {
                if (this.updateInterval != value)
                {
                    this.updateInterval = value;
                    UpdateIntervalChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        private int enmityIntervalMs;
        public int EnmityIntervalMs
        {
            get
            {
                return this.enmityIntervalMs;
            }
            set
            {
                if (this.enmityIntervalMs != value)
                {
                    this.enmityIntervalMs = value;
                    EnmityIntervalChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        private string sortKey;
        public string SortKey
        {
            get
            {
                return this.sortKey;
            }
            set
            {
                if (this.sortKey != value)
                {
                    this.sortKey = value;
                    SortKeyChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        private bool sortDesc;
        public bool SortDesc
        {
            get
            {
                return this.sortDesc;
            }
            set
            {
                if (this.sortDesc != value)
                {
                    this.sortDesc = value;
                    SortDescChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        private bool updateDpsDuringImport;
        public bool UpdateDpsDuringImport
        {
            get
            {
                return this.updateDpsDuringImport;
            }
            set
            {
                if (this.updateDpsDuringImport != value)
                {
                    this.updateDpsDuringImport = value;
                    UpdateDpsDuringImportChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        private bool endEncounterAfterWipe;
        public bool EndEncounterAfterWipe
        {
            get
            {
                return this.endEncounterAfterWipe;
            }
            set
            {
                if (this.endEncounterAfterWipe != value)
                {
                    this.endEncounterAfterWipe = value;
                    EndEncounterAfterWipeChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        private bool endEncounterOutOfCombat;
        public bool EndEncounterOutOfCombat
        {
            get
            {
                return this.endEncounterOutOfCombat;
            }
            set
            {
                if (this.endEncounterOutOfCombat != value)
                {
                    this.endEncounterOutOfCombat = value;
                    EndEncounterOutOfCombatChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        private bool logLines;
        public bool LogLines
        {
            get
            {
                return logLines;
            }
            set
            {
                if (this.logLines != value)
                {
                    this.logLines = value;
                    LogLinesChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        // Data that overlays can save/load via event handlers.
        public Dictionary<string, JToken> OverlayData = new Dictionary<string, JToken>();

        public BuiltinEventConfig()
        {
            this.updateInterval = 1;
            this.enmityIntervalMs = 100;
            this.sortKey = "encdps";
            this.sortDesc = true;
            this.updateDpsDuringImport = false;
            this.endEncounterAfterWipe = false;
            this.endEncounterOutOfCombat = false;
            this.logLines = false;
        }

        public static BuiltinEventConfig LoadConfig(IPluginConfig Config)
        {
            var result = new BuiltinEventConfig();

            if (Config.EventSourceConfigs.ContainsKey("MiniParse"))
            {
                var obj = Config.EventSourceConfigs["MiniParse"];
                
                if (obj.TryGetValue("UpdateInterval", out JToken value))
                {
                    result.updateInterval = value.ToObject<int>();
                }

                if (obj.TryGetValue("EnmityIntervalMs", out value))
                {
                    result.enmityIntervalMs = value.ToObject<int>();
                }

                if (obj.TryGetValue("SortKey", out value))
                {
                    result.sortKey = value.ToString();
                }

                if (obj.TryGetValue("SortDesc", out value))
                {
                    result.sortDesc = value.ToObject<bool>();
                }

                if (obj.TryGetValue("UpdateDpsDuringImport", out value))
                {
                    result.updateDpsDuringImport = value.ToObject<bool>();
                }

                if (obj.TryGetValue("EndEncounterAfterWipe", out value))
                {
                    result.endEncounterAfterWipe = value.ToObject<bool>();
                }

                if (obj.TryGetValue("EndEncounterOutOfCombat", out value))
                {
                    result.endEncounterOutOfCombat = value.ToObject<bool>();
                }

                if (obj.TryGetValue("OverlayData", out value))
                {
                    result.OverlayData = value.ToObject<Dictionary<string, JToken>>();

                    // Remove data for overlays that no longer exist.
                    var obsoleteKeys = new List<string>();
                    var overlayUUIDs = new List<string>();

                    foreach (var overlay in Config.Overlays)
                    {
                        if (overlay is Overlays.MiniParseOverlayConfig)
                        {
                            overlayUUIDs.Add(((Overlays.MiniParseOverlayConfig)overlay).Uuid.ToString());
                        }
                    }

                    foreach (var key in result.OverlayData.Keys)
                    {
                        if (!key.StartsWith("overlay#")) continue;

                        var uuid = key.Substring(8, key.LastIndexOf('#')-8);
                        if (!overlayUUIDs.Contains(uuid))
                        {
                            obsoleteKeys.Add(key);
                        }
                    }

                    foreach (var key in obsoleteKeys)
                    {
                        result.OverlayData.Remove(key);
                    }
                }

                if (obj.TryGetValue("LogLines", out value))
                {
                    result.logLines = value.ToObject<bool>();
                }
            }

            return result;
        }

        public void SaveConfig(IPluginConfig Config)
        {
            var newObj = JObject.FromObject(this);
            if (!Config.EventSourceConfigs.ContainsKey("MiniParse")
                || !JObject.DeepEquals(Config.EventSourceConfigs["MiniParse"], newObj))
            {
                Config.EventSourceConfigs["MiniParse"] = newObj;
                Config.MarkDirty();
            }
        }
    }

    public enum MiniParseSortType
    {
        None,
        StringAscending,
        StringDescending,
        NumericAscending,
        NumericDescending
    }
}
