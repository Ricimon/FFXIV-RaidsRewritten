using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using ZLinq;

namespace RaidsRewritten.Utility;

public static class DalamudExtensions
{
    public static string? GetPlayerFullName(this IPlayerCharacter playerCharacter)
    {
        string playerName = playerCharacter.Name.TextValue;
        var homeWorld = playerCharacter.HomeWorld;
        if (homeWorld.IsValid)
        {
            playerName += $"@{homeWorld.Value.Name.ExtractText()}";
        }

        return playerName;
    }

    public static IEnumerable<IPlayerCharacter> GetPlayers(this IObjectTable objectTable)
    {
        return objectTable.OfType<IPlayerCharacter>();
    }

    public static string GetResourcePath(this IDalamudPluginInterface pluginInterface, string fileName)
    {
        var resourcesDir = Path.Combine(pluginInterface.AssemblyLocation.Directory?.FullName!, "Resources");
        return Path.Combine(resourcesDir, fileName);
    }

    public static bool HasTranscendance(this IBattleChara battleChara)
    {
        // 418 = Transcendance status ID
        return battleChara.StatusList.AsValueEnumerable().Any(s => s.StatusId == 418);
    }

    public static void PrintSystemMessage(this IChatGui chatGui, string message, string? messageTag = null)
    {
        if (messageTag != null)
        {
            message = $"[{messageTag}] {message}";
        }
        chatGui.Print(new XivChatEntry
        {
            Message = message,
            Type = XivChatType.SystemMessage,
        });
    }

    public unsafe static GameObject* Native(this IGameObject go)
    {
        return (GameObject*)go.Address;
    }

    public unsafe static IGameObject? GetGameObjectByIndex(this IObjectTable objectTable, ushort index)
    {
        // ObjectTable.GetObjectAddress does not return the correct value for fake objects
        var obj = (BattleChara*)ClientObjectManager.Instance()->GetObjectByIndex(index);
        if (obj == null)
        {
            return null;
        }
        // ObjectTable.CreateObjectReference is an idempotent operation
        return objectTable.CreateObjectReference((nint)obj);
    }

    /// <summary>
    /// An extension of the IGameObject.IsValid() check but with additional checks against
    /// the native ClientObjectManager that properly checks the validity of fake GameObjects
    /// </summary>
    public unsafe static bool IsCompletelyValid(this IGameObject gameObject)
    {
        // Fake GameObjects always appear as valid even if they are deleted
        if (!gameObject.IsValid()) { return false; }
        var index = ClientObjectManager.Instance()->GetIndexByObject(gameObject.Native());
        if (index == 0xFFFFFFFF)
        {
            // Object is real
            return true;
        }
        var chara = ClientObjectManager.Instance()->GetObjectByIndex((ushort)index);
        return chara != null;
    }
}
