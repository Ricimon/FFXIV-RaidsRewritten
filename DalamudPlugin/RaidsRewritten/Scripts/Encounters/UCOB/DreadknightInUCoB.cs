using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class DreadknightInUCoB : Mechanic
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

    private const uint NeurolinkDataId = 0x1E88FF;
    private readonly Vector3 ArenaCenter = new(0,0,0);
    private const int GenerateId = 9902;
    private const int HatchId = 9903;
    private const float BaseSpeedIncrement = 0.5f;
    private const float TwintaniaId = 0x1FDF;
    private const byte AddsWeather = 31;
    private const float AddsDreadknightSpawnDelay = 10f;
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
    private readonly List<Entity> attacks = [];

    private int neurolinksSpawned = 0;
    private int oviformsOnField = 0;
    private DateTime lastTargetSwap = DateTime.MinValue;
    private bool tetherVfxChanged = true;
    private bool ccCancellable = true;

    public override void Reset()
    {
        SoftReset();
        neurolinksSpawned = 0;
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
    }

    private void SoftReset()
    {
        this.dreadknight?.Destruct();
        this.dreadknight = null;
        oviformsOnField = 0;
        lastTargetSwap = DateTime.MinValue;
        tetherVfxChanged = true;
        ccCancellable = false;
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
        if (dreadknight.HasValue)
        {
            dreadknight.Value.Destruct();
            dreadknight = null;
        }

        SpawnDreadknight();
    }

    public override void OnWeatherChange(byte weather)
    {
        if (weather == AddsWeather)
        {
            attacks.Add(DelayedAction.Create(World, SpawnDreadknight, AddsDreadknightSpawnDelay));
        }
    }

    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
        if (newObject.DataId != NeurolinkDataId) { return; }

        neurolinksSpawned++;

        if (neurolinksSpawned > 2)
        {
            SoftReset();
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (!dreadknight.HasValue) { return; }

        var isCancellingCC = Data.Actions.DamageActions.Contains(set.Action.Value.RowId) ||
            Data.Actions.AutoAttacks.Contains(set.Action.Value.RowId);
        var isTargetingTwintania = set.Target?.DataId == TwintaniaId;

        // don't want to keep looping over entity's children if not cancellable
        if (ccCancellable && isCancellingCC && isTargetingTwintania)
        {
            Dreadknight.RemoveCancellableCC(dreadknight.Value);
            ccCancellable = false;
        }

        if (set.Action.Value.RowId == GenerateId)
        {
            oviformsOnField = neurolinksSpawned;
            return;
        } else if (set.Action.Value.RowId == HatchId) {
            oviformsOnField--;
            if (oviformsOnField <= 0 && dreadknight.HasValue)
            {
                var speedIncrement = BaseSpeedIncrement;
                if (neurolinksSpawned >= 3)
                {
                    speedIncrement *= 2;
                }
                Dreadknight.IncrementSpeed(dreadknight.Value, speedIncrement);
            }
            return;
        } else if (baitActionIds.Contains(set.Action.Value.RowId) && isTargetingTwintania) {
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

        if (CrowdControlDict.TryGetValue(set.Action.Value.RowId, out var ccData) && isTargetingTwintania)
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
                ccCancellable = true;
                break;
            case CrowdControlType.Bind:
                Conditions.Bind.ApplyToTarget(dreadknight!.Value, ccData.duration * CrowdControlDurationMultiplier);
                ccCancellable = true;
                break;
        }
    }

    public override void OnVFXSpawn(IGameObject? target, string vfxPath)
    {
        if (vfxPath.Equals("vfx/channeling/eff/chn_kosoku1f.avfx"))
        {
            Reset();
        }
    }

    public override void OnFrameworkUpdate(IFramework framework)
    {
        if (dreadknight.HasValue && !tetherVfxChanged && (DateTime.UtcNow - lastTargetSwap).TotalSeconds > SecondsUntilSwappable)
        {
            Dreadknight.ChangeTetherVfx(dreadknight.Value, SwappableTetherVfx);
            tetherVfxChanged = true;
        }
    }

    private void SpawnDreadknight()
    {
        if (this.AttackManager.TryCreateAttackEntity<Dreadknight>(out var dreadknight))
        {
            Dalamud.ToastGui.ShowNormal(StartMessage);
            Dalamud.ChatGui.Print(StartMessage);
            dreadknight.Set(new Position(ArenaCenter));
            this.dreadknight = dreadknight;

            foreach (var obj in Dalamud.ObjectTable)
            {
                if (obj.DataId == TwintaniaId)
                {
                    dreadknight.Set(new Dreadknight.BackupTarget(obj));
                    break;
                }
            }
        }
    }
}
