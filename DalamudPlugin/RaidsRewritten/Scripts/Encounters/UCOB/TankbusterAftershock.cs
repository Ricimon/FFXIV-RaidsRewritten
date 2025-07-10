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
    private const string PlummetAftershockVfxPath = "vfx/monster/m0389/eff/m389sp_05c0n.avfx";

    private const uint FlareBreathId = 9940;  //7381;
    private const string FlareBreathAftershockVfxPath = "vfx/monster/m0117/eff/baha_earth_90c0s.avfx";
    // vfx/monster/gimmick2/eff/z3oe_b3_g05c0i.avfx
    // vfx/monster/gimmick2/eff/z3oe_b3_g06c0i.avfx

    private const int InitialDelayMs = 1000;
    private const int OmenVisibleMs = 500;
    private const int VfxDelayMs = 600;

    private List<Entity> ToDestruct = new();
    private CancellationTokenSource cts = new();

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe)
        {
            cts.Cancel();
            cts = new();
        }
    }

    public override async void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Source == null) { return; }
        if (set.Action.Value.RowId != FlareBreathId && set.Action.Value.RowId != PlummetId) { return; }

        // make sure this has a valid value always
        string vfxPath = PlummetAftershockVfxPath;
        if (set.Action.Value.RowId == FlareBreathId)
        {
            vfxPath = FlareBreathAftershockVfxPath;
        }

        var ct = this.cts.Token;

        var originalPosition = set.Source.Position;
        var originalRotation = set.Source.Rotation;

        if (this.AttackManager.TryCreateAttackEntity<FakeActor>(out var fakeActor))
        {
            fakeActor.Set(new Position(originalPosition));
            fakeActor.Set(new Rotation(originalRotation));  
            ToDestruct.Add(fakeActor);
        } else
        {
            Reset();
            return;
        }

        try
        {
            await Task.Delay(InitialDelayMs, ct).ConfigureAwait(true);
        } catch (OperationCanceledException)
        {
            Reset();
            return;
        }

        if (this.AttackManager.TryCreateAttackEntity<FanOmen>(out var tempFanOmen))
        {
            tempFanOmen.Set(new Position(originalPosition));
            tempFanOmen.Set(new Rotation(originalRotation));
            tempFanOmen.Set(new Scale(new Vector3(30f)));
            ToDestruct.Add(tempFanOmen);
        } else
        {
            Reset();
            return;
        }

        try
        {
            await Task.Delay(OmenVisibleMs, ct).ConfigureAwait(true);
        } catch (OperationCanceledException)
        {
            Reset();
            return;
        }

        ToDestruct.Remove(tempFanOmen);
        tempFanOmen.Destruct();

        if (this.AttackManager.TryCreateAttackEntity<Fan>(out var Aftershock))
        {
            Aftershock.Set(new Position(originalPosition));
            Aftershock.Set(new Rotation(originalRotation));
            Aftershock.Set(new Scale(new Vector3(30f)));
            ToDestruct.Add(Aftershock);

            try
            {
                await Task.Delay(VfxDelayMs, ct).ConfigureAwait(true);
            } catch (OperationCanceledException)
            {
                Reset();
                return;
            }

            fakeActor.Set(new ActorVfx(vfxPath));

            try
            {
                await Task.Delay(VfxDelayMs, ct).ConfigureAwait(true);
            } catch (OperationCanceledException)
            {
                Reset();
                return;
            }

            Reset();
        }
    }

    private void Reset()
    {
        foreach (var e in ToDestruct)
        {
            e.Destruct();
        }
        ToDestruct.Clear();
    }
}
