using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;
using TerraFX.Interop.Windows;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class ChakBall : Mechanic
{
    private const uint CHAKRAM_ACTION_ID = 18517;
    private const uint STEAM_CHAKRAM_BASE_ID = 0x2C4D;
    private const float RollingDelay = 5f;
    private const float arenaRadius = 20f;
    
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
        var ballPosition = new Vector3(arenaCenter.Z + translatedPosition.Z, newObject.Position.Y, newObject.Position.X);
        var r = MathUtilities.ClampRadians(MathF.Atan2(translatedPosition.Z, translatedPosition.X) + MathF.PI);
        
        if (!EntityManager.TryCreateEntity<RollingBall>(out var ball)) { return; }

        ball.Set(new RollingBall.Component(TimeUntilRolling: RollingDelay))
            .Set(new Position(ballPosition))
            .Set(new Rotation(r))
            .Set(new RollingBall.Movement(MathUtilities.RotationToUnitVector(r)))
            .Set(new RollingBall.CircleArena(arenaCenter.ToVector2(), arenaRadius))
            .Set(new RollingBall.WallBounces(0));
            attacks.Add(ball);
        ballsSpawned++;
    }
}