using System.Collections.Generic;
using System.IO;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
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
        using var array = objectTable.AsValueEnumerable().Where(go => go.ObjectKind == ObjectKind.Player).OfType<IPlayerCharacter>().ToArrayPool();
        return array.AsEnumerable();
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
}
