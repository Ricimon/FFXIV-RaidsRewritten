﻿using Dalamud.Configuration;
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

        public Dictionary<string, int> EncounterSettings = [];

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
    }
}
