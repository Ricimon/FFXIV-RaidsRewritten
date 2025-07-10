using System.Collections.Generic;
using System.Linq;
using ImGuiNET;

namespace RaidsRewritten.Scripts.Encounters.E1S;

public class EdenPrimeTest : IEncounter
{
    public ushort TerritoryId => 853;

    public string Name => "Eden Prime Test";

    // Config
    private string RollingBallKey => $"{Name}.RollingBall";
    private string RollingBallRngSeedKey => $"{Name}.RollingBallRngSeed";

    private readonly Mechanic.Factory mechanicFactory;
    private readonly Configuration configuration;

    private readonly List<Mechanic> mechanics = [];

    public EdenPrimeTest(Mechanic.Factory mechanicFactory, Configuration configuration)
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
        bool rollingBall = this.configuration.GetEncounterSetting(RollingBallKey, true);
        if (ImGui.Checkbox("Rolling Ball", ref rollingBall))
        {
            this.configuration.EncounterSettings[RollingBallKey] =
                rollingBall ? bool.TrueString : bool.FalseString;
            this.configuration.Save();
            RefreshMechanics();
        }

        string rollingBallRngSeed = this.configuration.GetEncounterSetting(RollingBallRngSeedKey, string.Empty);
        if (ImGui.InputText("Rolling Ball RNG Seed", ref rollingBallRngSeed, 100))
        {
            this.configuration.EncounterSettings[RollingBallRngSeedKey] = rollingBallRngSeed;
            this.configuration.Save();
            RefreshMechanics();
        }
    }

    private void RefreshMechanics()
    {
        this.mechanics.Clear();

        this.mechanics.Add(mechanicFactory.Create<PermanentViceOfApathyTest>());

        if (this.configuration.GetEncounterSetting(RollingBallKey, true))
        {
            var rollingBall = mechanicFactory.Create<RollingBallOnViceOfApathy>();
            var seed = this.configuration.GetEncounterSetting(RollingBallRngSeedKey, string.Empty);
            rollingBall.RngSeed = seed.ToCharArray().Select(c => (int)c).Sum();
            this.mechanics.Add(rollingBall);
        }
    }
}
