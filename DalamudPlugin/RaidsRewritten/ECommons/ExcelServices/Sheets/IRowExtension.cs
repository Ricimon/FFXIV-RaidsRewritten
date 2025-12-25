// https://github.com/NightmareXIV/ECommons/blob/master/ECommons/ExcelServices/Sheets/IRowExtension.cs
// 78da16b
using ECommons.DalamudServices;
using Lumina.Excel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.ExcelServices.Sheets;

public interface IRowExtension<out TExtension, in TBase> : IExcelRow<TExtension> where TBase : struct, IExcelRow<TBase> where TExtension : struct, IExcelRow<TExtension>, IRowExtension<TExtension, TBase>
{
    static virtual TExtension GetExtended(IExcelRow<TBase> baseRow)
    {
        return Svc.Data.GetExcelSheet<TExtension>().GetRow(baseRow.RowId);
    }
}