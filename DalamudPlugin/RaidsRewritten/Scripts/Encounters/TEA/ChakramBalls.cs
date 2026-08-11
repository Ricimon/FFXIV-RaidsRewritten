using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Hooks;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;
using ZLinq;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class ChakramBalls : Mechanic
{
    public int RngSeed { get; set; }
    
    private const uint STEAM_CHAKRAM_BASE_ID = 0x2C4D;
    private const float rollingDelay = 4.5f;
    private const float arenaRadius = 20f;
    private const int maxBalls = 2;
    private const float BallDelay = 0.85f;
    
    private readonly IReadOnlyList<Vector3> ballPositions = 
    [
        new(100, 0, 121.5f),
        new(85, 0, 115f),
        new(78.5f, 0, 100),
        new(85f, 0, 85f),
        new(100, 0, 78.5f),
        new(115, 0, 85),
        new(121.5f, 0, 100),
        new(115, 0 , 115)
    ];
    
    private readonly List<Vector3> chakramPositions =
    [
        new(100, -4.2657223E-15f, 121.5f),
        new(84.7972f, -3.5527137E-15f, 115.2028f),
        new(78.5f, 1.747305E-20f, 99.99948f),
        new(84.7972f, 3.0153164E-15f, 84.7972f),
        new(100, 4.2651514E-15f, 78.5f),
        new(115.2028f, 3.0160776E-15f, 84.7972f),
        new(121.5f, 1.2397274E-20f, 99.99948f),
        new(115.2028f, -3.0160323E-15f, 115.2028f)
    ];
    
    
    private Vector3 arenaCenter = new Vector3(100, 0, 100);
    private readonly List<Entity> attacks = [];
    private int ballsSpawned = 0;
    private int chakramsSpawned = 0;
    private Vector3 chakramPos;
    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
        ballsSpawned = 0;
        chakramsSpawned = 0;
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

    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
        if (newObject.BaseId != STEAM_CHAKRAM_BASE_ID) { return; }
        if (ballsSpawned > maxBalls) { return; }
        
        //Comparing to make sure every client's math is based off the same Chakram
        if (chakramsSpawned < 1)
        {
            chakramPos = newObject.Position;
            chakramsSpawned++;
            return;
        }
        if (newObject.Position.Y < chakramPos.Y)
        {
            chakramPos = newObject.Position;
        }
        
        //Party List and player randomizer
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
        
        int anchor = chakramPositions.FindIndex(e => e.Y == chakramPos.Y);
        if (anchor == -1) { return; }

        var random = new Random(RngSeed);
        playerList = [.. playerList.AsValueEnumerable().OrderBy(o => random.Next())];
        var ball1Position = ballPositions[(anchor + random.Next(1, 4)) % 8];
        var ball2Position = ballPositions[(anchor + random.Next(5, 8)) % 8];
        var player1Position = playerList[0].Position;
        var player2Position = playerList[1].Position;
        var ball1Direction = new Vector2(player1Position.X - ball1Position.X, player1Position.Z - ball1Position.Z);
        var ball2Direction = new Vector2(player2Position.X - ball2Position.X, player2Position.Z - ball2Position.Z);

        var spawnAction = DelayedAction.Create(World, () =>
        {
            if (!EntityManager.TryCreateEntity<RollingBall>(out var ball1)) { return; }
            ball1.Set(new RollingBall.Component(TimeUntilRolling: rollingDelay))
                .Set(new Position(ball1Position))
                .Set(new Rotation(MathUtilities.VectorToRotation(ball1Direction)))
                .Set(new RollingBall.Movement(ball1Direction))
                .Set(new RollingBall.CircleArena(arenaCenter.ToVector2(), arenaRadius))
                .Set(new RollingBall.WallBounces(0))
                .Add<RollingBall.NotResistible>();
            attacks.Add(ball1);
            ballsSpawned++;
        
            if (!EntityManager.TryCreateEntity<RollingBall>(out var ball2)) { return; }
            ball2.Set(new RollingBall.Component(TimeUntilRolling: rollingDelay))
                .Set(new Position(ball2Position))
                .Set(new Rotation(MathUtilities.VectorToRotation(ball2Direction)))
                .Set(new RollingBall.Movement(ball2Direction))
                .Set(new RollingBall.CircleArena(arenaCenter.ToVector2(), arenaRadius))
                .Set(new RollingBall.WallBounces(0))
                .Add<RollingBall.NotResistible>();
            attacks.Add(ball2);
            ballsSpawned++;
        }, BallDelay);
        attacks.Add(spawnAction);
    }
}