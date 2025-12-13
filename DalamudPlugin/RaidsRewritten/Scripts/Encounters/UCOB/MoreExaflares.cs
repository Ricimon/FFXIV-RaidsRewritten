using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Components;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class MoreExaflares : Mechanic
{
    public enum Difficulties
    {
        Intended = 0,
        Unnerfed = 1,
        Insane = 2,
        Impossible = 3,
    }

    private struct DifficultyData
    {
        public int MaxConcurrentExaflares;
        public int RequiredNeuroNum;
        public List<uint> ActionEffectIds;
        public List<uint> ObjectIds;
        public List<uint> StartCastIds;
        public bool NerfedGolden;
    }

    private static readonly Vector3 Center = new(0, 0, 0);
    private const float Radius = 20f;

    private readonly Dictionary<Difficulties, DifficultyData> DifficultyInfo = new()
    {
        {
            Difficulties.Intended, new DifficultyData
            {
                MaxConcurrentExaflares = 1,
                RequiredNeuroNum = 2,
                ActionEffectIds = [
                    9939,  // calamitous blaze (seventh umbral era)
                ],
                ObjectIds = [NeurolinkBaseId],
                StartCastIds = [
                    9967,  // exaflare part 1
                    9968,  // exaflare part 2
                ],
                NerfedGolden = true
            }
        },
        {
            Difficulties.Unnerfed, new DifficultyData
            {
                MaxConcurrentExaflares = 1,
                RequiredNeuroNum = 2,
                ActionEffectIds = [
                    9939,  // calamitous blaze (seventh umbral era)
                ],
                ObjectIds = [NeurolinkBaseId],
                StartCastIds = [
                    9967,  // exaflare part 1
                    9968,  // exaflare part 2
                ],
                NerfedGolden = false
            }
        },
        {
            Difficulties.Insane, new DifficultyData
            {
                MaxConcurrentExaflares = 1,
                RequiredNeuroNum = 2,
                ActionEffectIds = [
                    9939,  // calamitous blaze (seventh umbral era)
                    9950,  // megaflare stack
                ],
                ObjectIds = [NeurolinkBaseId],
                StartCastIds = [
                    9967,  // exaflare part 1
                    9968,  // exaflare part 2
                ],
                NerfedGolden = false
            }
        },
        {
            Difficulties.Impossible, new DifficultyData
            {
                MaxConcurrentExaflares = 2,
                RequiredNeuroNum = 0,
                ActionEffectIds = [
                    9900, // fireball (twin)
                    9901, // liquid hell
                    9914, // adds megaflare
                    9925, // fireball (firehorn)
                    9942, // gigaflare
                ],
                ObjectIds = [
                    NeurolinkBaseId
                ],
                StartCastIds = [
                    9941,  // flatten
                    9967,  // exaflare part 1
                    9968,  // exaflare part 2
                ],
                NerfedGolden = false
            }
        },
    };

    private const uint NeurolinkBaseId = 0x1E88FF;
    private int LiquidHellCounter = 0;
    private int CurrentNeuroCounter = 0;
    private bool GoldenCanSpawnExa = false;

    private int ExaflareRowsSpawned = 0;
    private readonly List<Entity> attacks = [];
    public int RngSeed { get; set; }
    public Difficulties Difficulty { get; set; } = Difficulties.Intended;

    public override void Reset()
    {
        foreach (var attack in this.attacks)
        {
            attack.Destruct();
        }
        this.attacks.Clear();
        LiquidHellCounter = 0;
        GoldenCanSpawnExa = false;
        ExaflareRowsSpawned = 0;
        CurrentNeuroCounter = 0;
    }

    private int CountActiveAttacks()
    {
        var numAttacks = 0;
        for (int i = attacks.Count - 1; i >= 0; i--)
        {
            if (attacks[i].IsValid())
            {
                numAttacks++;
            } else
            {
                attacks.RemoveAt(i);
            }
        }
        return numAttacks;
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

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        var allowedAttacks = DifficultyInfo[Difficulty].ActionEffectIds;
        if (!allowedAttacks.Contains(set.Action.Value.RowId)) { return; }

        // only spawn every 5 liquid hells
        if (set.Action.Value.RowId == 9901)
        {
            LiquidHellCounter++;
            if (LiquidHellCounter >= 5)
            {
                LiquidHellCounter = 0;
            }

            if (LiquidHellCounter != 1) { return; }
        }

        RandomExaflareRow();
    }

    public override void OnStartingCast(Lumina.Excel.Sheets.Action action, IBattleChara source)
    {
        var allowedAttacks = DifficultyInfo[Difficulty].StartCastIds;
        if (!allowedAttacks.Contains(action.RowId)) { return; }

        switch (action.RowId)
        {
            case 9941:
                RandomExaflareRow();
                break;
            case 9967:
                GoldenCanSpawnExa = true;
                break;
            case 9968:
                if (!GoldenCanSpawnExa) { return; }
                GoldenCanSpawnExa = false;
                var angleNumber = MathF.Round(MathHelper.RadToDeg(source.Rotation)) / 45;
                var exaDirection = Convert.ToInt32(angleNumber) % 8;
                RandomExaflareRow(exaDirection, DifficultyInfo[Difficulty].NerfedGolden);
                break;
            default:
                return;
        }
    }

    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
        var allowedAttacks = DifficultyInfo[Difficulty].ObjectIds;
        if (!allowedAttacks.Contains(newObject.BaseId)) { return; }

        if (DifficultyInfo[Difficulty].RequiredNeuroNum <= CurrentNeuroCounter)
        {
            RandomExaflareRow();
        } else
        {
            CurrentNeuroCounter++;
        }

    }

    private void RandomExaflareRow()
    {
        if (CountActiveAttacks() >= DifficultyInfo[Difficulty].MaxConcurrentExaflares) { return; }

        var seed = RngSeed;
        unchecked
        {
            seed += ExaflareRowsSpawned * 69420;
        }
        var random = new Random(seed);

        var randVal = random.Next(8);

        CalculateExaPosition(random, randVal);
    }

    private void RandomExaflareRow(int excludeAngle, bool onlyRelativeCardinal)
    {
        if (CountActiveAttacks() >= DifficultyInfo[Difficulty].MaxConcurrentExaflares) { return; }

        var seed = RngSeed;
        unchecked
        {
            seed += ExaflareRowsSpawned * 69420;
        }
        var random = new Random(seed);

        int randVal;
        if (!onlyRelativeCardinal)
        {
            randVal = random.Next(7);
            if (randVal >= excludeAngle) { randVal++; }
        } else
        {
            randVal = (excludeAngle + random.Next(1, 4) * 2) % 8;
        }

        CalculateExaPosition(random, randVal);
    }

    private void CalculateExaPosition(Random random, int direction)
    {
        int deg = direction * 45;
        var X = Center.X - Radius * MathF.Sin(MathHelper.DegToRad(deg));
        var Z = Center.Z - Radius * MathF.Cos(MathHelper.DegToRad(deg));

        if (this.EntityManager.TryCreateEntity<ExaflareRow>(out var exaflareRow))
        {
            exaflareRow.Set(new Position(new Vector3(X, Center.Y, Z)))
                .Set(new Rotation(MathHelper.DegToRad(deg)))
                .Set(new ExaflareRow.SeededRandom(random));
            attacks.Add(exaflareRow);
        }

        ExaflareRowsSpawned++;
    }
}
