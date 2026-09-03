using DTS.Common;
using DTS.Constants;
using DTS.Enums;
using DTS.Extensions;
using DTS.Interfaces;
using DTS.Services;

using OneStream.Shared.Common;
using OneStream.Shared.Wcf;

namespace DTS.Dashboards;

internal sealed class SettingsDashboard(DashboardTaskArgs taskArgs) : IContentDashboard
{
    public string Name => DashboardNames.Settings;
    public DashboardTaskArgs TaskArgs { get; } = taskArgs;
    public NavigationUnit NavUnit => NavigationUnit.Settings;

    public XFSelectionChangedTaskResult ApplyTimeZone()
    {
        var tResult = new XFSelectionChangedTaskResult();

        var prms = TaskArgs.Args.SelectionChangedTaskInfo.CustomSubstVarsWithUserSelectedValues;
        var selected = prms.XFGetValue(Parameters.PrmSelectTimeZones, "-7");
        BRApi.Dashboards.Parameters.SetLiteralParameterValue(
            TaskArgs.Si,
            false,
            TaskArgs.Workspace.WorkspaceID,
            Parameters.PrmTimeZone,
            selected);

        return tResult;
    }

    public XFSelectionChangedTaskResult Uninstall()
    {
        var tResult = new XFSelectionChangedTaskResult
        {
            ChangeCustomSubstVarsInDashboard = true,
            ChangeCustomSubstVarsInLaunchedDashboard = true,
            ChangeSelectionChangedUIActionInDashboard = true,
            ModifiedSelectionChangedUIActionInfo = new XFSelectionChangedUIActionInfo
            {
                SelectionChangedUIActionType =
                    XFSelectionChangedUIActionType.OpenDialogWithNoButtonsApplyChangesAndRefresh,
                DashboardForDialog = DashboardNames.PopupUninstall,
                DashboardsToRedraw = DashboardNames.OnePlace,
                DlgInitialParameterValues = $"{Parameters.PrmOnePlaceDbrd}=[{DashboardNames.Layout}]"
            }
        };

        return tResult;
    }

    public XFSelectionChangedTaskResult ConfirmUninstall()
    {
        var tResult = new XFSelectionChangedTaskResult
        {
            ChangeCustomSubstVarsInDashboard = true,
            ChangeSelectionChangedUIActionInDashboard = true,
            ModifiedSelectionChangedUIActionInfo = new XFSelectionChangedUIActionInfo
            {
                SelectionChangedUIActionType = XFSelectionChangedUIActionType.CloseDialog,
                DashboardsToHide = DashboardNames.PopupUninstall
            }
        };

        var uninstallResult = UninstallService.Uninstall(TaskArgs.Si);

        if (uninstallResult.IsSuccess)
        {
            tResult.ModifiedCustomSubstVars.InsertOrAppend(Parameters.PrmOnePlaceDbrd, DashboardNames.Install);
            return tResult;
        }

        tResult.ShowMessageBox = true;
        tResult.Message = uninstallResult.ToString();
        return tResult;
    }
}