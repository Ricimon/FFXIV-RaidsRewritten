using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;
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
    private const string PunishmentVfx = "vfx/monster/m0005/eff/m0005sp_15t0t.avfx";
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

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        switch (set.Action.Value.RowId)
        {
            case HeavensfallTrio:
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
                    var source = playerList[i];
                    var target = playerList[i + 1];

                    if (this.AttackManager.TryCreateAttackEntity<DistanceTether>(out var tether))
                    {
                        var onCondition1 = (Entity _) => { };
                        var onCondition2 = () => { DistanceTether.RemoveTetherVfx(tether); };

                        if (source == this.Dalamud.ClientState.LocalPlayer || target == this.Dalamud.ClientState.LocalPlayer)
                        {
                            onCondition1 = (e) => {
                                Stun.ApplyToPlayer(e, 15);
                            };
                        }

                        if (i < 4)
                        {
                            DistanceTether.SetTetherVfx(tether, DistanceTether.TetherVfxes[DistanceTether.TetherVfx.DelayedClose])
                                .Set(new DistanceTether.Tether(CloseTetherFailCondition, onCondition1, onCondition2));
                        } else
                        {
                            DistanceTether.SetTetherVfx(tether, DistanceTether.TetherVfxes[DistanceTether.TetherVfx.DelayedFar])
                                .Set(new DistanceTether.Tether(FarTetherFailCondition, onCondition1, onCondition2));
                        }

                        tether.Set(new ActorVfxSource(source))
                            .Set(new ActorVfxTarget(target))
                            .Set(new DistanceTether.VfxOnCondition(PunishmentVfx));

                        attacks.Add(tether);
                        tethers.Add(tether);
                    }
                }
                break;
            case TwistingDive:
                for (int i = 0; i < tethers.Count; i++)
                {
                    var tether = tethers[i];
                    var vfx = DistanceTether.TetherVfxes[DistanceTether.TetherVfx.ActivatedClose];
                    if (i > 1) { vfx = DistanceTether.TetherVfxes[DistanceTether.TetherVfx.ActivatedFar]; }
                    DistanceTether.SetTetherVfx(tether, vfx);
                    tether.Add<DistanceTether.Activated>();
                    attacks.Add(DelayedAction.Create(World, () => { tether.Destruct(); }, 1));
                }
                break;
            case Heavensfall:
                Reset();
                break;
        }
    }

    private bool FarTetherFailCondition(float distance) => distance < 30;
    private bool CloseTetherFailCondition(float distance) => distance > 10;
}
