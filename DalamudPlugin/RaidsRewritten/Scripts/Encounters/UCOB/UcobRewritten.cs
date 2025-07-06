using System.Collections.Generic;
using ImGuiNET;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public sealed class UcobRewritten : IEncounter
{
    public ushort TerritoryId => 733;

    public string Name => "UCOB Rewritten";

    // Config
    private string PermanentTwistersKey => $"{Name}.PermanentTwisters";

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
        bool permanentTwisters = true;
        if (this.configuration.EncounterSettings.TryGetValue(PermanentTwistersKey, out var i) &&
            i == 0)
        {
            permanentTwisters = false;
        }
        if (ImGui.Checkbox("Permanent Twisters", ref permanentTwisters))
        {
            this.configuration.EncounterSettings[PermanentTwistersKey] =
                permanentTwisters ? 1 : 0;
            this.configuration.Save();
            RefreshMechanics();
        }
    }

    private void RefreshMechanics()
    {
        this.mechanics.Clear();

        var permanentTwisters = true;
        if (this.configuration.EncounterSettings.TryGetValue(PermanentTwistersKey, out var i) &&
            i == 0)
        {
            permanentTwisters = false;
        }
        if (permanentTwisters)
        {
            this.mechanics.Add(mechanicFactory.Create<PermanentTwister>());
        }
        this.mechanics.Add(mechanicFactory.Create<TankbusterAftershock>());
    }
}
