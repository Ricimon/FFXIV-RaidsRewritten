using Castle.DynamicProxy.Contributors;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Flecs.NET.Core;
using System.Numerics;
using System.Xml.Linq;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

internal class TankbusterAftershock : Mechanic
{
    private const uint PlummetId = 9896;
    private const float PlummetScale = 10f;
    private const string PlummetAftershockVfxPath = "vfx/monster/m0389/eff/m389sp_05c0n.avfx";

    private const uint FlareBreathId = 9940;
    private const float FlareBreathScale = 30f;
    private const string FlareBreathAftershockVfxPath = "vfx/monster/m0117/eff/baha_earth_90c0s.avfx";
    // vfx/monster/gimmick2/eff/z3oe_b3_g05c0i.avfx
    // vfx/monster/gimmick2/eff/z3oe_b3_g06c0i.avfx

    private const float InitialDelaySeconds = 1;
    private const float OmenVisibleSeconds = 0.4f;
    private const float VfxDelaySeconds = 0.4f;


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

        // make sure this has a valid value always
        string vfxPath = PlummetAftershockVfxPath;
        float aoeScale = PlummetScale;
        if (set.Action.Value.RowId == FlareBreathId)
        {
            vfxPath = FlareBreathAftershockVfxPath;
            aoeScale = FlareBreathScale;
        }

        var originalPosition = set.Source.Position;
        var originalRotation = set.Source.Rotation;

        List<Entity> ToDestruct = new();

        // create fake actor to keep vfxes in place if boss turns/moves
        if (this.AttackManager.TryCreateAttackEntity<FakeActor>(out var fakeActor))
        {
            fakeActor.Set(new Position(originalPosition));
            fakeActor.Set(new Rotation(originalRotation));
            fakeActor.Set(new Scale(new Vector3(1f)));
            ToDestruct.Add(fakeActor);
        } else
        {
            Reset(ToDestruct);
            return;
        }

        // show a quick telegraph
        void Telegraph()
        {
            if (this.AttackManager.TryCreateAttackEntity<FanOmen>(out var tempFanOmen))
            {
                tempFanOmen.Set(new Position(originalPosition));
                tempFanOmen.Set(new Rotation(originalRotation));
                tempFanOmen.Set(new Scale(new Vector3(aoeScale)));
                ToDestruct.Add(tempFanOmen);
            } else
            {
                Reset(ToDestruct);
                return;
            }

            void DestroyTelegraph()
            {
                ToDestruct.Remove(tempFanOmen);
                tempFanOmen.Destruct();
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
                Aftershock.Set(new Position(originalPosition));
                Aftershock.Set(new Rotation(originalRotation));
                Aftershock.Set(new Scale(new Vector3(aoeScale)));
                ToDestruct.Add(Aftershock);

                var delayedAction = DelayedAction.Create(this.World, () => fakeActor.Set(new ActorVfx(vfxPath)), VfxDelaySeconds);
                ToDestruct.Add(delayedAction);
            }
        }

        delayedAction = DelayedAction.Create(this.World, Aftershock, InitialDelaySeconds + OmenVisibleSeconds - 0.1f);
        ToDestruct.Add(delayedAction);

        delayedAction = DelayedAction.Create(this.World, () => Reset(ToDestruct), 3);
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
