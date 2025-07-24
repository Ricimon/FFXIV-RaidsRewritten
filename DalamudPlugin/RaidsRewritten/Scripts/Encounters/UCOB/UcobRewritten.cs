using System.Collections.Generic;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class UcobRewritten(Mechanic.Factory mechanicFactory, Configuration configuration) : IEncounter
{
    public ushort TerritoryId => 733;

    public string Name => "UCOB Rewritten";

    // Config
    private string PermanentTwistersKey => $"{Name}.PermanentTwisters";
    private string RollingBallKey => $"{Name}.RollingBall";
    private string RollingBallMaxBallsKey => $"{Name}.RollingBallMaxBalls";
    private string RollingBallRngSeedKey => $"{Name}.RollingBallRngSeed";
    private string TankbusterAftershockKey => $"{Name}.TankbusterAftershock";
    private string LightningCorridorKey => $"{Name}.LightningCorridor";

    private readonly List<Mechanic> mechanics = [];

    public IEnumerable<Mechanic> GetMechanics()
    {
        return this.mechanics;
    }

    public void RefreshMechanics()
    {
        this.mechanics.Clear();

        if (configuration.GetEncounterSetting(PermanentTwistersKey, true))
        {
            this.mechanics.Add(mechanicFactory.Create<PermanentTwister>());
        }

        if (configuration.GetEncounterSetting(RollingBallKey, true))
        {
            var rollingBall = mechanicFactory.Create<RollingBallOnNeurolink>();

            rollingBall.MaxBalls = configuration.GetEncounterSetting(RollingBallMaxBallsKey, 1);

            var seed = configuration.GetEncounterSetting(RollingBallRngSeedKey, string.Empty);
            rollingBall.RngSeed = RandomUtilities.HashToRngSeed(seed);

            this.mechanics.Add(rollingBall);
        }

        if (configuration.GetEncounterSetting(TankbusterAftershockKey, true))
        {
            this.mechanics.Add(mechanicFactory.Create<TankbusterAftershock>());
        }

        if (configuration.GetEncounterSetting(LightningCorridorKey, true))
        {
            this.mechanics.Add(mechanicFactory.Create<LightningCorridor>());
        }
    }

    public void Unload()
    {
        foreach(var mechanic in this.mechanics)
        {
            mechanic.Reset();
        }
        this.mechanics.Clear();
    }

    public void DrawConfig()
    {
        bool permanentTwisters = configuration.GetEncounterSetting(PermanentTwistersKey, true);
        if (ImGui.Checkbox("Permanent Twisters", ref permanentTwisters))
        {
            configuration.EncounterSettings[PermanentTwistersKey] =
                permanentTwisters ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool rollingBall = configuration.GetEncounterSetting(RollingBallKey, true);
        if (ImGui.Checkbox("Rolling Ball", ref rollingBall))
        {
            configuration.EncounterSettings[RollingBallKey] =
                rollingBall ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        using (ImRaii.PushIndent())
        using (ImRaii.Disabled(!rollingBall))
        {
            ImGui.PushItemWidth(120);
            int maxBalls = configuration.GetEncounterSetting(RollingBallMaxBallsKey, 1);
            if (ImGui.InputInt("Rolling Ball Max Balls", ref maxBalls))
            {
                configuration.EncounterSettings[RollingBallMaxBallsKey] = maxBalls.ToString();
                configuration.Save();
                RefreshMechanics();
            }
            ImGui.PopItemWidth();

            ImGui.PushItemWidth(120);
            string rollingBallRngSeed = configuration.GetEncounterSetting(RollingBallRngSeedKey, string.Empty);
            if (ImGui.InputText("Rolling Ball RNG Seed", ref rollingBallRngSeed, 100))
            {
                configuration.EncounterSettings[RollingBallRngSeedKey] = rollingBallRngSeed;
                configuration.Save();
                RefreshMechanics();
            }
            ImGui.PopItemWidth();
        }

        bool tankbusterAftershock = configuration.GetEncounterSetting(TankbusterAftershockKey, true);
        if (ImGui.Checkbox("Tankbuster Aftershocks", ref tankbusterAftershock))
        {
            configuration.EncounterSettings[TankbusterAftershockKey] =
                tankbusterAftershock ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }

        bool lightningCorridor = configuration.GetEncounterSetting(LightningCorridorKey, true);
        if (ImGui.Checkbox("Lightning Corridor", ref lightningCorridor))
        {
            configuration.EncounterSettings[LightningCorridorKey] =
                lightningCorridor ? bool.TrueString : bool.FalseString;
            configuration.Save();
            RefreshMechanics();
        }
    }
}
