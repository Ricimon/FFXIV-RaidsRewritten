using RaidsRewritten.UI.Presenter;
using RaidsRewritten.UI.View;

namespace RaidsRewritten;

public class EffectsRendererPresenter : IPluginUIPresenter
{
    public IPluginUIView View => this.view;

    private readonly EffectsRenderer view;

    public EffectsRendererPresenter(EffectsRenderer view)
    {
        this.view = view;
    }

    public void SetupBindings()
    {

    }

}
