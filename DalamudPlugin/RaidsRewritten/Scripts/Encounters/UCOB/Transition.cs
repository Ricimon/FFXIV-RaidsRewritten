using System;
using System.Collections.Generic;
using System.Numerics;
using ECommons;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

internal class Transition : Mechanic
{
    public enum Phase
    {
        Octet,
        Pheonix,
        Resolution
    }
    private static readonly Dictionary<uint, Phase> HookedActions = new()
    {
        { 41, Phase.Octet},       //Octet NEED NUMBER
        { 9970, Phase.Pheonix },    //Pheonix
        { 1646, Phase.Resolution }, //NEED NUMBER FOR RESOLUTION
    };
    private readonly Vector3 ArenaCenter = new(100, 0, 100);
    private const int ArenaRadius = 22;
    public int RngSeed { get; set; }
    private Random? random;
    private readonly List<Entity> attacks = [];
    private List<int> telegraphs = [0, 1, 2, 3, 4, 5, 6, 7];
    private static List<string> SymbolPaths = new List<string> {
        "0",
        "1",
        "2",
        "3",
        "4",
        "5",
        "6",
        "7",
    };
    private int resolution;

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

    public override void OnCombatEnd()
    {
        Reset();
    }
    
    private struct AddData
    {
        public int Kaliya;
        public int Melusine;
        public int[] Ads;
    }

    private readonly Dictionary<int, AddData> Table = new Dictionary<int, AddData>
    {
        { 
            0, new AddData { Kaliya = 0, Melusine = 7, Ads = new int[] { 2, 4, 6 } } 
        },
        {
            1, new AddData { Kaliya = 0, Melusine = 1, Ads = new int[] { 2, 4, 6 } }
        },
        {
            2, new AddData { Kaliya = 2, Melusine = 3, Ads = new int[] { 0, 4, 6 } }
        },
        {
            3, new AddData { Kaliya = 2, Melusine = 1, Ads = new int[] { 0, 4, 6 } }
        },
        {
            4, new AddData { Kaliya = 4, Melusine = 5, Ads = new int[] { 0, 2, 6 } }
        },
        {
            5, new AddData { Kaliya = 4, Melusine = 3, Ads = new int[] { 0, 2, 6 } }
        },
        {
            6, new AddData { Kaliya = 6, Melusine = 7, Ads = new int[] { 0, 2, 4 } }
        },
        {
            7, new AddData { Kaliya = 6, Melusine = 5, Ads = new int[] { 0, 2, 4 } }
        },
    };



    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Target == null) { return; }
        if (!HookedActions.TryGetValue(set.Action.Value.RowId, out var phase)) { return; }
        switch (phase)
        {
            case Phase.Octet:
                var seed = RngSeed;
                random = new Random(seed);
                Shuffle(random, telegraphs);
                Shuffle(random, SymbolPaths);
                resolution = random.Next(0, 7);
                DebugOutput();

                //Kaliya
                if (this.AttackManager.TryCreateAttackEntity<ADS>(out var kaliya))
                {
                    var angle = 2 * MathF.PI / 8 * Table[telegraphs[0]].Kaliya;
                    var pos = new Vector3(
                    ArenaCenter.X - ArenaRadius * MathF.Sin(angle),
                    ArenaCenter.Y,
                    ArenaCenter.Z - ArenaRadius * MathF.Cos(angle)
                    );
                    kaliya.Set(new Position(pos));
                    attacks.Add(kaliya);
                }

                //Melusine
                if (this.AttackManager.TryCreateAttackEntity<ADS>(out var melusine))
                {
                    var angle = 2 * MathF.PI / 8 * Table[telegraphs[0]].Melusine;
                    var pos = new Vector3(
                    ArenaCenter.X - ArenaRadius * MathF.Sin(angle),
                    ArenaCenter.Y,
                    ArenaCenter.Z - ArenaRadius * MathF.Cos(angle)
                    );
                    melusine.Set(new Position(pos));
                    attacks.Add(melusine);
                }

                //ADS
                for (int i = 0; i < 3; i++)
                {
                    if (this.AttackManager.TryCreateAttackEntity<ADS>(out var ads))
                    {
                        var angle = 2 * MathF.PI / 8 * Table[telegraphs[0]].Ads[i];
                        var pos = new Vector3(
                        ArenaCenter.X - ArenaRadius * MathF.Sin(angle),
                        ArenaCenter.Y,
                        ArenaCenter.Z - ArenaRadius * MathF.Cos(angle)
                        );
                        ads.Set(new Position(pos));
                        attacks.Add(ads);
                    }
                }
                //Show Table[telegraphs[0]] during octet 
                //Delay and execute telegraphs
                break;
            case Phase.Pheonix:
                //find local player's location on list
                //Show Table[telegraphs[LP]] with SymbolPaths[LP]
                //Spawn Portals
                break;
            case Phase.Resolution:
                //Show SymbolPath[resolution] on gates
                //Spawn Table[telegraphs[resolution]] and execute attack
                break;
        }
    }

    private static void Shuffle<T>(Random rand, List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void DebugOutput()
    {
        Table.TryGetValue(telegraphs[resolution], out var data);
        Logger.Debug($"Resolution Number: {resolution}");
        Logger.Debug($"Resolution Actual: {telegraphs[resolution]}");
        Logger.Debug($"Symbol: {SymbolPaths[resolution]}");
        Logger.Debug($"Kaliya: {data.Kaliya.ToString()} ");
        Logger.Debug($"Melusine: {data.Melusine.ToString()} ");
        Logger.Debug($"ADS: {data.Ads[0].ToString()}, {data.Ads[1].ToString()}, {data.Ads[2].ToString()}");
        string str = "";
        Logger.Debug($"Octet Telegraph: {telegraphs[0]}");
        telegraphs.Each(a =>
        {
            str += a.ToString();
        });
        Logger.Debug(str);
        str = "";
        SymbolPaths.Each(a => 
        {
            str += a.ToString();
        });
        Logger.Debug(str);
        str = "";
    }
}
