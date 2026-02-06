using RaidsRewritten.Memory;
using System;
using System.Collections.Generic;
using System.Text;

namespace RaidsRewritten;

public class StatusManager(StatusCommonProcessor statusCommonProcessor, StatusCustomProcessor statusCustomProcessor)
{
    private readonly StatusCommonProcessor statusCommonProcessor = statusCommonProcessor;
    private readonly StatusCustomProcessor statusCustomProcessor = statusCustomProcessor;
}
