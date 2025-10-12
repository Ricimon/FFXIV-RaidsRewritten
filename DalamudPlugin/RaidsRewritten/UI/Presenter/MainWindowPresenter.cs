using System;
using System.Reactive.Linq;
using RaidsRewritten.Log;
using RaidsRewritten.UI.View;
using Reactive.Bindings;

namespace RaidsRewritten.UI.Presenter;

public class MainWindowPresenter(
    MainWindow view,
    Configuration configuration,
    ILogger logger) : IPluginUIPresenter
{
    public IPluginUIView View => this.view;

    private readonly MainWindow view = view;
    private readonly Configuration configuration = configuration;
    private readonly ILogger logger = logger;

    public void SetupBindings()
    {
        BindVariables();
        BindActions();
    }

    private void BindVariables()
    {
        Bind(this.view.PrintLogsToChat,
            b => { this.configuration.PrintLogsToChat = b; this.configuration.Save(); }, this.configuration.PrintLogsToChat);
        Bind(this.view.MinimumVisibleLogLevel,
            i => { this.configuration.MinimumVisibleLogLevel = i; this.configuration.Save(); }, this.configuration.MinimumVisibleLogLevel);
    }

    private void BindActions()
    {
    }

    private void Bind<T>(
        IReactiveProperty<T> reactiveProperty,
        Action<T> dataUpdateAction,
        T initialValue)
    {
        if (initialValue != null)
        {
            reactiveProperty.Value = initialValue;
        }
        reactiveProperty.Subscribe(dataUpdateAction);
    }
}
