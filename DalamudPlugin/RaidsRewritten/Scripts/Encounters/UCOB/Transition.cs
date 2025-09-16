using System;
using System.Collections.Generic;
using System.Reactive;
using Dalamud.Plugin.Services;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Conditions;
namespace RaidsRewritten.Scripts.Encounters.UCOB;

internal class Transition : Mechanic
{
    private struct AddData
    {
        public int Kaliya;
        public int Melusine;
        public int[] Ads;
    }

    private readonly Dictionary<uint, AddData> Table = new Dictionary<uint, AddData>
    {
        { 
            0, new AddData { Kaliya = 0, Melusine = 7, Ads = new int[] { 2, 4, 6 } } 
        },
        {
            1, new AddData { Kaliya = 0, Melusine = 1, Ads = new int[] { 2, 4, 6 } }
        },
        {
            2, new AddData { Kaliya = 2, Melusine = 3, Ads = new int[] { 0, 4, 6 } }
        },
        {
            3, new AddData { Kaliya = 2, Melusine = 1, Ads = new int[] { 0, 4, 6 } }
        },
        {
            4, new AddData { Kaliya = 4, Melusine = 5, Ads = new int[] { 0, 2, 6 } }
        },
        {
            5, new AddData { Kaliya = 4, Melusine = 3, Ads = new int[] { 0, 2, 6 } }
        },
        {
            6, new AddData { Kaliya = 6, Melusine = 7, Ads = new int[] { 0, 2, 4 } }
        },
        {
            7, new AddData { Kaliya = 6, Melusine = 5, Ads = new int[] { 0, 2, 4 } }
        },
    };



    public override void OnActionEffectEvent(ActionEffectSet set)
    { 
    
    }
}
