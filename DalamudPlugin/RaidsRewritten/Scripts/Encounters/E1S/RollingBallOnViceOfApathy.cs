using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.E1S;

public class RollingBallOnViceOfApathy : Mechanic
{
    public int RngSeed
    {
        get => rngSeed;
        set
        {
            rngSeed = value;
            random = new Random(value);
        }
    }

    private const uint ViceOfApathyDataId = 0x1EAE20;

    private readonly List<Entity> attacks = [];

    private int rngSeed;
    private bool ballSpawned;
    private Random random = new();

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe)
        {
            Reset();
        }
    }

    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
        if (newObject.DataId != ViceOfApathyDataId) { return; }
        if (ballSpawned) { return; }

        var arenaCenter = new Vector3(100, 0, 100);
        float arenaWidth = 40;

        var X = arenaCenter.X - arenaWidth / 2f + random.NextSingle() * arenaWidth;
        var Z = arenaCenter.Z - arenaWidth / 2f + random.NextSingle() * arenaWidth;
        var r = MathUtilities.ClampRadians(random.NextSingle() * 2 * MathF.PI);

        if (this.AttackManager.TryCreateAttackEntity<RollingBall>(out var ball))
        {
            ball.Set(new Position(new Vector3(X, arenaCenter.Y, Z)))
                .Set(new Rotation(r))
                .Set(new RollingBall.Movement(MathUtilities.RotationToUnitVector(r)))
                .Set(new RollingBall.SquareArena(arenaCenter.ToVector2(), arenaWidth))
                .Set(new RollingBall.SeededRandom(random));
            this.attacks.Add(ball);

            ballSpawned = true;
        }
    }

    private void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        ballSpawned = false;
        random = new Random(RngSeed);
    }
}
