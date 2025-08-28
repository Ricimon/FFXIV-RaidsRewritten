using Dalamud.Configuration;
using Dalamud.Plugin;
using NLog;
using System;
using System.Collections.Generic;

namespace RaidsRewritten
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public bool EverythingDisabled = false;

        public bool PunishmentImmunity = false;

        public Dictionary<string, string> EncounterSettings = [];

        // Saved UI inputs
        public bool PublicRoom { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public string RoomPassword { get; set; } = string.Empty;

        public int SelectedAudioOutputDeviceIndex { get; set; } = -1;

        public float MasterVolume { get; set; } = 2.0f;

        public bool PlayRoomJoinAndLeaveSounds { get; set; } = true;
        public bool KeybindsRequireGameFocus { get; set; }
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
