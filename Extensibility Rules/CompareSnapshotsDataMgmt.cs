using System;

using DTS.Constants;
using DTS.Enums;
using DTS.Services;

using OneStream.Shared.Common;
using OneStream.Shared.Database;
using OneStream.Shared.Wcf;

// ReSharper disable once CheckNamespace
namespace Workspace.DTS.DTS.BusinessRule.Extender.CompareSnapshotsDataMgmt;

public class MainClass
{
    public object Main(SessionInfo si, BRGlobals globals, object api, ExtenderArgs args)
    {
        try
        {
            if (args.FunctionType != ExtenderFunctionType.ExecuteDataMgmtBusinessRuleStep)
                return null;

            var csVars = args.NameValuePairs;
            var snapIdA = csVars.XFGetValue(Parameters.PrmAId).XFConvertToInt();
            var snapIdB = csVars.XFGetValue(Parameters.PrmBId).XFConvertToInt();

            BRApi.TaskActivity.UpdateRunningTaskActivityAndCheckIfCanceled(si, args, "Comparing Snapshots", 33m);

            var compResult = CompareSnapshotsService.GetComparisonResult(si, snapIdA, snapIdB);

            // Snapshot IDs + millisecond-precision timestamp keep concurrent
            // comparisons (a supported scenario) from overwriting each other's
            // file, and make the file identifiable without opening it.
            var fileName = $"DTS_{snapIdA}_vs_{snapIdB}_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}";
            CsvService.SaveDataFrameAsCsv(si, compResult, FileType.Comparison, fileName);

            BRApi.TaskActivity.UpdateRunningTaskActivityAndCheckIfCanceled(si, args, "Completed", 77m);
            return null;
        }
        catch (Exception ex)
        {
            throw ErrorHandler.LogWrite(si, new XFException(si, ex));
        }
    }
}