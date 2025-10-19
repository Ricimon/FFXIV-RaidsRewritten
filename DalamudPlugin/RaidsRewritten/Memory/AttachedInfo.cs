// Adapted from https://github.com/PunishXIV/Splatoon/blob/main/Splatoon/Memory/AttachedInfo.cs
// 2528d6a
using System;
using System.Collections.Generic;
using System.Text;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using ECommons;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using RaidsRewritten.Log;
using RaidsRewritten.Structures;
using RaidsRewritten.Utility;
using Reloaded.Hooks.Definitions.X64;
using ZLinq;

namespace RaidsRewritten.Memory;

#nullable enable
public static unsafe class AttachedInfo
{
    private delegate nint GameObject_ctor(nint obj);
    private static Hook<GameObject_ctor>? GameObject_ctor_hook = null;
    public static Dictionary<nint, CachedCastInfo> CastInfos = [];
    public static Dictionary<nint, List<CachedObjectEffectInfo>> ObjectEffectInfos = [];
    public static Dictionary<nint, Dictionary<string, VFXInfo>> VFXInfos = [];
    public static Dictionary<nint, List<CachedTetherInfo>> TetherInfos = [];
    private static HashSet<nint> Casters = [];

    [Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
    private delegate nint ActorVfxCreateDelegate2(char* a1, nint a2, nint a3, float a4, char a5, ushort a6, char a7);
    private static Hook<ActorVfxCreateDelegate2>? ActorVfxCreateHook;

    private static Action<uint, uint>? OnStartingCastCallback;
    private static Action<uint, string>? OnVFXSpawnCallback;
    private static ILogger? Logger;

    internal static void Init(
        ILogger logger,
        Action<uint, uint> onStartingCastCallback,
        Action<uint, string> onVFXSpawnCallback)
    {
        Logger = logger;
        OnStartingCastCallback = onStartingCastCallback;
        OnVFXSpawnCallback = onVFXSpawnCallback;

        GenericHelpers.Safe(delegate
        {
            GameObject_ctor_hook = PluginInitializer.GameInteropProvider.HookFromAddress<GameObject_ctor>(PluginInitializer.SigScanner.ScanText("48 8D 05 ?? ?? ?? ?? C7 81 ?? ?? ?? ?? ?? ?? ?? ?? 48 89 01 48 8B C1 C3"), GameObject_ctor_detour);
            GameObject_ctor_hook.Enable();
        });
        GenericHelpers.Safe(delegate
        {
            var actorVfxCreateAddress = PluginInitializer.SigScanner.ScanText("40 53 55 56 57 48 81 EC ?? ?? ?? ?? 0F 29 B4 24 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 0F B6 AC 24 ?? ?? ?? ?? 0F 28 F3 49 8B F8");
            ActorVfxCreateHook = PluginInitializer.GameInteropProvider.HookFromAddress<ActorVfxCreateDelegate2>(actorVfxCreateAddress, ActorVfxNewHandler);
            ActorVfxCreateHook.Enable();
        });
        PluginInitializer.Framework.Update += Tick;
    }


    internal static void Dispose()
    {
        PluginInitializer.Framework.Update -= Tick;
        if (GameObject_ctor_hook != null)
        {
            GameObject_ctor_hook.Disable();
            GameObject_ctor_hook.Dispose();
            GameObject_ctor_hook = null;
        }
        ActorVfxCreateHook?.Disable();
        ActorVfxCreateHook?.Dispose();
        CastInfos = null!;
        VFXInfos = null!;
        ObjectEffectInfos = null!;
    }

    private static nint ActorVfxNewHandler(char* a1, nint a2, nint a3, float a4, char a5, ushort a6, char a7)
    {
        try
        {
            var vfxPath = Dalamud.Memory.MemoryHelper.ReadString(new nint(a1), Encoding.ASCII, 256);
            if (!VFXInfos.ContainsKey(a2))
            {
                VFXInfos[a2] = [];
            }
            VFXInfos[a2][vfxPath] = new()
            {
                SpawnTime = Environment.TickCount64
            };
            var obj = PluginInitializer.ObjectTable.CreateObjectReference(a2)!;
            OnVFXSpawnCallback?.Invoke(obj.EntityId, vfxPath);
            //if (!Utils.BlacklistedVFX.Contains(vfxPath))
            //{
            //    if (obj is ICharacter c)
            //    {
            //        var targetText = c.AddressEquals(PluginInitializer.ClientState.LocalPlayer) ? "me" : (c is IPlayerCharacter pc ? pc.GetJob().ToString() : c.DataId.ToString() ?? "Unknown");
            //        var text = $"VFX {vfxPath} spawned on {targetText} npc id={c.NameId}, model id={c.Struct()->ModelContainer.ModelCharaId}, name npc id={c.NameId}, position={c.Position}, name={c.Name}";
            //        Logger?.Info(text);
            //    }
            //    else
            //    {
            //        var text = $"VFX {vfxPath} spawned on {obj.DataId} npc id={obj.Struct()->GetNameId()}, position={obj.Position}";
            //        Logger?.Info(text);
            //    }
            //}
        }
        catch (Exception e)
        {
            Logger?.Error(e.ToStringFull());
        }
        return ActorVfxCreateHook!.Original(a1, a2, a3, a4, a5, a6, a7);
    }

    public static bool TryGetVfx(this IGameObject go, out Dictionary<string, VFXInfo>? fx)
    {
        if (VFXInfos.ContainsKey(go.Address))
        {
            fx = VFXInfos[go.Address];
            return true;
        }
        fx = default;
        return false;
    }

    public static List<CachedTetherInfo> GetOrCreateTetherInfo(nint ptr)
    {
        if (TetherInfos.TryGetValue(ptr, out var list))
        {
            return list;
        }
        TetherInfos[ptr] = [];
        return TetherInfos[ptr];
    }

    public static List<CachedTetherInfo> GetOrCreateTetherInfo(Character* ptr) => GetOrCreateTetherInfo((nint)ptr);

    public static bool TryGetSpecificVfxInfo(this IGameObject go, string path, out VFXInfo info)
    {
        if (TryGetVfx(go, out var dict) && dict?.ContainsKey(path) == true)
        {
            info = dict[path];
            return true;
        }
        info = default;
        return false;
    }

    private static nint GameObject_ctor_detour(nint ptr)
    {
        CastInfos.Remove(ptr);
        Casters.Remove(ptr);
        VFXInfos.Remove(ptr);
        ObjectEffectInfos.Remove(ptr);
        TetherInfos.Remove(ptr);
        return GameObject_ctor_hook!.Original(ptr);
    }
    private static void Tick(object _)
    {
        foreach (var x in PluginInitializer.ObjectTable)
        {
            if (x is IBattleChara b)
            {
                bool isCasting;
                try
                {
                    isCasting = b.Struct()->GetCastInfo() != null && b.IsCasting;
                }
                catch
                {
                    // Ignore invalid BattleChara objects that exist during cutscenes
                    continue;
                }

                if (isCasting)
                {
                    if (!Casters.Contains(b.Address))
                    {
                        CastInfos[b.Address] = new(b.CastActionId, Environment.TickCount64 - (long)(b.CurrentCastTime * 1000));
                        Casters.Add(b.Address);
                        //string text;
                        //if (P.Config.LogPosition)
                        //if (true)
                        //{
                        //    text = $"{b.Name} ({x.Position}) starts casting {b.CastActionId} ({b.NameId}>{b.CastActionId})";
                        //}
                        //else
                        //{
                        //    text = $"{b.Name} starts casting {b.CastActionId} ({b.NameId}>{b.CastActionId})";
                        //}
                        //ScriptingProcessor.OnStartingCast(b.EntityId, b.CastActionId);
                        OnStartingCastCallback?.Invoke(b.EntityId, b.CastActionId);
                        //Logger?.Info(text);
                        //P.ChatMessageQueue.Enqueue(text);
                        //if (P.Config.Logging)
                        //{
                        //    Logger.Log(text);
                        //    if (b is IBattleNpc) P.LogWindow.Log(text);
                        //}
                    }
                }
                else
                {
                    if (Casters.Contains(b.Address))
                    {
                        Casters.Remove(b.Address);
                    }
                }
            }
        }
    }

    public static bool TryGetCastTime(nint ptr, IEnumerable<uint> castId, out float castTime)
    {
        if (CastInfos.TryGetValue(ptr, out var info))
        {
            if (castId.AsValueEnumerable().Contains(info.ID))
            {
                castTime = (float)(Environment.TickCount64 - info.StartTime) / 1000f;
                return true;
            }
        }
        castTime = default;
        return false;
    }
}
