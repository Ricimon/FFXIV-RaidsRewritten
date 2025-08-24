using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class ExpandingEarthshakerPuddles : Mechanic
{
    public const string VfxPath = "bgcommon/world/common/vfx_for_btl/b0801/eff/b0801_yuka_o.avfx";

    private const uint EarthShakerPuddleDataId = 0x1E9663;

    private readonly List<Entity> attacks = [];

    public override void Reset()
    {
        foreach (var attack in this.attacks)
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

    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
        if (newObject.DataId != EarthShakerPuddleDataId) { return; }

        if (this.AttackManager.TryCreateAttackEntity<ExpandingPuddle>(out var puddle))
        {
            puddle.Set(new Position(newObject.Position));
            puddle.Set(new Rotation(newObject.Rotation));
            puddle.Set(new ExpandingPuddle.Component(
                VfxPath,
                StartScale: 0.5f,
                EndScale: 5.0f,
                ExpandSpeed: 0.25f,
                Lifetime: 22.0f));
            attacks.Add(puddle);
        }
    }
}
