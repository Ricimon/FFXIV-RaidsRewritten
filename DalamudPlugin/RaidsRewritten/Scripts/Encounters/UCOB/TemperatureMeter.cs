using System;
using System.Collections.Generic;
using System.Numerics;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

internal class TemperatureMeter : Mechanic
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
            9900, new HeatData { HeatValue = 50.0f, ID = 9900, DelaySeconds = 0.8f } //Fireball (Twin)
        },
        {
            9901, new HeatData { HeatValue = 20.0f, ID = 9901, DelaySeconds = 0.8f } //Liquid hell
        },
        {
            9917, new HeatData { HeatValue = 50.0f, ID = 9917, DelaySeconds = 0.8f } //Thermeonic Beam
        },
        {
            9925, new HeatData { HeatValue = 50.0f, ID = 9925, DelaySeconds = 0.8f } //Fireball (Firehorn)
        },
        {
            9926, new HeatData { HeatValue = -50.0f, ID = 9926, DelaySeconds = 0.8f } //Iceball (Iceclaw)
        },
        {
            9937, new HeatData { HeatValue = 100.0f, ID = 9937, DelaySeconds = 0.8f } //Seventh Umbral Era
        },
        {
            9938, new HeatData { HeatValue = 20.0f, ID = 9938, DelaySeconds = 0.8f } //Calamitous Flame
        },
        {
            9939, new HeatData { HeatValue = 20.0f, ID = 9939, DelaySeconds = 0.8f } //Calamitous Flame (Final hit)
        },
        { 
            9940, new HeatData { HeatValue = 20.0f, ID = 9940, DelaySeconds = 0.8f } //Flare Breath
        },
        { 
            9942, new HeatData { HeatValue = 50.0f, ID = 9942, DelaySeconds = 0.8f} //Gigaflare
        },
        { 
            9962, new HeatData { HeatValue = 20.0f, ID = 9962, DelaySeconds = 0.8f } //Ahk Morn (1st hit)
        },
        { 
            9963, new HeatData { HeatValue = 20.0f, ID = 9963, DelaySeconds = 0.8f } //Ahk Morn (2nd+ Hit)
        },
        {
            9964, new HeatData { HeatValue = 20.0f, ID = 9964, DelaySeconds = 0.8f } //Morn Afah
        }
    };

    public override void Reset()
    {

    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        try
        {
            if (set.Action == null) { return; }
            if (set.Source == null) { return; }
            if (!HeatDict.TryGetValue(set.Action.Value.RowId, out var HeatData)) { return; }

            var localPlayer = Dalamud.ClientState.LocalPlayer;
            if (localPlayer == null) { return; }
            foreach (var targetEffects in set.TargetEffects)
            {
                if (targetEffects.TargetID == localPlayer.GameObjectId)
                {
                    using var q = World.Query<Player.Component>();
                    q.Each((Entity e, ref Player.Component pc) =>
                    {
                        Temperature.HeatChangedEvent(e, HeatData.HeatValue, 0, HeatData.ID);
                    });
                    return;
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error(e.ToStringFull());
        }
    }
}