using System;
using System.Text;

namespace RaidsRewritten.Extensions;

public static class ExceptionExtensions
{
    public static string ToStringFull(this Exception e)
    {
        var str = new StringBuilder($"{e.Message}\n{e.StackTrace}");
        var inner = e.InnerException;
        for (var i = 1; inner !=null; i++)
        {
            str.Append($"\nAn inner exception ({i}) was thrown: {e.Message}\n{e.StackTrace}");
            inner = inner.InnerException;
        }
        return str.ToString();
    }
}
