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
    private string GreatWhirlwindRandomOffsetKey => $"{Name}.GreatWhirlwindRandomOffset";
    private string DownburstKey => $"{Name}.Downburst";
    private string GigastormCleansesKey => $"{Name}.GigastormCleanses";

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
            { GreatWhirlwindRandomOffsetKey, true },
            { DownburstKey, true },
            { GigastormCleansesKey, true },
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

        var rngSeedString = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
        int rngSeed = RandomUtilities.HashToRngSeed(rngSeedString);

        if (configuration.GetEncounterSetting(GreatWhirlwindStacksKey, this.defaultBoolSettings[GreatWhirlwindStacksKey]))
        {
            var greatWhirlwindStacks = mechanicFactory.Create<GreatWhirlwindStacks>();
            greatWhirlwindStacks.RngSeed = rngSeed;
            greatWhirlwindStacks.RandomTowerOffset = configuration.GetEncounterSetting(GreatWhirlwindRandomOffsetKey, this.defaultBoolSettings[GreatWhirlwindRandomOffsetKey]);
            this.mechanics.Add(greatWhirlwindStacks);
        }

        if (configuration.GetEncounterSetting(DownburstKey, this.defaultBoolSettings[DownburstKey]))
        {
            this.mechanics.Add(mechanicFactory.Create<Downburst>());
        }

        if (configuration.GetEncounterSetting(GigastormCleansesKey, this.defaultBoolSettings[GigastormCleansesKey]))
        {
            this.mechanics.Add(mechanicFactory.Create<GigastormCleanses>());
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
        ImGui.SetNextItemWidth(140);
        string rngSeedInput = configuration.GetEncounterSetting(RngSeedKey, string.Empty);
        if (ImGui.InputText("RNG Seed", ref rngSeedInput, 100))
        {
            configuration.EncounterSettings[RngSeedKey] = rngSeedInput;
            configuration.Save();
            RefreshMechanics();
        }

        bool greatWhirlwindStacks = configuration.GetEncounterSetting(GreatWhirlwindStacksKey, this.defaultBoolSettings[GreatWhirlwindStacksKey]);
        if (ImGui.Checkbox("Great Whirlwind Stacks", ref greatWhirlwindStacks))
        {
            configuration.EncounterSettings[GreatWhirlwindStacksKey] =
                greatWhirlwindStacks ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool randomTowerOffset = configuration.GetEncounterSetting(GreatWhirlwindRandomOffsetKey, this.defaultBoolSettings[GreatWhirlwindRandomOffsetKey]);
        if (ImGui.Checkbox("  Random Tower Positions", ref randomTowerOffset))
        {
            configuration.EncounterSettings[GreatWhirlwindRandomOffsetKey] =
                randomTowerOffset ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool downburst = configuration.GetEncounterSetting(DownburstKey, this.defaultBoolSettings[DownburstKey]);
        if (ImGui.Checkbox("Downburst Soaking", ref downburst))
        {
            configuration.EncounterSettings[DownburstKey] =
                downburst ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool gigastormCleanses = configuration.GetEncounterSetting(GigastormCleansesKey, this.defaultBoolSettings[GigastormCleansesKey]);
        if (ImGui.Checkbox("Gigastorm / Cleanses", ref gigastormCleanses))
        {
            configuration.EncounterSettings[GigastormCleansesKey] =
                gigastormCleanses ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }
    }
}
