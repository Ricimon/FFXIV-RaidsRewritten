using System;
using System.Collections.Generic;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Conditions;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class TemperatureControl : Mechanic
{
    private struct HeatData
    {
        public float HeatValue;
        public int ID;
        public float DelaySeconds;
    }

    private readonly Dictionary<uint, HeatData> HeatDict = new Dictionary<uint, HeatData>
    {
        {
            9900, new HeatData { HeatValue = 50.0f, ID = 9900, DelaySeconds = 0.6f } //Fireball (Twin)
        },
        {
            9901, new HeatData { HeatValue = 20.0f, ID = 9901, DelaySeconds = 1.1f } //Liquid hell
        },
        {
            9917, new HeatData { HeatValue = 50.0f, ID = 9917, DelaySeconds = 0.8f } //Thermeonic Beam
        },
        {
            9925, new HeatData { HeatValue = 50.0f, ID = 9925, DelaySeconds = 1.0f } //Fireball (Firehorn)
        },
        {
            9926, new HeatData { HeatValue = -50.0f, ID = 9926, DelaySeconds = 1.0f } //Iceball (Iceclaw)
        },
        {
            9937, new HeatData { HeatValue = 100.0f, ID = 9937, DelaySeconds = 0.1f } //Seventh Umbral Era
        },
        {
            9938, new HeatData { HeatValue = 20.0f, ID = 9938, DelaySeconds = 0.2f } //Calamitous Flame
        },
        {
            9939, new HeatData { HeatValue = 20.0f, ID = 9939, DelaySeconds = 0.2f } //Calamitous Blaze (Final hit)
        },
        { 
            9940, new HeatData { HeatValue = 20.0f, ID = 9940, DelaySeconds = 0.9f } //Flare Breath
        },
        { 
            9942, new HeatData { HeatValue = 50.0f, ID = 9942, DelaySeconds = 3.6f} //Gigaflare
        },
        { 
            9962, new HeatData { HeatValue = 20.0f, ID = 9962, DelaySeconds = 0.9f } //Ahk Morn (1st hit)
        },
        { 
            9963, new HeatData { HeatValue = 20.0f, ID = 9963, DelaySeconds = 0.9f } //Ahk Morn (2nd+ Hit)
        },
        {
            9964, new HeatData { HeatValue = 20.0f, ID = 9964, DelaySeconds = 0.9f } //Morn Afah
        }
        //9970 Flames of Rebirth (20s till targetable)
    };

    private readonly List<Entity> attacks = [];
    private int AfahMultiplier = 1;

    public override void Reset()
    {
        foreach (var attack in this.attacks)
        {
            attack.Destruct();
        }
        this.attacks.Clear();

        using var q = World.Query<Player.Component>();
        q.Each((Entity e, ref Player.Component pc) =>
        {
            Temperature.SetTemperature(e);
        });
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
        try
        {
            if (set.Action == null) { return; }
            if (set.Source == null) { return; }
            if (!HeatDict.TryGetValue(set.Action.Value.RowId, out var Heat)) { return; }
            
            var localPlayer = Dalamud.ClientState.LocalPlayer;
            if (localPlayer == null) { return; }

            var da = DelayedAction.Create(this.World, () =>     
            {
                foreach (var targetEffects in set.TargetEffects)
                {
                    if (targetEffects.TargetID == localPlayer.GameObjectId)
                    {
                        using var q = World.Query<Player.Component>();
                        q.Each((Entity e, ref Player.Component pc) =>
                        {
                            float HeatDelta = Heat.HeatValue;
                            if (Heat.ID == 9964)
                            {
                                HeatDelta *= AfahMultiplier++;

                            }
                            Temperature.HeatChangedEvent(e, HeatDelta, 0, Heat.ID);
                        });
                        return;
                    }
                }
            }, Heat.DelaySeconds);
            this.attacks.Add(da);
        }
        catch (Exception e)
        {
            Logger.Error(e.ToStringFull());
        }
    }
}