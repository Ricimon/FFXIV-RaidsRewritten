using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Plugin;
using NLog;
using RaidsRewritten.UI.Util;

namespace RaidsRewritten
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public bool EverythingDisabled { get; set; } = false;

        public Vector3 StatusTextColor { get; set; } = Vector4Colors.Red.AsVector3();

        public string ServerUrl { get; set; } = string.Empty;
        public bool UseCustomPartyId { get; set; }
        public string CustomPartyId { get; set; } = string.Empty;

        public Dictionary<string, string> EncounterSettings = [];

        public bool PunishmentImmunity { get; set; } = false;

        public bool PrintLogsToChat { get; set; }

        public int EffectsRendererPositionX { get; set; } = 0;
        public int EffectsRendererPositionY { get; set; } = 0;

        public int MinimumVisibleLogLevel { get; set; } = LogLevel.Info.Ordinal;

        // the below exist just to make saving less cumbersome
        [NonSerialized]
        private IDalamudPluginInterface? pluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface!.SavePluginConfig(this);
        }

        public bool GetEncounterSetting(string key, bool defaultValue)
        {
            if (EncounterSettings.TryGetValue(key, out var s) && bool.TryParse(s, out var b))
            {
                return b;
            }
            return defaultValue;
        }

        public int GetEncounterSetting(string key, int defaultValue)
        {
            if (EncounterSettings.TryGetValue(key, out var s) && int.TryParse(s, out var i))
            {
                return i;
            }
            return defaultValue;
        }

        public string GetEncounterSetting(string key, string defaultValue)
        {
            if (EncounterSettings.TryGetValue(key, out var s))
            {
                return s;
            }
            return defaultValue;
        }
    }
}
