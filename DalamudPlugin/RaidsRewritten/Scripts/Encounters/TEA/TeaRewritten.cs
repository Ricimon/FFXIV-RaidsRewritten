using System.Collections.Generic;
using Dalamud.Bindings.ImGui;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class TeaRewritten : IEncounter
{
    public uint TerritoryId => 887;

    public string Name => "TEA Rewritten";

    // Config
    private string RngSeedKey => $"{Name}.RngSeed";
    private string FireTornadoKey => $"{Name}.FireTornado";

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

        if (configuration.GetEncounterSetting(FireTornadoKey, this.defaultBoolSettings[FireTornadoKey]))
        {
            this.mechanics.Add(mechanicFactory.Create<FireTornado>());
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

        bool fireTornado = configuration.GetEncounterSetting(FireTornadoKey, this.defaultBoolSettings[FireTornadoKey]);
        if (ImGui.Checkbox("Fire Tornado", ref fireTornado))
        {
            configuration.EncounterSettings[FireTornadoKey] =
                fireTornado ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }
    }

    private void ApplyIntendedFightSettings()
    {
        foreach(var setting in this.defaultBoolSettings)
        {
            configuration.EncounterSettings[setting.Key] = setting.Value.ToString();
        }

        foreach(var setting in this.defaultIntSettings)
        {
            configuration.EncounterSettings[setting.Key] = setting.Value.ToString();
        }

        configuration.Save();
        RefreshMechanics();
    }

    private void DisableEverything()
    {
        foreach(var setting in this.defaultBoolSettings)
        {
            configuration.EncounterSettings[setting.Key] = bool.FalseString;
        }

        configuration.Save();
        RefreshMechanics();
    }
}
