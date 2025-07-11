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

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class RollingBallOnFirstNeurolink : Mechanic
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

    private const uint NeurolinkDataId = 0x1E88FF;

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
        if (newObject.DataId != NeurolinkDataId) { return; }
        if (ballSpawned) { return; }

        var arenaCenter = new Vector3(0, 0, 0);
        float arenaRadius = 21.5f;

        var polarAngle = MathUtilities.ClampRadians(random.NextSingle() * 2 * MathF.PI);
        var polarDist = (1 - MathF.Pow(random.NextSingle(), 2)) * arenaRadius;
        var v = polarDist * MathUtilities.RotationToUnitVector(polarAngle);
        var r = MathUtilities.ClampRadians(random.NextSingle() * 2 * MathF.PI);

        if (this.AttackManager.TryCreateAttackEntity<RollingBall>(out var ball))
        {
            ball.Set(new Position(new Vector3(v.X, arenaCenter.Y, v.Y)))
                .Set(new Rotation(r))
                .Set(new RollingBall.Movement(MathUtilities.RotationToUnitVector(r)))
                .Set(new RollingBall.CircleArena(arenaCenter.ToVector2(), arenaRadius))
                .Set(new RollingBall.SeededRandom(random));
            this.attacks.Add(ball);

            ballSpawned = true;
        }
    }

    private void Reset()
    {
        foreach(var attack in attacks)
        {
            attack.Destruct();
        }
        ballSpawned = false;
        random = new Random(RngSeed);
    }
}
