using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Encounters;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.UWU;

public class UwuRewritten : IEncounter
{
    public ushort TerritoryId => 777;

    public string Name => "UWU Rewritten";

    // Config
    private string RngSeedKey => $"{Name}.RngSeed";
    private string GreatWhirlwindStacksKey => $"{Name}.GreatWhirlwindStacks";

    private readonly Mechanic.Factory mechanicFactory;
    private readonly DalamudServices dalamud;
    private readonly Configuration configuration;
    private readonly EcsContainer ecsContainer;

    private readonly List<Mechanic> mechanics = [];
    private readonly Dictionary<string, bool> defaultBoolSettings;

    public UwuRewritten(Mechanic.Factory mechanicFactory, DalamudServices dalamud, Configuration configuration, EcsContainer ecsContainer)
    {
        this.mechanicFactory = mechanicFactory;
        this.dalamud = dalamud;
        this.configuration = configuration;
        this.ecsContainer = ecsContainer;

        this.defaultBoolSettings = new()
        {
            { GreatWhirlwindStacksKey, true },
        };
    }

    public IEnumerable<Mechanic> GetMechanics()
    {
        return this.mechanics;
    }

    public void RefreshMechanics()
    {
        foreach (var mechanic in this.mechanics)
        {
            mechanic.Reset();
        }
        this.mechanics.Clear();

        if (configuration.GetEncounterSetting(GreatWhirlwindStacksKey, this.defaultBoolSettings[GreatWhirlwindStacksKey]))
        {
            this.mechanics.Add(mechanicFactory.Create<GreatWhirlwindStacks>());
        }
    }

    public void Unload()
    {
        foreach (var mechanic in this.mechanics)
        {
            mechanic.Reset();
        }
        this.mechanics.Clear();
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
        bool greatWhirlwindStacks = configuration.GetEncounterSetting(GreatWhirlwindStacksKey, this.defaultBoolSettings[GreatWhirlwindStacksKey]);
        if (ImGui.Checkbox("Great Whirlwind Stacks", ref greatWhirlwindStacks))
        {
            configuration.EncounterSettings[GreatWhirlwindStacksKey] =
                greatWhirlwindStacks ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }
    }
}
