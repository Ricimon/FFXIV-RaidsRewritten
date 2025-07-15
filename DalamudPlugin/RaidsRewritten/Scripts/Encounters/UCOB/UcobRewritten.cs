using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public sealed class UcobRewritten : IEncounter
{
    public ushort TerritoryId => 733;

    public string Name => "UCOB Rewritten";

    // Config
    private string PermanentTwistersKey => $"{Name}.PermanentTwisters";
    private string RollingBallKey => $"{Name}.RollingBall";
    private string RollingBallMaxBallsKey => $"{Name}.RollingBallMaxBalls";
    private string RollingBallRngSeedKey => $"{Name}.RollingBallRngSeed";
    private string TankbusterAftershockKey => $"{Name}.TankbusterAftershock";

    private readonly Mechanic.Factory mechanicFactory;
    private readonly Configuration configuration;

    private readonly List<Mechanic> mechanics = [];

    public UcobRewritten(Mechanic.Factory mechanicFactory, Configuration configuration)
    {
        this.mechanicFactory = mechanicFactory;
        this.configuration = configuration;
        RefreshMechanics();
    }

    public IEnumerable<Mechanic> GetMechanics()
    {
        return this.mechanics;
    }

    public void DrawConfig()
    {
        bool permanentTwisters = this.configuration.GetEncounterSetting(PermanentTwistersKey, true);
        if (ImGui.Checkbox("Permanent Twisters", ref permanentTwisters))
        {
            this.configuration.EncounterSettings[PermanentTwistersKey] =
                permanentTwisters ? bool.TrueString : bool.FalseString;
            this.configuration.Save();
            RefreshMechanics();
        }

        bool rollingBall = this.configuration.GetEncounterSetting(RollingBallKey, true);
        if (ImGui.Checkbox("Rolling Ball", ref rollingBall))
        {
            this.configuration.EncounterSettings[RollingBallKey] =
                rollingBall ? bool.TrueString : bool.FalseString;
            this.configuration.Save();
            RefreshMechanics();
        }

        int maxBalls = this.configuration.GetEncounterSetting(RollingBallMaxBallsKey, 1);
        if (ImGui.InputInt("Rolling Ball Max Balls", ref maxBalls))
        {
            this.configuration.EncounterSettings[RollingBallMaxBallsKey] = maxBalls.ToString();
            this.configuration.Save();
            RefreshMechanics();
        }

        string rollingBallRngSeed = this.configuration.GetEncounterSetting(RollingBallRngSeedKey, string.Empty);
        ImGui.PushItemWidth(150);
        if (ImGui.InputText("Rolling Ball RNG Seed", ref rollingBallRngSeed, 100))
        {
            this.configuration.EncounterSettings[RollingBallRngSeedKey] = rollingBallRngSeed;
            this.configuration.Save();
            RefreshMechanics();
        }

        bool tankbusterAftershock = this.configuration.GetEncounterSetting(TankbusterAftershockKey, true);
        if (ImGui.Checkbox("Tankbuster Aftershocks", ref tankbusterAftershock))
        {
            this.configuration.EncounterSettings[TankbusterAftershockKey] =
                tankbusterAftershock ? bool.TrueString : bool.FalseString;
            this.configuration.Save();
            RefreshMechanics();
        }
        
        ImGui.PopItemWidth();
    }

    private void RefreshMechanics()
    {
        this.mechanics.Clear();

        if (this.configuration.GetEncounterSetting(PermanentTwistersKey, true))
        {
            this.mechanics.Add(mechanicFactory.Create<PermanentTwister>());
        }

        if (this.configuration.GetEncounterSetting(RollingBallKey, true))
        {
            var rollingBall = mechanicFactory.Create<RollingBallOnNeurolink>();

            rollingBall.MaxBalls = this.configuration.GetEncounterSetting(RollingBallMaxBallsKey, 1);

            var seed = this.configuration.GetEncounterSetting(RollingBallRngSeedKey, string.Empty);
            rollingBall.RngSeed = RandomUtilities.HashToRngSeed(seed);

            this.mechanics.Add(rollingBall);
        }

        if (this.configuration.GetEncounterSetting(TankbusterAftershockKey, true))
        {
            this.mechanics.Add(mechanicFactory.Create<TankbusterAftershock>());
        }
    }
}
