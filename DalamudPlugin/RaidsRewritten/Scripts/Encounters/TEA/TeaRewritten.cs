using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using RaidsRewritten.UI.Util;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class TeaRewritten : IEncounter
{
    public uint TerritoryId => 887;

    public string Name => "TEA Rewritten";

    // Config
    private string RngSeedKey => $"{Name}.RngSeed";
    private string FireTornadoKey => $"{Name}.FireTornado";
    private string PickyDollsKey => $"{Name}.PickyDolls";
    private string SurfsUpKey => $"{Name}.SurfsUp";

    private readonly Mechanic.Factory mechanicFactory;
    private readonly DalamudServices dalamud;
    private readonly Configuration configuration;

    private readonly List<Mechanic> mechanics = [];
    private readonly Dictionary<string, bool> defaultBoolSettings;
    private readonly Dictionary<string, int> defaultIntSettings;

    public TeaRewritten(Mechanic.Factory mechanicFactory, DalamudServices dalamud, Configuration configuration)
    {
        this.mechanicFactory = mechanicFactory;
        this.dalamud = dalamud;
        this.configuration = configuration;

        this.defaultBoolSettings = new()
        {
            { FireTornadoKey, true },
            { PickyDollsKey, true },
            { SurfsUpKey, true },
        };

        this.defaultIntSettings = new()
        {
        };
    }

    public IEnumerable<Mechanic> GetMechanics()
    {
        return mechanics;
    }

    public void RefreshMechanics()
    {
        Unload();

        var rngSeedString = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
        int rngSeed = RandomUtilities.HashToRngSeed(rngSeedString);

        if (configuration.GetEncounterSetting(FireTornadoKey, defaultBoolSettings[FireTornadoKey]))
        {
            mechanics.Add(mechanicFactory.Create<FireTornado>());
        }
        if (configuration.GetEncounterSetting(PickyDollsKey, defaultBoolSettings[PickyDollsKey]))
        {
            var pickyDolls = mechanicFactory.Create<PickyDolls>();
            pickyDolls.RngSeed = rngSeed;
            mechanics.Add(pickyDolls);
        }
        if (configuration.GetEncounterSetting(SurfsUpKey, defaultBoolSettings[SurfsUpKey]))
        {
            var surfsUp = mechanicFactory.Create<SurfsUp>();
            mechanics.Add(surfsUp);
        }
    }

    public void Unload()
    {
        foreach(var mechanic in mechanics)
        {
            mechanic.Reset();
        }
        mechanics.Clear();
    }

    public void IncrementRngSeed()
    {
        string rngSeed = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
        rngSeed = EncounterUtilities.IncrementRngSeed(rngSeed);
        configuration.EncounterSettings[RngSeedKey] = rngSeed;
        configuration.Save();
        this.dalamud.ChatGui.PrintSystemMessage($"RNG seed is now {rngSeed}", PluginInitializer.Name);
        RefreshMechanics();
    }

    public void DrawConfig()
    {
        if (ImGui.Button("Apply Intended Difficulty"))
        {
            ApplyIntendedFightSettings();
        }
        ImGui.SameLine();
        if (ImGui.Button("Disable Everything"))
        {
            DisableEverything();
        }

        ImGui.SetNextItemWidth(140);
        string rngSeed = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
        if (ImGui.InputText("RNG Seed", ref rngSeed, 100))
        {
            configuration.EncounterSettings[RngSeedKey] = rngSeed;
            configuration.Save();
            RefreshMechanics();
        }
        ImGui.SameLine();
        Common.HelpMarker("Make sure all players are on the same RNG seed");

        bool fireTornado = configuration.GetEncounterSetting(FireTornadoKey, defaultBoolSettings[FireTornadoKey]);
        if (ImGui.Checkbox("Fire Tornado", ref fireTornado))
        {
            configuration.EncounterSettings[FireTornadoKey] =
                fireTornado ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool pickyDolls = configuration.GetEncounterSetting(PickyDollsKey, defaultBoolSettings[PickyDollsKey]);
        if (ImGui.Checkbox("Picky Dolls", ref pickyDolls))
        {
            configuration.EncounterSettings[PickyDollsKey] =
                pickyDolls ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool surfsUp = configuration.GetEncounterSetting(SurfsUpKey, defaultBoolSettings[SurfsUpKey]);
        if (ImGui.Checkbox("Surfs Up!", ref surfsUp))
        {
            configuration.EncounterSettings[SurfsUpKey] =
                surfsUp ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }
    }

    private void ApplyIntendedFightSettings()
    {
        foreach(var setting in defaultBoolSettings)
        {
            configuration.EncounterSettings[setting.Key] = setting.Value.ToString();
        }

        foreach(var setting in defaultIntSettings)
        {
            configuration.EncounterSettings[setting.Key] = setting.Value.ToString();
        }

        configuration.Save();
        RefreshMechanics();
    }

    private void DisableEverything()
    {
        foreach(var setting in defaultBoolSettings)
        {
            configuration.EncounterSettings[setting.Key] = bool.FalseString;
        }

        configuration.Save();
        RefreshMechanics();
    }
}
