using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;

namespace RaidsRewritten.Scripts.Encounters.E1S;

public class DreadknightTest : Mechanic
{
    private enum CrowdControlType
    {
        Stun,
        Heavy,
        Sleep,
        Bind
    }

    private struct CrowdControlData
    {
        public CrowdControlType ccType;
        public float duration;
        public float effectiveness;
    }

    private readonly Dictionary<uint, CrowdControlData> CrowdControlDict = new()
    {
        {
            // shield bash
            16, new()
            {
                ccType = CrowdControlType.Stun,
                duration = 6,
            }
        },
        {
            // holy
            139, new()
            {
                ccType = CrowdControlType.Stun,
                duration = 4
            }
        },
        {
            // low blow
            7540, new()
            {
                ccType = CrowdControlType.Stun,
                duration = 5
            }
        },
        {
            // foot graze
            7553, new()
            {
                ccType = CrowdControlType.Bind,
                duration = 10
            }
        },
        {
            // leg graze
            7554, new()
            {
                ccType = CrowdControlType.Heavy,
                duration = 10,
                effectiveness = .4f
            }
        },
        {
            // leg sweep
            7863, new()
            {
                ccType = CrowdControlType.Stun,
                duration = 3
            }
        },
        {
            // repose
            16560, new()
            {
                ccType = CrowdControlType.Sleep,
                duration = 30
            }
        },
        {
            // sleep
            25880, new()
            {
                ccType = CrowdControlType.Sleep,
                duration = 30
            }
        }
    };

    private readonly Vector3 ArenaCenter = new(100,0,100);
    private const float BossId = 0x2495;
    private const string SwappableTetherVfx = "vfx/channeling/eff/chn_light01f.avfx";
    private const int SecondsUntilSwappable = 60;
    private const string StartMessage = "Twintania channels energy to the Dreadknight...";
    private const float CrowdControlDurationMultiplier = 0.5f;
    private const float CrowdControlEffectivenessMultiplier = 0.5f;

    private readonly List<uint> baitActionIds = [
        7538,  // interject
        7551,  // head graze
        7553,  // foot graze
        25880, // sleep
        16560, // repose
        16,    // shield bash
        139,   // holy
        7540,  // low blow
        7554,  // leg graze
        7863,  // leg sweep
    ];

    private Entity? dreadknight;

    private DateTime lastTargetSwap = DateTime.MinValue;
    private bool tetherVfxChanged = true;
    private Query<Sleep.Component>? sleepQuery;
    private Query<Bind.Component>? bindQuery;

    public override void Reset()
    {
        SoftReset();
    }

    private void SoftReset()
    {
        this.dreadknight?.Destruct();
        this.dreadknight = null;
        this.sleepQuery?.Dispose();
        this.sleepQuery = null;
        this.bindQuery?.Dispose();
        this.bindQuery = null;
        lastTargetSwap = DateTime.MinValue;
        tetherVfxChanged = true;
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

    public override void OnCombatStart()
    {
        SoftReset();

        SpawnDreadknight();
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (!dreadknight.HasValue) { return; }

        var isCancellingCC = Data.Actions.DamageActions.Contains(set.Action.Value.RowId) ||
            Data.Actions.AutoAttacks.Contains(set.Action.Value.RowId);
        var isTargetingBoss = set.Target?.BaseId == BossId;

        // don't want to keep looping over entity's children if not cancellable
        if (isCancellingCC && isTargetingBoss)
        {
            if (sleepQuery.HasValue && sleepQuery.Value.IsTrue() || bindQuery.HasValue && bindQuery.Value.IsTrue())
            {
                Dreadknight.RemoveCancellableCC(dreadknight.Value);
            }
        }

       if (baitActionIds.Contains(set.Action.Value.RowId) && isTargetingBoss) {
            var timeDiff = DateTime.UtcNow - lastTargetSwap;
            if (!(timeDiff.TotalSeconds < SecondsUntilSwappable && Dreadknight.HasTarget(dreadknight.Value)))
            {
                if (set.Source != null)
                {
                    Dreadknight.ApplyTarget(dreadknight.Value, set.Source);
                    lastTargetSwap = DateTime.UtcNow;
                    tetherVfxChanged = false;
                }
            }
        }

        if (CrowdControlDict.TryGetValue(set.Action.Value.RowId, out var ccData) && isTargetingBoss)
        {
            HandleCC(ccData);
        }
    }

    private void HandleCC(CrowdControlData ccData)
    {
        switch(ccData.ccType)
        {
            case CrowdControlType.Stun:
                Conditions.Stun.ApplyToTarget(dreadknight!.Value, ccData.duration * CrowdControlDurationMultiplier);
                break;
            case CrowdControlType.Heavy:
                Conditions.Heavy.ApplyToTarget(dreadknight!.Value, ccData.duration * CrowdControlDurationMultiplier);
                Dreadknight.SetTemporaryRelativeSpeed(
                    dreadknight!.Value,
                    1 - (ccData.effectiveness * CrowdControlEffectivenessMultiplier)
                );
                break;
            case CrowdControlType.Sleep:
                Conditions.Sleep.ApplyToTarget(dreadknight!.Value, ccData.duration * CrowdControlDurationMultiplier);
                break;
            case CrowdControlType.Bind:
                Conditions.Bind.ApplyToTarget(dreadknight!.Value, ccData.duration * CrowdControlDurationMultiplier);
                break;
        }
    }

    public override void OnFrameworkUpdate(IFramework framework)
    {
        if (dreadknight.HasValue)
        {
            if (!tetherVfxChanged && (DateTime.UtcNow - lastTargetSwap).TotalSeconds >= SecondsUntilSwappable)
            {
                Dreadknight.ChangeTetherVfx(dreadknight.Value, SwappableTetherVfx);
                tetherVfxChanged = true;
            }
        }
    }

    private void SpawnDreadknight()
    {
        if (this.EntityManager.TryCreateEntity<Dreadknight>(out var dreadknight))
        {
            Dalamud.ToastGui.ShowNormal(StartMessage);
            Dalamud.ChatGui.Print(StartMessage);
            dreadknight.Set(new Position(ArenaCenter));
            this.dreadknight = dreadknight;

            foreach (var obj in Dalamud.ObjectTable)
            {
                if (obj.BaseId == BossId)
                {
                    dreadknight.Set(new Dreadknight.BackupTarget(obj));
                    break;
                }
            }

            this.sleepQuery = World.QueryBuilder<Sleep.Component>().With(Ecs.ChildOf, dreadknight).Build();
            this.bindQuery = World.QueryBuilder<Bind.Component>().With(Ecs.ChildOf, dreadknight).Build();
        }
    }
}
