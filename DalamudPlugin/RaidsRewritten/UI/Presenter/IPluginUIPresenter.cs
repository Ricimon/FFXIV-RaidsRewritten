using RaidsRewritten.UI.View;

namespace RaidsRewritten.UI.Presenter;

public interface IPluginUIPresenter
{
    IPluginUIView View { get; }

    void SetupBindings();
}
