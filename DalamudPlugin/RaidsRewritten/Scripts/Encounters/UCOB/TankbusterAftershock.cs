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

    private const uint FlareBreathId = 9940;
    private const float FlareBreathScale = 30f;
    private const int FlareBreathConeDeg = 90;
    private const string FlareBreathAftershockVfxPath = "vfx/monster/m0117/eff/baha_earth_90c0s.avfx";
    // vfx/monster/gimmick2/eff/z3oe_b3_g05c0i.avfx
    // vfx/monster/gimmick2/eff/z3oe_b3_g06c0i.avfx

    private const uint PlummetId = 9896;
    private const float PlummetScale = 10f;
    private const int PlummetConeDeg = 120;
    private const string PlummetOmenPath = "vfx/omen/eff/gl_fan120_1bf.avfx";
    private const string PlummetAftershockVfxPath = "vfx/monster/m0389/eff/m389sp_05c0n.avfx";

    private const float StunDurationSeconds = 10f;

    private const float InitialDelaySeconds = 1;
    private const float OmenVisibleSeconds = 0.4f;
    private const float VfxDelaySeconds = 0.4f;
    private const float StatusDelaySeconds = 0.5f;

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe)
        {
            // TODO: Cancel on wipe
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Source == null) { return; }
        if (set.Action.Value.RowId != FlareBreathId && set.Action.Value.RowId != PlummetId) { return; }

        ExecuteAttack(set.Action.Value.RowId == PlummetId, set.Source.Position, set.Source.Rotation);
    }

    public void ExecuteAttack(bool isPlummet, Vector3 position, float rotation)
    {
        // make sure this has a valid value always
        var omenPath = "";
        var vfxPath = FlareBreathAftershockVfxPath;
        var aoeScale = FlareBreathScale;
        var coneDeg = FlareBreathConeDeg;

        if (isPlummet)
        {
            omenPath = PlummetOmenPath;
            vfxPath = PlummetAftershockVfxPath;
            aoeScale = PlummetScale;
            coneDeg = PlummetConeDeg;
        }

        var originalPosition = position;
        var originalRotation = rotation;

        List<Entity> ToDestruct = new();

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
                       .Set(new Scale(new Vector3(aoeScale)));

                if (!String.IsNullOrEmpty(omenPath))
                {
                    FanOmen.Set(new StaticVfx("vfx/omen/eff/gl_fan120_1bf.avfx"));
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

        var delayedAction = DelayedAction.Create(this.World, Telegraph, InitialDelaySeconds);
        ToDestruct.Add(delayedAction);


        // check if player is in telegraph
        void Aftershock()
        {
            if (this.AttackManager.TryCreateAttackEntity<Fan>(out var Aftershock))
            {
                void OnHit(Entity e)
                {
                    DelayedAction.Create(e.CsWorld(), () => Bound.ApplyToPlayer(e, StunDurationSeconds), StatusDelaySeconds);
                }

                Aftershock.Set(new Position(originalPosition))
                          .Set(new Rotation(originalRotation))
                          .Set(new Scale(new Vector3(aoeScale)))
                          .Set(new Fan.Component(OnHit, coneDeg));

                ToDestruct.Add(Aftershock);

                var delayedAction = DelayedAction.Create(this.World, () => FakeActor.Set(new ActorVfx(vfxPath)), VfxDelaySeconds);
                ToDestruct.Add(delayedAction);
            }
        }

        delayedAction = DelayedAction.Create(this.World, Aftershock, InitialDelaySeconds + OmenVisibleSeconds - 0.1f);
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
    }
}
