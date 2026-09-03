using System;

using DTS.Constants;
using DTS.Enums;
using DTS.Services;

using OneStream.Shared.Common;
using OneStream.Shared.Database;
using OneStream.Shared.Wcf;

// ReSharper disable once CheckNamespace
namespace Workspace.DTS.DTS.BusinessRule.Extender.ExtractSnapshotDataMgmt;

public class MainClass
{
    public object Main(SessionInfo si, BRGlobals globals, object api, ExtenderArgs args)
    {
        try
        {
            if (args.FunctionType != ExtenderFunctionType.ExecuteDataMgmtBusinessRuleStep)
                return null;

            var snapId = args.NameValuePairs.XFGetValue(Parameters.PrmSelectSnapshot).XFConvertToInt();
            var fileName = args.NameValuePairs.XFGetValue("fileName");

            BRApi.TaskActivity.UpdateRunningTaskActivityAndCheckIfCanceled(si, args, "Extracting Snapshot", 33m);
            var dt = ExtractSnapshotService.GetExtractResult(si, snapId);
            CsvService.SaveDataFrameAsCsv(si, dt, FileType.Export, fileName);

            BRApi.TaskActivity.UpdateRunningTaskActivityAndCheckIfCanceled(si, args, "Completed", 77m);

            return null;
        }
        catch (Exception ex)
        {
            throw ErrorHandler.LogWrite(si, new XFException(si, ex));
        }
    }
}