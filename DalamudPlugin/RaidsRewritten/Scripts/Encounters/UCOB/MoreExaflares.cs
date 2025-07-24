using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using Lumina.Excel.Sheets;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class MoreExaflares : Mechanic
{
    private static readonly Vector3 Center = new(0, 0, 0);
    private const float Radius = 20f;
    private readonly List<uint> ActionEffectIds = [
        9900, // fireball (twin)
        9901, // liquid hell
        9914, // adds megaflare
        9925, // fireball (firehorn)
        9942, // gigaflare
    ];

    private const uint NeurolinkDataId = 0x1E88FF;
    private int LiquidHellCounter = 0;
    private bool GoldenCanSpawnExa = false;

    private readonly List<Entity> attacks = [];

    private static readonly Random random = new();

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

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (!ActionEffectIds.Contains(set.Action.Value.RowId)) { return; }

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
        switch(action.RowId)
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
                RandomExaflareRow(Convert.ToInt32(angleNumber));
                break;
            default:
                return;
        }
    }

    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
        if (newObject.DataId != NeurolinkDataId) { return; }

        RandomExaflareRow();
    }

    private void RandomExaflareRow(int excludeAngle = -1)
    {
        int randVal;
        if (excludeAngle == -1)
        {
            randVal = random.Next(8);
        } else
        {
            randVal = random.Next(7);
            if (randVal >= excludeAngle) { randVal++; }
        }

        int deg = randVal * 45;
        var X = Center.X - Radius * MathF.Sin(MathHelper.DegToRad(deg));
        var Z = Center.Z - Radius * MathF.Cos(MathHelper.DegToRad(deg));

        if (this.AttackManager.TryCreateAttackEntity<ExaflareRow>(out var exaflareRow))
        {
            exaflareRow.Set(new Position(new Vector3(X, Center.Y, Z)))
                .Set(new Rotation(MathHelper.DegToRad(deg)));
            attacks.Add(exaflareRow);
        }
    }
}
