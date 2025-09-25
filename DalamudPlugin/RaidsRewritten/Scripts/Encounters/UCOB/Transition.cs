using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

internal class Transition : Mechanic
{
    public enum Phase
    {
        Octet,
        Pheonix,
    }

    private static readonly Dictionary<uint, Phase> HookedActions = new()
    {
        { 41, Phase.Octet},       //Octet NEED NUMBER
        { 16462, Phase.Pheonix },    //Pheonix NEED NUMBER
    };

    private readonly Vector3 ArenaCenter = new(100, 0, 100);
    private const int ArenaRadius = 22;
    public int RngSeed { get; set; }
    private Random? random;
    private readonly List<Entity> attacks = [];
    private readonly List<Entity> gates = [];
    private List<IBattleChara> playerList = [];
    private IBattleChara? localPlayer;
    private static float PheonixDelay = 3.6f;
    private static float PlayerLimitCutDelay = 5.5f;
    private static float GateLimitCutDelay = 10.0f;
    private static float PlayerMarkerDelay = 15.0f;
    private static float ResolutionDelay = PlayerMarkerDelay + 1.0f;
    private List<int> telegraphs = [0, 1, 2, 3, 4, 5, 6, 7];
    private List<int> SymbolPaths = [0, 1, 2, 3, 4, 5, 6, 7];
    private int resolution;

    public override void Reset()
    {
        foreach (var attack in this.attacks)
        {
            attack.Destruct();
        }
        foreach (var gate in this.gates) 
        { 
            gate.Destruct();
        }
        this.attacks.Clear();
        this.gates.Clear();
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

    private void ShowAds(int value)
    {
        //Kaliya
        if (this.AttackManager.TryCreateAttackEntity<NerveGasKaliya>(out var kaliya))
        {
            var angle = 2 * MathF.PI / 8 * Table[value].Kaliya;
            var pos = new Vector3(
            ArenaCenter.X - ArenaRadius * MathF.Sin(angle),
            ArenaCenter.Y,
            ArenaCenter.Z - ArenaRadius * MathF.Cos(angle)
            );
            kaliya.Set(new Position(pos));
            kaliya.Set(new Rotation(angle));
            attacks.Add(kaliya);
        }

        //Melusine
        if (this.AttackManager.TryCreateAttackEntity<CircleBladeMelusine>(out var melusine))
        {
            var angle = 2 * MathF.PI / 8 * Table[value].Melusine;
            var pos = new Vector3(
            ArenaCenter.X - ArenaRadius * MathF.Sin(angle),
            ArenaCenter.Y,
            ArenaCenter.Z - ArenaRadius * MathF.Cos(angle)
            );
            melusine.Set(new Position(pos));
            melusine.Set(new Rotation(angle));
            attacks.Add(melusine);
        }

        //ADS
        for (int i = 0; i < 3; i++)
        {
            if (this.AttackManager.TryCreateAttackEntity<RepellingCannonADS>(out var ads))
            {
                var angle = 2 * MathF.PI / 8 * Table[value].Ads[i];
                var pos = new Vector3(
                ArenaCenter.X - ArenaRadius * MathF.Sin(angle),
                ArenaCenter.Y,
                ArenaCenter.Z - ArenaRadius * MathF.Cos(angle)
                );
                ads.Set(new Position(pos));
                ads.Set(new Rotation(angle));
                attacks.Add(ads);
            }
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        
        if (set.Action == null) { return; }
        if (set.Target == null) { return; }
        if (!HookedActions.TryGetValue(set.Action.Value.RowId, out var phase)) { return; }
        switch (phase)
        {
            case Phase.Octet:
                var seed = RngSeed;
                localPlayer = this.Dalamud.ClientState.LocalPlayer;
                random = new Random(seed);
                Shuffle(random, telegraphs);
                Shuffle(random, SymbolPaths);
                resolution = random.Next(0, 7);
                ShowAds(telegraphs[0]);
                
                foreach (var player in this.Dalamud.ObjectTable.PlayerObjects)
                {
                    playerList.Add(player);

                }
                if (playerList.Count != 8)
                {
                    this.Logger.Debug($"uh oh, unexpected number of players: {playerList.Count}");
                    //return;
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

                DebugOutput();
                break;
            case Phase.Pheonix:
                int playerNumber = playerList.IndexOf(localPlayer);

                ShowAds(telegraphs[playerNumber]);

                for (int i = 0; i < 8; i++)
                {
                    if (this.AttackManager.TryCreateAttackEntity<VoidGate>(out var voidgate))
                    {
                        var angle = 2 * MathF.PI / 8 * i;
                        var pos = new Vector3(
                        ArenaCenter.X - ArenaRadius * MathF.Sin(angle),
                        ArenaCenter.Y + 1.5f,
                        ArenaCenter.Z - ArenaRadius * MathF.Cos(angle)
                        );
                        voidgate.Set(new Position(pos));
                        voidgate.Set(new Rotation(angle));
                        gates.Add(voidgate);
                    }
                }

                var da1 = DelayedAction.Create(World, () => 
                {
                    CommonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                    {
                        LimitCutNumber.ApplyToTarget(e, 10, SymbolPaths[playerNumber]);
                    });
                }, PlayerLimitCutDelay);

                attacks.Add(da1);
                var da2 = DelayedAction.Create(World, () =>
                {
                    gates.ForEach(e =>
                    {
                        LimitCutNumber.ApplyToTarget(e, 10, SymbolPaths[resolution]);
                    });
                }, GateLimitCutDelay);
                attacks.Add(da2);

                var da4 = DelayedAction.Create(World, () =>
                {
                    ShowAds(telegraphs[resolution]);
                }, ResolutionDelay);
                attacks.Add(da4);
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
