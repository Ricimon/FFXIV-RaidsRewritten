using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class TankbusterAftershock : Mechanic
{
    private struct AftershockData
    {
        public float TelegraphScale;
        public int TelegraphDegrees;
        public string OmenPath;
        public string VfxPath;

        public float DelaySeconds;
        public float VfxDelaySeconds;
    }

    private readonly Dictionary<uint, AftershockData> AftershockDict = new Dictionary<uint, AftershockData>
    {
        {
            9940, new AftershockData
            {
                TelegraphDegrees = 90,
                TelegraphScale = 30f,
                OmenPath = "",
                VfxPath = "vfx/monster/m0117/eff/baha_earth_90c0s.avfx",
                DelaySeconds = 0.8f,
                VfxDelaySeconds = 0.4f
            }
        },
        {
            9896, new AftershockData
            {
                TelegraphDegrees = 120,
                TelegraphScale = 10f,
                OmenPath = "vfx/omen/eff/gl_fan120_1bf.avfx",
                VfxPath = "vfx/monster/m0389/eff/m389sp_05c0n.avfx",
                DelaySeconds = 0.5f,
                VfxDelaySeconds = 0.5f
            }
        }
    };

    private const float TurnDelaySeconds = 0.5f;
    private const float StunDurationSeconds = 10f;
    private const float OmenVisibleSeconds = 0.4f;
    private const float StatusDelaySeconds = 0.5f;

    private readonly List<List<Entity>> attacks = [];

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe)
        {
            foreach (var ToDestruct in attacks)
            {
                Reset(ToDestruct);
            }
            attacks.Clear();
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Source == null) { return; }
        if (!AftershockDict.TryGetValue(set.Action.Value.RowId, out var Aftershock)) { return; }


        List<Entity> ToDestruct = [];
        void execAttack() => ExecuteAttack(Aftershock, set.Source, ToDestruct);
        var delayedAction = DelayedAction.Create(this.World, execAttack, TurnDelaySeconds);
        ToDestruct.Add(delayedAction);
        attacks.Add(ToDestruct);
    }

    private void ExecuteAttack(AftershockData aftershockData, IGameObject source, List<Entity> ToDestruct)
    {
        var originalPosition = source.Position;
        var originalRotation = source.Rotation;

        // create fake actor to keep vfxes in place if boss turns/moves
        var FakeActor = Attacks.Components.FakeActor.Create(this.World)
            .Set(new Position(originalPosition))
            .Set(new Rotation(originalRotation))
            .Set(new Scale(new Vector3(1f)));
        ToDestruct.Add(FakeActor);

        // show a quick telegraph
        void Telegraph()
        {
            if (this.AttackManager.TryCreateAttackEntity<FanOmen>(out var FanOmen))
            {
                FanOmen.Set(new Position(originalPosition))
                       .Set(new Rotation(originalRotation))
                       .Set(new Scale(new Vector3(aftershockData.TelegraphScale)));

                if (!String.IsNullOrEmpty(aftershockData.OmenPath))
                {
                    FanOmen.Set(new StaticVfx(aftershockData.OmenPath));
                }

                ToDestruct.Add(FanOmen);
            } else
            {
                Reset(ToDestruct);
                return;
            }

            void DestroyTelegraph()
            {
                ToDestruct.Remove(FanOmen);
                FanOmen.Destruct();
            }

            var delayedAction = DelayedAction.Create(this.World, DestroyTelegraph, OmenVisibleSeconds);
        }

        var delayedAction = DelayedAction.Create(this.World, Telegraph, aftershockData.DelaySeconds);
        ToDestruct.Add(delayedAction);


        // check if player is in telegraph
        void Aftershock()
        {
            if (this.AttackManager.TryCreateAttackEntity<Fan>(out var AftershockAoE))
            {
                void OnHit(Entity e)
                {
                    DelayedAction.Create(e.CsWorld(), () => Bound.ApplyToPlayer(e, StunDurationSeconds), StatusDelaySeconds);
                }

                AftershockAoE.Set(new Position(originalPosition))
                          .Set(new Rotation(originalRotation))
                          .Set(new Scale(new Vector3(aftershockData.TelegraphScale)))
                          .Set(new Fan.Component(OnHit, aftershockData.TelegraphDegrees));

                ToDestruct.Add(AftershockAoE);

                var delayedAction = DelayedAction.Create(this.World, () => FakeActor.Set(new ActorVfx(aftershockData.VfxPath)), aftershockData.VfxDelaySeconds);
                ToDestruct.Add(delayedAction);
            }
        }

        delayedAction = DelayedAction.Create(this.World, Aftershock, aftershockData.DelaySeconds + OmenVisibleSeconds - 0.1f);
        ToDestruct.Add(delayedAction);

        delayedAction = DelayedAction.Create(this.World, () => Reset(ToDestruct), 5);
        ToDestruct.Add(delayedAction);
    }

    private void Reset(List<Entity> ToDestruct)
    {
        foreach (var e in ToDestruct)
        {
            e.Destruct();
        }
        ToDestruct.Clear();
        attacks.Remove(ToDestruct);
    }
}
