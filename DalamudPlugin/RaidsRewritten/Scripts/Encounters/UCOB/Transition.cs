using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class Transition : Mechanic
{
    public enum Phase
    {
        Octet,
        Teraflare,
    }

    private static readonly Dictionary<uint, Phase> HookedActions = new()
    {
        { 9959, Phase.Octet},       //Octet NEED NUMBER
        { 9961, Phase.Teraflare },    //Teraflare ID
    };

    private readonly Vector3 ArenaCenter = new(0, 0, 0);
    private const int ArenaRadius = 22;
    public int RngSeed { get; set; }
    private Random? random;
    private readonly List<Entity> attacks = [];
    private readonly List<Entity> gates = [];
    private List<IBattleChara> playerList = [];
    private IBattleChara? localPlayer;
    private static float OctetDelay = 40.0f;
    private static float TeraflareDelay = 8.0f;
    private static float SpawnDelay = TeraflareDelay;
    private static float TelegraphDelay = 8.0f;
    private static float GateMarkerDelay = TeraflareDelay + 10.0f;
    private static float PortalMarkerDelay = TeraflareDelay + 25.0f;
    private static float ResolutionDelay = TeraflareDelay + 35.0f;
    private static float ResetDelay = ResolutionDelay + 5.0f;

    private List<int> telegraphs = [0, 1, 2, 3, 4, 5, 6, 7];
    private List<int> SymbolNumber = [0, 1, 2, 3, 4, 5, 6, 7];
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

    private readonly List<string> SymbolPaths = new List<string> {
        "vfx/lockon/eff/m0361trg_a1t.avfx",
        "vfx/lockon/eff/m0361trg_a2t.avfx",
        "vfx/lockon/eff/m0361trg_a3t.avfx",
        "vfx/lockon/eff/m0361trg_a4t.avfx",
        "vfx/lockon/eff/m0361trg_a5t.avfx",
        "vfx/lockon/eff/m0361trg_a6t.avfx",
        "vfx/lockon/eff/m0361trg_a7t.avfx",
        "vfx/lockon/eff/m0361trg_a8t.avfx",
    };

    private void ShowAds(int value, float delay)
    {
        //Kaliya
        if (EntityManager.TryCreateEntity<NerveGasKaliya>(out var kaliya))
        {
            var angle = 2 * MathF.PI / 8 * Table[value].Kaliya;
            var pos = new Vector3(
            ArenaCenter.X - ArenaRadius * MathF.Sin(angle),
            ArenaCenter.Y,
            ArenaCenter.Z - ArenaRadius * MathF.Cos(angle)
            );
            kaliya.Set(new Position(pos));
            kaliya.Set(new Rotation(angle));
            kaliya.Set(new NerveGasKaliya.AttackDelay(delay));
            attacks.Add(kaliya);
        }

        //Melusine
        if (EntityManager.TryCreateEntity<CircleBladeMelusine>(out var melusine))
        {
            var angle = 2 * MathF.PI / 8 * Table[value].Melusine;
            var pos = new Vector3(
            ArenaCenter.X - ArenaRadius * MathF.Sin(angle),
            ArenaCenter.Y,
            ArenaCenter.Z - ArenaRadius * MathF.Cos(angle)
            );
            melusine.Set(new Position(pos));
            melusine.Set(new Rotation(angle));
            melusine.Set(new CircleBladeMelusine.AttackDelay(delay));
            attacks.Add(melusine);
        }

        //ADS
        for (int i = 0; i < 3; i++)
        {
            if (EntityManager.TryCreateEntity<RepellingCannonADS>(out var ads))
            {
                var angle = 2 * MathF.PI / 8 * Table[value].Ads[i];
                var pos = new Vector3(
                ArenaCenter.X - ArenaRadius * MathF.Sin(angle),
                ArenaCenter.Y,
                ArenaCenter.Z - ArenaRadius * MathF.Cos(angle)
                );
                ads.Set(new Position(pos));
                ads.Set(new Rotation(angle));
                ads.Set(new RepellingCannonADS.AttackDelay(delay));
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
                random = new Random(seed);
                Shuffle(random, telegraphs);
                Shuffle(random, SymbolNumber);
                resolution = random.Next(0, 7);
                foreach (var player in this.Dalamud.ObjectTable.PlayerObjects)
                {
                    playerList.Add(player);
                }
                if (playerList.Count != 8)
                {
                    this.Logger.Debug($"uh oh, unexpected number of players: {playerList.Count}");
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

                var da = DelayedAction.Create(World, () => 
                { 
                    ShowAds(telegraphs[0], TelegraphDelay); 
                }, OctetDelay);
                attacks.Add(da);
                DebugOutput();
                break;
            case Phase.Teraflare:
                localPlayer = this.Dalamud.ClientState.LocalPlayer;
                if (localPlayer == null) { return; }
                int playerNumber = playerList.IndexOf(localPlayer);


                var da1 = DelayedAction.Create(World, () => 
                {
                    ShowAds(telegraphs[playerNumber], TelegraphDelay);
                }, SpawnDelay);

                var da2 = DelayedAction.Create(World, () => 
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (EntityManager.TryCreateEntity<VoidGate>(out var voidgate))
                        {
                            var angle = 2 * MathF.PI / 8 * i;
                            var pos = new Vector3(
                            ArenaCenter.X - ArenaRadius * MathF.Sin(angle),
                            ArenaCenter.Y + 1.5f,
                            ArenaCenter.Z - ArenaRadius * MathF.Cos(angle)
                            );
                            voidgate.Set(new Position(pos));
                            voidgate.Set(new Rotation(angle));
                            voidgate.Set(new VoidGate.SpawnDelay(4.0f));
                            voidgate.Set(new VoidGate.ExpelDelay(24.2f));
                            gates.Add(voidgate);
                        }
                    }
                    CommonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                    {
                        var fa = FakeActor.Create(World)
                            .Set(new Position(localPlayer.Position))
                            .Set(new Rotation(localPlayer.Rotation))
                            .ChildOf(e);
                        attacks.Add(fa);
                        var lc = this.World.Entity()
                            .Set(new ActorVfx(SymbolPaths[SymbolNumber[playerNumber]]))
                            .ChildOf(fa);
                        attacks.Add(lc);
                        //LimitCutNumber.ApplyToTarget(fa, 10, SymbolNumber[playerNumber]);
                    });
                }, GateMarkerDelay);

                var da3 = DelayedAction.Create(World, () => 
                {
                    gates.ForEach(e =>
                    {
                        var lc = this.World.Entity()
                            .Set(new ActorVfx(SymbolPaths[SymbolNumber[playerNumber]]))
                            .ChildOf(e);
                        attacks.Add(lc);
                        //LimitCutNumber.ApplyToTarget(e, 10, SymbolNumber[resolution]);
                    });

                }, PortalMarkerDelay);
                var da4 = DelayedAction.Create(World, () => 
                {
                    ShowAds(telegraphs[resolution], 0.0f);
                }, ResolutionDelay);

                var da5 = DelayedAction.Create(World, () =>
                {
                    Reset();
                }, ResetDelay);

                attacks.Add(da1);
                attacks.Add(da2);
                attacks.Add(da3);
                attacks.Add(da4);
                attacks.Add(da5);
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
        Logger.Debug($"Symbol: {SymbolNumber[resolution]}");
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
        SymbolNumber.Each(a => 
        {
            str += a.ToString();
        });
        Logger.Debug(str);
        str = "";
    }
}
