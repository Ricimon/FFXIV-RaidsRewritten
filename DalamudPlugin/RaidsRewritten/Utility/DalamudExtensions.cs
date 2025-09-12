using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace RaidsRewritten.Extensions;

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

    public static string? GetLocalPlayerFullName(this IClientState clientState)
    {
        var localPlayer = clientState.LocalPlayer;
        if (localPlayer == null)
        {
            return null;
        }
        return GetPlayerFullName(localPlayer);
    }

    public static IEnumerable<IPlayerCharacter> GetPlayers(this IObjectTable objectTable)
    {
        return objectTable.Where(go => go.ObjectKind == ObjectKind.Player).OfType<IPlayerCharacter>();
    }

    public static string GetResourcePath(this IDalamudPluginInterface pluginInterface, string fileName)
    {
        var resourcesDir = Path.Combine(pluginInterface.AssemblyLocation.Directory?.FullName!, "Resources");
        return Path.Combine(resourcesDir, fileName);
    }

    public static bool HasTranscendance(this IBattleChara battleChara)
    {
        // 418 = Transcendance status ID
        return battleChara.StatusList.Any(s => s.StatusId == 418);
    }
}
