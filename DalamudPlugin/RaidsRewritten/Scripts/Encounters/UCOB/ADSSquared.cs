using Dalamud.Plugin.Services;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class ADSSquared : Mechanic
{
    private struct MechanicInfoPair
    {
        public int NumADS;
        public float IntervalMilliseconds;
        public int LineGap;
    }

    private readonly Vector3 ArenaCenter = new(0, 0, 0);
    private const int ArenaRadius = 22;

    private const int BahamutPrimeWeather = 30;
    private const int DalamudDive = 9921;
    private const int NumDalamudDivesToSpawn = 2;
    private const int FlareBreath = 9940;
    private const int NumFlareBreathsToSpawn = 3;
    private const int FlareBreathTimeout = 5;
    private const int CalamitiousBlaze = 9939;
    private const int FellruinTrio = 9956;
    private const int TenstrikeTrio = 9958;
    private const int NumAdjacentToAvoid = 2;
    private const int AethericProfusion = 9905;
    private const float ADSReuseDelay = 2.6f;
    private readonly Dictionary<int, MechanicInfoPair> MechanicInfo = new Dictionary<int, MechanicInfoPair>
    {
        {
            0, new MechanicInfoPair
            {
                NumADS = 16,
                IntervalMilliseconds = 220f,
                LineGap = 3
            }
        },
        {
            1, new MechanicInfoPair
            {
                NumADS = 24,
                IntervalMilliseconds = 165f,
                LineGap = 4
            }
        },
        {
            2, new MechanicInfoPair
            {
                NumADS = 32,
                IntervalMilliseconds = 130f,
                LineGap = 5
            }
        }
    };

    public int RngSeed { get; set; }

    Random random = new Random();
    private readonly List<Entity> attacks = [];
    private readonly List<(int, Entity)> totalADS = [];
    private readonly List<(int, Entity)> availableADS = [];
    private DateTime lastProc = DateTime.MinValue;
    private int difficulty = 0;
    private bool stopCasting = false;
    private int flareBreathCounter = 0;
    private DateTime lastFlareBreath = DateTime.MinValue;
    private int dalamudDiveCounter = 0;
    private bool isLine = true;

    public override void Reset()
    {
        SoftReset();
        difficulty = 0;
        dalamudDiveCounter = 0;
        isLine = true;
    }

    private void SoftReset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
        totalADS.Clear();
        availableADS.Clear();
        lastProc = DateTime.MinValue;
        stopCasting = false;
        flareBreathCounter = 0;
        lastFlareBreath = DateTime.MinValue;
    }

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe ||
            a3 == DirectorUpdateCategory.Recommence)
        {
            Reset();
        }
    }

    public override void OnWeatherChange(byte weather)
    {
        // reset post-divebombs ADS
        if (weather == BahamutPrimeWeather) {
            stopCasting = true;
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        
        switch(set.Action.Value.RowId)
        {
            case DalamudDive:
                dalamudDiveCounter++;

                if (dalamudDiveCounter == NumDalamudDivesToSpawn)
                {
                    SpawnADSes();
                }

                break;
            case FlareBreath:
                flareBreathCounter++;
                lastFlareBreath = DateTime.Now;

                if (flareBreathCounter == NumFlareBreathsToSpawn)
                {
                    SpawnADSes();
                }

                break;
            case CalamitiousBlaze:
                SoftReset();
                difficulty = 1;
                isLine = false;
                break;
            case FellruinTrio:
                attacks.Add(DelayedAction.Create(World, () =>
                {
                    stopCasting = true;
                }, 5f));
                break;
            case AethericProfusion:
                SoftReset();
                difficulty = 2;
                break;
            case TenstrikeTrio:
                attacks.Add(DelayedAction.Create(World, () =>
                {
                    stopCasting = true;
                }, 5f));
                attacks.Add(DelayedAction.Create(World, SoftReset, 8f));
                break;
        }
    }

    public override void OnFrameworkUpdate(IFramework framework)
    {
        ProcessFlareBreathCounter();

        if (attacks.Count == 0) { return; }
        if (stopCasting) { return; }
        if ((DateTime.Now - lastProc).TotalMilliseconds < MechanicInfo[difficulty].IntervalMilliseconds) { return; }

        if (isLine)
        {
            ShootRandomADSLine();
        } else
        {
            ShootRandomADSCircle();
            // shoot 2 at once pre tenstrike
            if (difficulty == 2) { ShootRandomADSCircle(); }
        }

        if (difficulty == 2) { isLine = !isLine; }
        
        //Logger.Debug($"Avail: {availableADS.Count}");
    }

    private void SpawnADSes()
    {
        var seed = RngSeed;
        unchecked
        {
            seed += difficulty * 733;
        }
        random = new Random(seed);

        for (int i = 0; i < MechanicInfo[difficulty].NumADS; i++)
        {
            if (this.EntityManager.TryCreateEntity<ADS>(out var ads))
            {
                var angle = 2 * MathF.PI / MechanicInfo[difficulty].NumADS * i;
                var pos = new Vector3(
                    ArenaCenter.X - ArenaRadius * MathF.Sin(angle),
                    ArenaCenter.Y,
                    ArenaCenter.Z - ArenaRadius * MathF.Cos(angle)
                    );
                ads.Set(new Position(pos));

                attacks.Add(ads);
                totalADS.Add((i, ads));
                availableADS.Add((i, ads));
            }
        }
    }

    private void ShootRandomADSLine()
    {
        var sourcePair = availableADS[random.Next(availableADS.Count)];
        var sourceNum = sourcePair.Item1;
        var source = sourcePair.Item2;

        // avoid targeting self and adjacent (up to 2) ADSes 
        var numAvoid = NumAdjacentToAvoid * MechanicInfo[difficulty].LineGap + 1;
        var randNum = random.Next(totalADS.Count - numAvoid);
        if (randNum > sourceNum - 2) { randNum += numAvoid; }
        var targetPair = totalADS[randNum];
        var targetNum = targetPair.Item1;
        var target = targetPair.Item2;

        RemoveADSFromAvailablePool(sourcePair);

        if (source.TryGet<Position>(out var sourcePos) && target.TryGet<Position>(out var targetPos))
        {
            if (!ADS.CastLineAoe(source, MathUtilities.GetAbsoluteAngleFromSourceToTarget(sourcePos.Value, targetPos.Value)))
            {
                this.Logger.Debug("ADS tried to cast before it was ready");
            }
            lastProc = DateTime.Now;
        }
    }

    private void ShootRandomADSCircle()
    {
        var sourcePair = availableADS[random.Next(availableADS.Count)];
        var sourceNum = sourcePair.Item1;
        var source = sourcePair.Item2;

        var pos1 = RandomPointInArena();
        var pos2 = RandomPointInArena();

        RemoveADSFromAvailablePool(sourcePair);

        if (!ADS.CastSteppedLeader(source, pos1, pos2))
        {
            this.Logger.Debug("ADS tried to cast before it was ready");
        }
        lastProc = DateTime.Now;
    }

    private Vector3 RandomPointInArena()
    {
        var distanceFromCenter = ArenaRadius * MathF.Sqrt(random.NextSingle());
        var angle = random.NextSingle() * 2 * MathF.PI;
        var X = MathF.Cos(angle) * distanceFromCenter + ArenaCenter.X;
        var Z = MathF.Sin(angle) * distanceFromCenter + ArenaCenter.Z;
        return new Vector3(X, ArenaCenter.Y, Z);
    }

    private void RemoveADSFromAvailablePool((int, Entity) pair)
    {
        availableADS.Remove(pair);
        attacks.Add(DelayedAction.Create(World, () =>
        {
            availableADS.Add(pair);
        }, ADSReuseDelay));
    }

    private void ProcessFlareBreathCounter()
    {
        if (flareBreathCounter > 0 && (DateTime.Now - lastFlareBreath).TotalSeconds > FlareBreathTimeout)
        {
            flareBreathCounter = 0;
            lastFlareBreath = DateTime.MinValue;
        }
    }
}
