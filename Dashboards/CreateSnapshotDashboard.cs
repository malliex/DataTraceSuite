using System;

using DTS.Common;
using DTS.Constants;
using DTS.Enums;
using DTS.Interfaces;
using DTS.Services;

using OneStream.Finance.Engine;
using OneStream.Shared.Common;
using OneStream.Shared.Wcf;

namespace DTS.Dashboards;

internal sealed class CreateSnapshotDashboard(DashboardTaskArgs taskArgs) : IContentDashboard
{
    public string Name => DashboardNames.Create;
    public DashboardTaskArgs TaskArgs { get; } = taskArgs;
    public NavigationUnit NavUnit => NavigationUnit.Create;

    public XFSelectionChangedTaskResult Import()
    {
        var tResult = new XFSelectionChangedTaskResult { IsOK = true, ShowMessageBox = true };

        var prms = TaskArgs.Args.SelectionChangedTaskInfo.CustomSubstVarsWithUserSelectedValues;

        //Documents/Public/Data Trace Suite/Import/ABC.zip
        var fileFullName = prms.XFGetValue(Parameters.PrmImportFile, string.Empty);

        if (string.IsNullOrWhiteSpace(fileFullName))
        {
            tResult.IsOK = false;
            tResult.Message = "Please select a file to import.";
            return tResult;
        }

        if (!CreateSnapshotService.TryValidateCsvFile(TaskArgs.Si, fileFullName, out var validationError))
        {
            tResult.IsOK = false;
            tResult.Message = "ERROR. Operation Failed." + Environment.NewLine + validationError;
            return tResult;
        }

        BRApi.Utilities.QueueDataMgmtSequence(
            TaskArgs.Si,
            TaskArgs.Workspace.WorkspaceID,
            Sequences.ImportSnapshot,
            prms);

        tResult.Message = "Snapshot import started in background.";
        return tResult;
    }

    public XFSelectionChangedTaskResult CreateSnapshot()
    {
        var result = new XFSelectionChangedTaskResult { IsOK = true, ShowMessageBox = true };

        var validationResult = ValidateParameters();

        if (validationResult.IsSuccess)
        {
            var selectedVars = TaskArgs.Args.SelectionChangedTaskInfo.CustomSubstVarsWithUserSelectedValues;

            BRApi.Utilities.QueueDataMgmtSequence(
                TaskArgs.Si,
                TaskArgs.Workspace.WorkspaceID,
                Sequences.CreateSnapshot,
                selectedVars);

            result.Message = "Snapshot creation started in background.";
            return result;
        }

        result.IsOK = false;
        result.Message = "ERROR. Operation Failed." + Environment.NewLine + validationResult;

        return result;
    }

    private OperationResult ValidateParameters()
    {
        var result = new OperationResult();

        var csVars = TaskArgs.Args.SelectionChangedTaskInfo.CustomSubstVarsWithUserSelectedValues;
        var cbName = csVars.XFGetValue(Parameters.PrmSelectCube, string.Empty);
        var etName = csVars.XFGetValue(Parameters.PrmSelectEntity, string.Empty);
        var snName = csVars.XFGetValue(Parameters.PrmSelectScenario, string.Empty);
        var tmFilter = csVars.XFGetValue(Parameters.PrmSelectTimeFilter, string.Empty);

        result.AddMessage(ValidateCube(cbName));
        result.AddMessage(ValidateEntity(etName));
        result.AddMessage(ValidateScenario(snName));
        result.AddMessage(ValidateTimeFilter(tmFilter));

        return result;
    }

    private string ValidateCube(string cbName)
    {
        if (string.IsNullOrWhiteSpace(cbName))
            return "Cube name is empty.";

        var cbInfo = BRApi.Finance.Cubes.GetCubeInfo(TaskArgs.Si, cbName);
        return cbInfo is null ? $"Invalid Cube name: '{cbName}'" : null;
    }

    private string ValidateEntity(string etName)
    {
        if (string.IsNullOrWhiteSpace(etName))
            return "Entity name is empty.";

        var etMem = BRApi.Finance.Members.GetMember(TaskArgs.Si, DimType.Entity.Id, etName);
        return etMem is null ? $"Invalid Entity name: '{etName}'" : null;
    }

    private string ValidateScenario(string snName)
    {
        if (string.IsNullOrWhiteSpace(snName))
            return "Scenario name is empty.";

        var snMem = BRApi.Finance.Members.GetMember(TaskArgs.Si, DimType.Scenario.Id, snName);
        return snMem is null ? $"Invalid Scenario name: '{snName}'" : null;
    }

    private string ValidateTimeFilter(string timeFilter)
    {
        if (string.IsNullOrWhiteSpace(timeFilter))
            return "Time Filter is empty.";

        var scanner = new MemberFilterScanner();
        scanner.Scan(TaskArgs.Si, DimType.Time, timeFilter);

        return scanner.HasScriptError
            ? $"Invalid Time Filter: '{timeFilter}' ({scanner.GetFirstScriptError()})"
            : null;
    }
}