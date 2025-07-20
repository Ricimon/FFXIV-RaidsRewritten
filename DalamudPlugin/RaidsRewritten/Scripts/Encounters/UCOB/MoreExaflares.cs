using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.MathHelpers;
using Flecs.NET.Core;
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
    private readonly List<uint> attackIds = [
        9914, // adds megaflare?
        9925, // fireball
        9941, // flatten
        9942, // gigaflare
        // exaflare
    ];
    private const uint NeurolinkDataId = 0x1E88FF;

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
        if (!attackIds.Contains(set.Action.Value.RowId)) { return; }

        RandomExaflareRow();
    }


    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
        if (newObject.DataId != NeurolinkDataId) { return; }

        RandomExaflareRow();
    }

    private void RandomExaflareRow()
    {
        int randVal = random.Next(8);
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
