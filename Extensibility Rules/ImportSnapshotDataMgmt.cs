using System;

using DTS.Constants;
using DTS.Services;

using OneStream.Shared.Common;
using OneStream.Shared.Database;
using OneStream.Shared.Wcf;

// ReSharper disable once CheckNamespace
namespace Workspace.DTS.DTS.BusinessRule.Extender.ImportSnapshotDataMgmt;

public class MainClass
{
    public object Main(SessionInfo si, BRGlobals globals, object api, ExtenderArgs args)
    {
        try
        {
            if (args.FunctionType != ExtenderFunctionType.ExecuteDataMgmtBusinessRuleStep)
                return null;

            var csVars = args.NameValuePairs;
            var fileFullName = csVars.XFGetValue(Parameters.PrmImportFile);

            var result = CreateSnapshotService.CreateSnapshotFromCsv(si, fileFullName, args);
            return !result.IsSuccess ? throw new Exception(result.ToString()) : null;
        }
        catch (Exception ex)
        {
            throw ErrorHandler.LogWrite(si, new XFException(si, ex));
        }
    }
}