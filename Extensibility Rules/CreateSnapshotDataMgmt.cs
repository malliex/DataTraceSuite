using System;
using System.Linq;

using DTS.Common;
using DTS.Constants;
using DTS.Services;

using OneStream.Shared.Common;
using OneStream.Shared.Database;
using OneStream.Shared.Wcf;

// ReSharper disable once CheckNamespace
namespace Workspace.DTS.DTS.BusinessRule.Extender.CreateSnapshotDataMgmt;

public class MainClass
{
    public object Main(SessionInfo si, BRGlobals globals, object api, ExtenderArgs args)
    {
        try
        {
            if (args.FunctionType != ExtenderFunctionType.ExecuteDataMgmtBusinessRuleStep)
                return null;

            var csVars = args.NameValuePairs;
            var appName = string.Concat(si.AppName.Where(c => !char.IsWhiteSpace(c)));
            var cbName = csVars.XFGetValue(Parameters.PrmSelectCube);
            var etName = csVars.XFGetValue(Parameters.PrmSelectEntity);
            var snName = csVars.XFGetValue(Parameters.PrmSelectScenario);
            var tmFilter = csVars.XFGetValue(Parameters.PrmSelectTimeFilter);
            var notes = csVars.XFGetValue(Parameters.PrmSelectNotes);

            SnapshotCreateOptions options = new(appName, cbName, etName, snName, tmFilter);
            var result = CreateSnapshotService.CreateSnapshot(si, options, notes, args);
            return !result.IsSuccess ? throw new Exception(result.ToString()) : null;
        }
        catch (Exception ex)
        {
            throw ErrorHandler.LogWrite(si, new XFException(si, ex));
        }
    }
}