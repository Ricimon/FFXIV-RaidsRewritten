using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class SurfsUp : Mechanic
{
    private const uint SuperBlasstyChargeActionId = 19279;

    private readonly List<Entity> attacks = [];
    private int bitsSpawned = 0;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
        bitsSpawned = 0;
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Source == null) { return; }
        if (set.Action.Value.RowId != SuperBlasstyChargeActionId) { return; }

        if (EntityManager.TryCreateEntity<ArticulatedBit>(out var bit))
        {
            attacks.Add(bit);

            var targets = new List<IGameObject>();
            foreach (var target in set.TargetEffects)
            {
                var go = Dalamud.ObjectTable.SearchById(target.TargetID);
                if (go != null && go.ObjectKind == ObjectKind.Pc)
                {
                    targets.Add(go);
                }
            }
            bit
                .Set(new Position(set.Source.Position))
                .Set(new Rotation(set.Source.Rotation))
                .Set(new ArticulatedBit.Component(
                    bitsSpawned % 2 == 0 ? ArticulatedBit.ModelType.LeftHand : ArticulatedBit.ModelType.RightHand,
                    targets));
        }
    }
}
