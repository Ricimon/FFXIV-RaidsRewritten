using System;
using System.Collections.Generic;
using ECommons;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using static RaidsRewritten.Scripts.Attacks.LastWish;
using Dalamud.Game.ClientState.Party;
using RaidsRewritten.Scripts.Attacks;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

internal class LastWish : Mechanic
{
    public enum Phase
    {
        Start,
        Telegraph,
        Resolution
    }
    /*
    private static readonly Dictionary<uint, Phase> HookedActions = new()
    {
        { 9970, Phase.Telegraph }, //Pheonix

    };
    */
    //Debug Hooks
    private static readonly Dictionary<uint, Phase> HookedActions = new()
    {
        { 9898, Phase.Start }, //Twister
        { 9896, Phase.Telegraph }, //plummet
        { 9922, Phase.Resolution }, // Bahamut Favor

    };
    private readonly List<Entity> attacks = [];

    public int RngSeed { get; set; }
    private Random? random;
    HitZone[,]? SolutionTable;
    List<(HitZone, HitZone)>? TrueSolutionZones;
    List<(int, int)>? Pairs;
    private const float TELEGRAPH_DELAY = 1.5f;
    private int PlayerID;
    public override void Reset()
    {
        foreach (var attack in this.attacks)
        {
            attack.Destruct();
        }
        this.attacks.Clear();
    }

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe ||
            a3 == DirectorUpdateCategory.Recommence)
        {
            Reset();
        }
    }

    public void ForceSim(int phase)
    {
        switch (phase)
        {
            case 0:
                var seed = RngSeed;
                random = new Random(seed);
                GenerateTable(8, 4);
                GenerateSolution();
                Shuffle(random, SymbolPaths);
                DebugOutput();
                break;
            case 1:

                break;
            case 2:
                break;
        }
    }
    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Target == null) { return; }
        if (!HookedActions.TryGetValue(set.Action.Value.RowId, out var phase)) { return; }
        switch (phase)
        {
            case Phase.Start:
                Logger.Info("Starting Start");
                var seed = RngSeed;
                random = new Random(seed);
                GenerateTable(8, 4);
                GenerateSolution();
                Shuffle(random, SymbolPaths);
                DebugOutput();
                PlayerID = 0;
                break;
            case Phase.Telegraph:
                Logger.Info("Starting Telegraph");
                if (SolutionTable == null || Pairs == null || TrueSolutionZones == null) { return; }
                
                for (int i = 0; i < SolutionTable.GetLength(1); i++)
                {
                    int iteration = i;
                    var da = DelayedAction.Create(this.World, () =>
                    {
                        if (this.AttackManager.TryCreateAttackEntity<Scripts.Attacks.LastWish>(out var wish))
                        {
                            wish.Set(new Scripts.Attacks.LastWish.Component(SolutionTable[PlayerID,iteration], true));
                        }
                    }, (TELEGRAPH_DELAY + Scripts.Attacks.LastWish.OMEN_VISIBLE_SECONDS)*i) ;
                    this.attacks.Add(da);
                }
                break;
            case Phase.Resolution:
                Logger.Info("Starting Resolution");
                if (SolutionTable == null || Pairs == null || TrueSolutionZones == null) { return; }

                int count = 0;
                TrueSolutionZones.Each(a =>
                {
                    int iteration = count;
                    var da = DelayedAction.Create(this.World, () =>
                    {
                        if (this.AttackManager.TryCreateAttackEntity<Scripts.Attacks.LastWish>(out var wish))
                        {
                            wish.Set(new Scripts.Attacks.LastWish.Component(a.Item1));
                        }
                    }, (TELEGRAPH_DELAY + Scripts.Attacks.LastWish.OMEN_VISIBLE_SECONDS) * iteration);
                    this.attacks.Add(da);

                    da = DelayedAction.Create(this.World, () =>
                    {
                        if (this.AttackManager.TryCreateAttackEntity<Scripts.Attacks.LastWish>(out var wish))
                        {
                            wish.Set(new Scripts.Attacks.LastWish.Component(a.Item2));
                        }
                    }, (TELEGRAPH_DELAY + Scripts.Attacks.LastWish.OMEN_VISIBLE_SECONDS) * iteration);
                    this.attacks.Add(da);
                    count++;
                }); 
                break;
        }
    }

    private static readonly Dictionary<HitZone, HitZone> Opposites = new()
    {
        { HitZone.In, HitZone.Out },
        { HitZone.Out, HitZone.In },
        { HitZone.North, HitZone.South },
        { HitZone.South, HitZone.North },
        { HitZone.East, HitZone.West },
        { HitZone.West, HitZone.East },
    };

    private static List<string> SymbolPaths = new List<string> {
        "0",
        "1",
        "2",
        "3",
        "4",
        "5",
        "6",
        "7",
        "8",
        "9",
        "10",
        "11"
    };


    #region Algorithm
    private void GenerateTable(int Players, int Sequences)
    {
        if (Players < Enum.GetValues<HitZone>().Length) { return; } //unsolveable random distribution
        var zones = Enum.GetValues<HitZone>();
        SolutionTable = new HitZone[Players, Sequences];

        for (int col = 0; col < Sequences; col++)
        {
            var availableRows = new List<int>();
            for (int r = 0; r < Players; r++)
            {
                availableRows.Add(r);
            }
            var shuffledZones = new List<HitZone>(zones);
            Shuffle(random, shuffledZones);

            for (int i = 0; i < zones.Length; i++)
            {
                int rowIndex = availableRows[random.Next(availableRows.Count)];
                availableRows.Remove(rowIndex);
                SolutionTable[rowIndex, col] = shuffledZones[i];
            }

            foreach (var row in availableRows)
            {
                var randomZone = zones[random.Next(zones.Length)];
                SolutionTable[row, col] = randomZone;
            }
        }
    }

    private void GenerateSolution()
    {
        if (SolutionTable == null) { return; }
        if (SolutionTable.GetLength(0) == 0) { return; }
        int cols = SolutionTable.GetLength(1);
        int rows = SolutionTable.GetLength(0);

        TrueSolutionZones = new List<(HitZone, HitZone)>();
        Pairs = new List<(int, int)>();

        HitZone? lastInDirection = null;

        for (int col = 0; col < cols; col++)
        {
            var validPairs = new List<((HitZone, HitZone) pair, (int, int) indices)>();

            var allRowPairs = new List<(int, int)>();
            for (int i = 0; i < rows; i++)
                for (int j = i + 1; j < rows; j++)
                    allRowPairs.Add((i, j));

            Shuffle(random, allRowPairs);

            foreach (var (i, j) in allRowPairs)
            {
                var a = SolutionTable[i, col];
                var b = SolutionTable[j, col];

                if (a == b || IsOpposite(a, b)) continue;

                if (ContainsInWithDirection(a, b, out var currentInDirection))
                {
                    if (lastInDirection.HasValue && IsOpposite(currentInDirection, lastInDirection.Value))
                        continue;
                }

                validPairs.Add(((a, b), (i, j)));
            }



            var (selectedPair, selectedIndices) = validPairs[random.Next(validPairs.Count)];
            TrueSolutionZones.Add(selectedPair);
            Pairs.Add(selectedIndices);

            if (ContainsInWithDirection(selectedPair.Item1, selectedPair.Item2, out var newInDir))
                lastInDirection = newInDir;
        }
    }

    private static bool IsOpposite(HitZone a, HitZone b)
    {
        return Opposites.TryGetValue(a, out var oppA) && oppA == b;
    }

    private static bool ContainsInWithDirection(HitZone a, HitZone b, out HitZone direction)
    {
        direction = default;

        if (a == HitZone.In && IsCardinal(b))
        {
            direction = b;
            return true;
        }
        if (b == HitZone.In && IsCardinal(a))
        {
            direction = a;
            return true;
        }

        return false;
    }

    private static bool IsCardinal(HitZone a)
    {
        return a == HitZone.North || a == HitZone.East || a == HitZone.South || a == HitZone.West;
    }

    private static void Shuffle<T>(Random rand, List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
    #endregion

    
    private void DebugOutput()
    {
        if (SolutionTable == null || Pairs == null || TrueSolutionZones == null)
        {
            Logger.Info("Something is null");
            return;
        }
        var str = "";
        int rows = SolutionTable.GetLength(0);
        int cols = SolutionTable.GetLength(1);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                str += $"{SolutionTable[r, c],6} ";
            }
            Logger.Info(str);
            str = "";
        }


        Logger.Info("Player Solution");
        Pairs.Each(a =>
        {
            Logger.Info($"{a.ToString()}");
        });

        Logger.Info("Zone Solution");
        TrueSolutionZones.Each(a =>
        {
            Logger.Info($"{a.ToString()}");
        });
        /*
        Logger.Info("Symbol Paths");
        SymbolPaths.Each(a =>
        { 
            Logger.Info(a.ToString());
        });
        Logger.Info("Party Members");

        Dalamud.PartyList.Each(a =>
        {
            Logger.Info(a.ToString());
        });
        */
    }    
}
