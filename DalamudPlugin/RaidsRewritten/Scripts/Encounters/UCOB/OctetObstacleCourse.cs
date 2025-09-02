using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using Flecs.NET.Core;
using Lumina.Excel.Sheets;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class OctetObstacleCourse : Mechanic
{
    private const uint GrandOctetCastId = 9959;

    private readonly List<Entity> attacks = [];

    public override void Reset()
    {
        foreach(var attack in this.attacks)
        {
            attack.Destruct();
        }
        this.attacks.Clear();
    }

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe ||
            a3 == DirectorUpdateCategory.Recommence)
        {
            Reset();
        }
    }

    public override void OnCombatEnd()
    {
        Reset();
    }

    public override void OnStartingCast(Action action, IBattleChara source)
    {
        if (action.RowId == GrandOctetCastId)
        {
            // Mechanic setup
        }
    }
}
