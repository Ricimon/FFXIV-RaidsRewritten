using Dalamud.Game.Command;
using RaidsRewritten.UI.View;

namespace RaidsRewritten;

public sealed class CommandDispatcher(
    DalamudServices dalamud,
    MainWindow mainWindow) : IDalamudHook
{
    private const string commandName = "/raidsrewritten";
    private const string commandNameAlt = "/rr";

    public void HookToDalamud()
    {
        dalamud.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
        {
            HelpMessage = $"Open the {PluginInitializer.Name} window"
        });
        dalamud.CommandManager.AddHandler(commandNameAlt, new CommandInfo(OnCommand)
        {
            HelpMessage = $"Open the {PluginInitializer.Name} window"
        });
    }

    public void Dispose()
    {
        dalamud.CommandManager.RemoveHandler(commandName);
        dalamud.CommandManager.RemoveHandler(commandNameAlt);
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just display our main ui
        ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        mainWindow.Visible = true;
    }
}
