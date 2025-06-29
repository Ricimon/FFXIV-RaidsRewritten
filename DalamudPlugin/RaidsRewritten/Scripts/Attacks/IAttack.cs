using System;
using Flecs.NET.Core;

namespace RaidsRewritten.Scripts.Attacks;

public interface IAttack
{
    public Entity Create(World world);
}
