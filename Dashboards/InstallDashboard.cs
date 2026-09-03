using System.Collections.Generic;

using DTS.Common;
using DTS.Constants;
using DTS.Enums;
using DTS.Interfaces;
using DTS.Services;

using OneStream.Shared.Wcf;

namespace DTS.Dashboards;

internal sealed class InstallDashboard(DashboardTaskArgs taskArgs) : IContentDashboard
{
    public string Name => DashboardNames.Install;
    public DashboardTaskArgs TaskArgs { get; } = taskArgs;
    public NavigationUnit NavUnit => NavigationUnit.Install;

    public XFLoadDashboardTaskResult OnLoad()
    {
        var loadTaskInfo = TaskArgs.Args.LoadDashboardTaskInfo;

        if (loadTaskInfo.Reason != LoadDashboardReasonType.Initialize ||
            loadTaskInfo.Action != LoadDashboardActionType.BeforeFirstGetParameters)
            return null;

        var tResult = new XFLoadDashboardTaskResult { ChangeCustomSubstVarsInDashboard = true };
        var csVars = new Dictionary<string, string> { { Parameters.PrmOnePlaceDbrd, DashboardNames.Install } };
        tResult.ModifiedCustomSubstVars = csVars;

        return tResult;
    }

    public XFSelectionChangedTaskResult Install()
    {
        var tResult = new XFSelectionChangedTaskResult
        {
            ChangeCustomSubstVarsInDashboard = true,
            ChangeSelectionChangedUIActionInDashboard = true,
            ModifiedCustomSubstVars = new Dictionary<string, string>(),
            ModifiedSelectionChangedUIActionInfo = new XFSelectionChangedUIActionInfo
            {
                SelectionChangedUIActionType = XFSelectionChangedUIActionType.OpenDialogWithNoButtonsAndRefresh,
                DashboardsToRedraw = DashboardNames.OnePlace,
                DashboardForDialog = DashboardNames.PopupInstall
            }
        };

        tResult.ModifiedCustomSubstVars.Add(Parameters.PrmOnePlaceDbrd, DashboardNames.Layout);

        InstallationService.Install(TaskArgs.Si);

        NavigationService.Navigate(new SnapshotsDashboard(TaskArgs), tResult);

        return tResult;
    }
}