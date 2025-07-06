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
    // vfx/monster/m0389/eff/m389sp_05c0n.avfx

    private const uint FlareBreathId = 9940;  //7381;
    // vfx/monster/m0117/eff/baha_earth_90c0s.avfx
    // vfx/monster/gimmick2/eff/z3oe_b3_g05c0i.avfx
    // vfx/monster/gimmick2/eff/z3oe_b3_g06c0i.avfx

    private const int AnimationDelayMs = 500;
    private const int SpawnDelayMs = 600;
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

        var ct = this.cts.Token;

        var originalPosition = set.Source.Position;
        var originalRotation = set.Source.Rotation;

        Entity? FanOmenVfx = null;

        if (this.AttackManager.TryCreateAttackEntity<FanOmen>(out var tempFanOmen))
        {
            tempFanOmen.Set(new Position(originalPosition));
            tempFanOmen.Set(new Rotation(originalRotation));
            tempFanOmen.Set(new Scale(new Vector3(30f)));
            FanOmenVfx = tempFanOmen;
        }

        try
        {
            await Task.Delay(SpawnDelayMs, ct).ConfigureAwait(true);
        } catch (OperationCanceledException) {
            if (FanOmenVfx != null) { FanOmenVfx.Value.Destruct(); }
            return;
        }


        if (this.AttackManager.TryCreateAttackEntity<Fan>(out var Aftershock))
        {
            Aftershock.Set(new Position(originalPosition));
            Aftershock.Set(new Rotation(originalRotation));
            Aftershock.Set(new Scale(new Vector3(30f)));

            if (FanOmenVfx != null) { FanOmenVfx.Value.Destruct(); }

            try
            {
                await Task.Delay(SpawnDelayMs, ct).ConfigureAwait(true);
            } catch (OperationCanceledException)
            {
                Aftershock.Destruct();
                return;
            }

            var vfx = Aftershock.CsWorld().Entity()
                .Set(new Position(originalPosition))
                .Set(new Rotation(originalRotation))
                .Set(new Scale(Vector3.One))
                .Set(new Vfx("vfx/omen/eff/general_1bf.avfx"));  // TODO: change this to earthshaker when actor spawning is hooked up

            try
            {
                await Task.Delay(SpawnDelayMs, ct).ConfigureAwait(true);
            } catch (OperationCanceledException)
            {
                Aftershock.Destruct();
                vfx.Destruct();
                return;
            }

            Aftershock.Destruct();
            vfx.Destruct();
        }
    }
}
