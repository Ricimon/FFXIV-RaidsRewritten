using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Systems;
using RaidsRewritten.Utility;
using TerraFX.Interop.Windows;
using ZLinq;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class ChakBall : Mechanic
{
    private const uint CHAKRAM_ACTION_ID = 18517;
    private const uint STEAM_CHAKRAM_BASE_ID = 0x2C4D;
    private const float RollingDelay = 5f;
    private const float arenaRadius = 20f;
    public int RngSeed { get; set; }
    
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
        if (ballsSpawned > 0) { return; }
        if (chakramsSpawned < 1)
        {
            chakramPos = newObject.Position;
            chakramsSpawned++;
            return;
        }
        
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
        playerList = [.. playerList.AsValueEnumerable().OrderBy(o => random.Next())];
        
        
        //Comparing to make sure every client's math is based off the same Chakram
        if (newObject.Position.X == chakramPos.X)
        {
            if (newObject.Position.Y < chakramPos.Y)
            {
                chakramPos = newObject.Position;
            }
        }
        else
        {
            if (newObject.Position.X < chakramPos.X)
            {
                chakramPos = newObject.Position;
            }
        }
        
        var translatedPosition = chakramPos - arenaCenter;
        var player1Position = playerList[0].Position - arenaCenter;
        var player2Position = playerList[1].Position - arenaCenter;
        var ball1Position = new Vector3(arenaCenter.Z + translatedPosition.Z, newObject.Position.Y, newObject.Position.X);
        var ball2Position = new Vector3(arenaCenter.Z - translatedPosition.Z, newObject.Position.Y, newObject.Position.X);
        var r1 = MathUtilities.ClampRadians(MathF.Atan2(translatedPosition.Z + player1Position.Z, translatedPosition.X + player2Position.X) + MathF.PI);
        var r2 = MathUtilities.ClampRadians(MathF.Atan2(translatedPosition.Z + player2Position.Z, translatedPosition.X + player2Position.X));
        
        if (!EntityManager.TryCreateEntity<RollingBall>(out var ball1)) { return; }
        ball1.Set(new RollingBall.Component(TimeUntilRolling: RollingDelay))
            .Set(new Position(ball1Position))
            .Set(new Rotation(r1))
            .Set(new RollingBall.Movement(MathUtilities.RotationToUnitVector(r1)))
            .Set(new RollingBall.CircleArena(arenaCenter.ToVector2(), arenaRadius))
            .Set(new RollingBall.WallBounces(0));
            attacks.Add(ball1);
        ballsSpawned++;
        
        if (!EntityManager.TryCreateEntity<RollingBall>(out var ball2)) { return; }
        ball2.Set(new RollingBall.Component(TimeUntilRolling: RollingDelay))
            .Set(new Position(ball2Position))
            .Set(new Rotation(r2))
            .Set(new RollingBall.Movement(MathUtilities.RotationToUnitVector(r2)))
            .Set(new RollingBall.CircleArena(arenaCenter.ToVector2(), arenaRadius))
            .Set(new RollingBall.WallBounces(0));
        attacks.Add(ball2);
        ballsSpawned++;
    }
}