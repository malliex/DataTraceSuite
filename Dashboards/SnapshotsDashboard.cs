using System;
using System.Collections.Generic;
using System.Text;

using DTS.Common;
using DTS.Constants;
using DTS.Enums;
using DTS.Extensions;
using DTS.Interfaces;
using DTS.Services;

using Newtonsoft.Json;

using OneStream.Shared.Common;
using OneStream.Shared.Wcf;

namespace DTS.Dashboards;

internal sealed class SnapshotsDashboard(DashboardTaskArgs taskArgs) : IContentDashboard
{
    public string Name => DashboardNames.Snapshots;
    public DashboardTaskArgs TaskArgs { get; } = taskArgs;
    public NavigationUnit NavUnit => NavigationUnit.Snapshots;

    public XFLoadDashboardTaskResult OnLoad()
    {
        var loadTaskInfo = TaskArgs.Args.LoadDashboardTaskInfo;

        if (loadTaskInfo.Reason != LoadDashboardReasonType.Initialize ||
            loadTaskInfo.Action != LoadDashboardActionType.BeforeFirstGetParameters)
            return null;

        var tResult = new XFLoadDashboardTaskResult { ChangeCustomSubstVarsInDashboard = true };
        var csVars = new Dictionary<string, string>();
        AddDefaultOnLoadParamValues(csVars);

        tResult.ModifiedCustomSubstVars = csVars;
        return NavigationService.Load(this, tResult);
    }

    public XFSelectionChangedTaskResult Extract()
    {
        var tResult = new XFSelectionChangedTaskResult();

        var prms = TaskArgs.Args.SelectionChangedTaskInfo.CustomSubstVarsWithUserSelectedValues;
        var selectedId = prms.XFGetValue(Parameters.PrmSelectSnapshot, string.Empty);

        if (string.IsNullOrWhiteSpace(selectedId)
         || !int.TryParse(selectedId, out var snapId))
        {
            tResult.ShowMessageBox = true;
            tResult.Message = "Invalid snapshot ID. Please select a snapshot from grid.";
            return tResult;
        }

        var snapHeader = DatabaseService.GetSnapshotHeader(TaskArgs.Si, snapId);

        if (snapHeader == null)
        {
            tResult.ShowMessageBox = true;
            tResult.Message = "Invalid snapshot ID. Please select a snapshot from grid.";
            return tResult;
        }

        var snapName = snapHeader.SnapshotName;

        // No ".csv" here - FileService.CreateCsvFile appends the extension
        // itself (see CsvService.SaveComparisonResultToCsv for the same
        // convention on the compare path). Appending it here too would make
        // the business rule write "...csv.csv".
        var fileName = $"DTS_Extract_{snapName}";
        var csVars = new Dictionary<string, string>
        {
            { Parameters.PrmSelectSnapshot, selectedId }, { "fileName", fileName }
        };
        var ta = BRApi.Utilities.ExecuteDataMgmtSequence(
            TaskArgs.Si,
            TaskArgs.Workspace.WorkspaceID,
            "seq_Extract_Snapshot_DTS",
            csVars);

        if (BRApi.TaskActivity.GetTaskActivityItem(TaskArgs.Si, ta.UniqueID).TaskActivityStatus !=
            TaskActivityStatus.Completed)
        {
            tResult.ShowMessageBox = true;
            tResult.Message = "Snapshot Extraction Failed.";
            return tResult;
        }

        if (!FileService.TryGetFileFullName(TaskArgs.Si, FileType.Export, $"{fileName}.csv", out var fileFullName))
        {
            tResult.ShowMessageBox = true;
            tResult.Message = "Snapshot Extraction Failed.";
            return tResult;
        }

        tResult.ChangeSelectionChangedNavigationInDashboard = true;
        tResult.ModifiedSelectionChangedNavigationInfo = new XFSelectionChangedNavigationInfo
        {
            SelectionChangedNavigationType = XFSelectionChangedNavigationType.OpenFile,
            SelectionChangedNavigationArgs =
                $"FileSourceType=Application, UrlOrFullFileName=[{fileFullName}], OpenInXFPageIfPossible=False"
        };

        return tResult;
    }

    public XFSelectionChangedTaskResult SnapshotCompare()
    {
        var tResult = new XFSelectionChangedTaskResult
        {
            ChangeSelectionChangedUIActionInDashboard = true,
            ModifiedSelectionChangedUIActionInfo = new XFSelectionChangedUIActionInfo
            {
                SelectionChangedUIActionType = XFSelectionChangedUIActionType.Refresh, DashboardsToRedraw = Name
            },
            ShowMessageBox = true
        };

        var prms = TaskArgs.Args.SelectionChangedTaskInfo.CustomSubstVarsWithUserSelectedValues;
        var selectedAId = prms.XFGetValue(Parameters.PrmAId, string.Empty);
        var selectedBId = prms.XFGetValue(Parameters.PrmBId, string.Empty);

        var msg = new StringBuilder();

        if (string.IsNullOrWhiteSpace(selectedAId) || !int.TryParse(selectedAId, out _))
            msg.AppendLine("Please Select Snapshot A");

        if (string.IsNullOrWhiteSpace(selectedBId) || !int.TryParse(selectedBId, out _))
            msg.AppendLine("Please Select Snapshot B");

        if (msg.Length > 0)
        {
            tResult.Message = msg.ToString();
            return tResult;
        }

        BRApi.Utilities.QueueDataMgmtSequence(
            TaskArgs.Si,
            TaskArgs.Workspace.WorkspaceID,
            Sequences.CompareSnapshots,
            prms);

        tResult.Message = "Comparison Started in the background";

        return tResult;
    }

    public XFSelectionChangedTaskResult SnapshotDelete()
    {
        var tResult = new XFSelectionChangedTaskResult();

        var prms = TaskArgs.Args.SelectionChangedTaskInfo.CustomSubstVarsWithUserSelectedValues;
        var selectedId = prms.XFGetValue(Parameters.PrmSelectSnapshot, string.Empty);

        if (string.IsNullOrWhiteSpace(selectedId)
         || !int.TryParse(selectedId, out var snapId))
        {
            tResult.ShowMessageBox = true;
            tResult.Message = "Invalid snapshot ID. Please select a snapshot from grid.";
            return tResult;
        }

        var snapHeader = DatabaseService.GetSnapshotHeader(TaskArgs.Si, snapId);

        if (snapHeader == null)
        {
            tResult.ShowMessageBox = true;
            tResult.Message = "Invalid snapshot ID. Please select a snapshot from grid.";
            return tResult;
        }

        var snapName = snapHeader.SnapshotName;

        tResult.ChangeSelectionChangedUIActionInDashboard = true;
        tResult.ModifiedCustomSubstVarsForLaunchedDashboard = new Dictionary<string, string>
        {
            { Parameters.PrmSnapshotDeleteName, snapName }, { Parameters.PrmSelectSnapshot, snapId.ToString() }
        };

        tResult.ChangeCustomSubstVarsInLaunchedDashboard = true;
        tResult.ModifiedSelectionChangedUIActionInfo = new XFSelectionChangedUIActionInfo
        {
            SelectionChangedUIActionType = XFSelectionChangedUIActionType.OpenDialogWithNoButtonsAndRefresh,
            DashboardsToRedraw = Name,
            DashboardForDialog = DashboardNames.PopupSnapshots
        };

        return tResult;
    }

    public XFSelectionChangedTaskResult ConfirmDeleteSnapshot()
    {
        var tResult = new XFSelectionChangedTaskResult
        {
            ChangeSelectionChangedUIActionInDashboard = true,
            ModifiedSelectionChangedUIActionInfo = new XFSelectionChangedUIActionInfo
            {
                SelectionChangedUIActionType = XFSelectionChangedUIActionType.CloseDialog,
                DashboardsToHide = DashboardNames.PopupSnapshots
            }
        };

        var prms = TaskArgs.Args.SelectionChangedTaskInfo.CustomSubstVars;
        var snapId = prms.XFGetValue(Parameters.PrmSelectSnapshot, string.Empty).XFConvertToInt();

        var deleteResult = DeleteSnapshotService.DeleteSnapshot(TaskArgs.Si, snapId);
        if (deleteResult.IsSuccess)
            return tResult;

        tResult.ShowMessageBox = true;
        tResult.Message = deleteResult.ToString();
        return tResult;
    }

    public XFSelectionChangedTaskResult SnapshotSelectionChanged()
    {
        var tResult = new XFSelectionChangedTaskResult
        {
            ChangeCustomSubstVarsInDashboard = true,
            ChangeSelectionChangedUIActionInDashboard = true,
            ModifiedSelectionChangedUIActionInfo = new XFSelectionChangedUIActionInfo
            {
                SelectionChangedUIActionType = XFSelectionChangedUIActionType.Refresh,
                DashboardsToRedraw = DashboardNames.GridSnapshotsSelected
            }
        };

        var prms = TaskArgs.Args.SelectionChangedTaskInfo.CustomSubstVarsWithUserSelectedValues;
        var selectedId = prms.XFGetValue(Parameters.PrmSelectSnapshot, string.Empty);
        if (string.IsNullOrWhiteSpace(selectedId)
         || !int.TryParse(selectedId, out var snapId))
            return null;

        var snapshotHeader = DatabaseService.GetSnapshotHeader(TaskArgs.Si, snapId);

        if (snapshotHeader is null)
            throw new NullReferenceException("Snapshot header not found");

        var extOptions = JsonConvert.DeserializeObject<SnapshotCreateOptions>(snapshotHeader.ExtractOptions);

        var csVars = new Dictionary<string, string>
        {
            { Parameters.PrmXName, snapshotHeader.SnapshotName },
            { Parameters.PrmXUser, snapshotHeader.CreatedBy },
            { Parameters.PrmXDate, snapshotHeader.CreatedDateLocal.ToString("yyyy-MM-dd HH:mm:ss") },
            { Parameters.PrmXCube, extOptions.Cube },
            { Parameters.PrmXEntity, extOptions.Entity },
            { Parameters.PrmXScenario, extOptions.Scenario },
            { Parameters.PrmXTime, extOptions.TimeFilter }
        };

        tResult.ModifiedCustomSubstVars = csVars;

        return tResult;
    }

    public XFSelectionChangedTaskResult SetSnapshotFromSelection()
    {
        var tResult = new XFSelectionChangedTaskResult
        {
            ChangeCustomSubstVarsInDashboard = true,
            ChangeSelectionChangedUIActionInDashboard = true,
            ModifiedSelectionChangedUIActionInfo = new XFSelectionChangedUIActionInfo
            {
                SelectionChangedUIActionType = XFSelectionChangedUIActionType.Refresh,
                DashboardsToRedraw = $"{DashboardNames.GridSnapshotsA},{DashboardNames.GridSnapshotsB}"
            }
        };

        var prms = TaskArgs.Args.SelectionChangedTaskInfo.CustomSubstVarsWithUserSelectedValues;
        var selectedId = prms.XFGetValue(Parameters.PrmSelectSnapshot, string.Empty);

        if (string.IsNullOrWhiteSpace(selectedId))
        {
            tResult.ShowMessageBox = true;
            tResult.Message = "Please select a snapshot from grid";
            return tResult;
        }

        if (!int.TryParse(selectedId, out var snapId))
        {
            tResult.ShowMessageBox = true;
            tResult.Message = "Invalid snapshot ID";
            return tResult;
        }

        var side = TaskArgs.Args.NameValuePairs.XFGetValue("side", string.Empty);
        if (string.IsNullOrWhiteSpace(side))
            throw new ArgumentException("Missing 'side' parameter");

        var snapshotHeader = DatabaseService.GetSnapshotHeader(TaskArgs.Si, snapId);

        if (snapshotHeader is null)
            throw new NullReferenceException("Snapshot header not found");

        var extOptions = JsonConvert.DeserializeObject<SnapshotCreateOptions>(snapshotHeader.ExtractOptions);
        var snapshotInfo =
            $"{extOptions.Cube} - {extOptions.Entity} - {extOptions.Scenario} - {snapshotHeader.RowCount.ToString()} rows";

        // 'side' comes from dashboard config (the button that raised this
        // action), not free-text user input, so this interpolation - unlike
        // the fixed keys above - can't be a plain constant.
        var csVars = new Dictionary<string, string>
        {
            { $"prm_{side}_Title_DTS", snapshotHeader.SnapshotName },
            { $"prm_{side}_Info_DTS", snapshotInfo },
            { $"prm_{side}_Id_DTS", snapshotHeader.SnapshotId.ToString() }
        };

        tResult.ModifiedCustomSubstVars = csVars;

        return tResult;
    }

    private static void AddDefaultOnLoadParamValues(Dictionary<string, string> csVars)
    {
        csVars.InsertOrAppend(Parameters.PrmOnePlaceDbrd, DashboardNames.Layout);
        csVars.InsertOrAppend(Parameters.PrmSelectSnapshot, "");
        csVars.InsertOrAppend(Parameters.PrmATitle, "Not selected");
        csVars.InsertOrAppend(Parameters.PrmBTitle, "Not selected");
        csVars.InsertOrAppend(Parameters.PrmAInfo, "");
        csVars.InsertOrAppend(Parameters.PrmBInfo, "");
        csVars.InsertOrAppend(Parameters.PrmAId, "");
        csVars.InsertOrAppend(Parameters.PrmBId, "");
        csVars.InsertOrAppend(Parameters.PrmXName, "");
        csVars.InsertOrAppend(Parameters.PrmXUser, "");
        csVars.InsertOrAppend(Parameters.PrmXDate, "");
        csVars.InsertOrAppend(Parameters.PrmXCube, "");
        csVars.InsertOrAppend(Parameters.PrmXEntity, "");
        csVars.InsertOrAppend(Parameters.PrmXScenario, "");
        csVars.InsertOrAppend(Parameters.PrmXTime, "");
    }
}