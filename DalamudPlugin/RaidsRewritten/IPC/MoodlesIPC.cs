using Dalamud.Plugin.Ipc;
using FFXIVClientStructs.FFXIV.Client.Game;
using RaidsRewritten.Log;
using System;
using System.Collections.Generic;
using System.Text;

namespace RaidsRewritten.IPC;

public class MoodlesIPC
{
    private Configuration configuration;
    private DalamudServices dalamudServices;
    private StatusManager statusManager;
    private ILogger logger;

    private readonly ICallGateSubscriber<int> _moodlesVersion;
    public bool MoodlesPresent = false;

    public MoodlesIPC(Configuration configuration, StatusManager statusManager,DalamudServices dalamudServices, ILogger logger)
    {
        
        this.dalamudServices = dalamudServices;
        this.configuration = configuration;
        this.statusManager = statusManager;
        this.logger = logger;

        _moodlesVersion = this.dalamudServices.PluginInterface.GetIpcSubscriber<int>("Moodles.Version");

        CheckMoodles();
    }

    public bool CheckMoodles()
    {
        try
        {
            _moodlesVersion.InvokeFunc();
            statusManager.HideAll();
            MoodlesPresent = true;
            this.configuration.UseLegacyStatusRendering = true;
            this.configuration.Save();
            return true;
        } catch
        {

        }
        MoodlesPresent = false;
        return false;
    }
}
