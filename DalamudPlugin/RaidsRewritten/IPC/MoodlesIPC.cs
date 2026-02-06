using Dalamud.Plugin.Ipc;
using RaidsRewritten.Log;
using System;
using System.Collections.Generic;
using System.Text;

namespace RaidsRewritten.IPC;

public class MoodlesIPC
{
    private ILogger logger;
    private DalamudServices dalamudServices;

    private readonly ICallGateSubscriber<int> _moodlesVersion;
    public bool MoodlesPresent = false;

    public MoodlesIPC(DalamudServices dalamudServices, ILogger logger)
    {
        this.dalamudServices = dalamudServices;
        this.logger = logger;

        _moodlesVersion = this.dalamudServices.PluginInterface.GetIpcSubscriber<int>("Moodles.Version");

        CheckMoodles();
    }

    public bool CheckMoodles()
    {
        try
        {
            _moodlesVersion.InvokeFunc();
            MoodlesPresent = true;
            return true;
        } catch
        {

        }
        MoodlesPresent = false;
        return false;
    }
}
