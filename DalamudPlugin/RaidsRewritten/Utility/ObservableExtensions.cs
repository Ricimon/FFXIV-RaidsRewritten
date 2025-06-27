using System;
using System.Reactive.Disposables;

namespace RaidsRewritten.Extensions;

public static class ObservableExtensions
{
    public static void DisposeWith(this IDisposable disposable, CompositeDisposable disposables) => 
        disposables.Add(disposable);
}
