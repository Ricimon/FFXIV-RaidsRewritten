using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class Tethers : Mechanic
{
    private const int TwistingDive = 9906;
    private const int Heavensfall = 9911;
    private const int HeavensfallTrio = 9957;

    private readonly List<string> PunishmentVfx = ["vfx/lockon/eff/m0489trg_b0c.avfx", "vfx/monster/m0005/eff/m0005sp_15t0t.avfx"];
    private readonly List<string> CorrectVfx = ["vfx/lockon/eff/m0489trg_a0c.avfx"];

    private const float CloseTetherBreakpoint = 10f;
    private const float FarTetherBreakpoint = 30f;

    public int RngSeed { get; set; }

    private readonly List<Entity> attacks = [];
    private readonly List<Entity> tethers = [];
    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
        tethers.Clear();
    }

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe ||
            a3 == DirectorUpdateCategory.Recommence)
        {
            Reset();
        }
    }

    public override void OnStartingCast(Lumina.Excel.Sheets.Action action, IBattleChara source)
    {
        if (action.RowId == HeavensfallTrio)
        {
            List<IBattleChara> playerList = [];
            foreach (var player in this.Dalamud.ObjectTable.PlayerObjects)
            {
                playerList.Add(player);

            }
            if (playerList.Count != 8)
            {
                this.Logger.Debug($"uh oh, unexpected number of players: {playerList.Count}");
                return;
            }

            // ensure same order before randomizing list
            playerList.Sort((a, b) => {
                BattleChara aCs;
                BattleChara bCs;
                unsafe
                {
                    aCs = *(BattleChara*)a.Address;
                    bCs = *(BattleChara*)b.Address;
                }
                return aCs.ContentId.CompareTo(bCs.ContentId);
            });

            var random = new Random(RngSeed);
            playerList = [.. playerList.OrderBy(o => random.Next())];

            for (int i = 0; i < playerList.Count; i += 2)
            {
                var src = playerList[i];
                var target = playerList[i + 1];

                if (this.AttackManager.TryCreateAttackEntity<DistanceSnapshotTether>(out var tether))
                {
                    var onCondition1 = (Entity _) => { };

                    if (src == this.Dalamud.ClientState.LocalPlayer || target == this.Dalamud.ClientState.LocalPlayer)
                    {
                        onCondition1 = (e) => {
                            Stun.ApplyToPlayer(e, 15);
                        };
                    }

                    if (i < 4)
                    {
                        DistanceSnapshotTether.SetTetherVfx(tether, TetherOmen.TetherVfx.ActivatedClose)
                            .Set(new DistanceSnapshotTether.Tether(onCondition1))
                            .Set(new DistanceSnapshotTether.FailWhenFurtherThan(CloseTetherBreakpoint));
                    } else
                    {
                        DistanceSnapshotTether.SetTetherVfx(tether, TetherOmen.TetherVfx.ActivatedFar)
                            .Set(new DistanceSnapshotTether.Tether(onCondition1))
                            .Set(new DistanceSnapshotTether.FailWhenCloserThan(FarTetherBreakpoint));
                    }

                    tether.Set(new ActorVfxSource(src))
                        .Set(new ActorVfxTarget(target))
                        .Set(new DistanceSnapshotTether.VfxOnFail(PunishmentVfx))
                        .Set(new DistanceSnapshotTether.VfxOnSuccess(CorrectVfx));

                    attacks.Add(tether);
                    tethers.Add(tether);
                }
            }
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        switch (set.Action.Value.RowId)
        {
            case TwistingDive:
                attacks.Add(DelayedAction.Create(World,
                    () => {
                        foreach (var tether in tethers)
                        {
                            if (tether.IsValid()) {
                                tether.Add<DistanceSnapshotTether.Activated>();
                            }
                        }
                    }, 0.5f));
                break;
            case Heavensfall:
                Reset();
                break;
        }
    }
}
