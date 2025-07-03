using RaidsRewritten.UI.Presenter;
using RaidsRewritten.UI.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
