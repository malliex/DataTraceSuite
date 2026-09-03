using System;

using DTS.Common;
using DTS.Dashboards;
using DTS.Services;

using OneStream.Shared.Common;
using OneStream.Shared.Wcf;

using OneStreamWorkspacesApi.V800;

namespace DTS.ServiceFactory.Services;

public class ComponentService : IWsasComponentV800
{
    public XFSelectionChangedTaskResult ProcessComponentSelectionChanged(
        SessionInfo si,
        BRGlobals brGlobals,
        DashboardWorkspace workspace,
        DashboardExtenderArgs args)
    {
        try
        {
            if (brGlobals == null || workspace == null || args?.SelectionChangedTaskInfo == null)
                return null;

            var taskArgs = new DashboardTaskArgs(si, brGlobals, workspace, args);

            return args.FunctionName switch
            {
                "Navigate" => NavigationService.Navigate(taskArgs),

                "Install" => new InstallDashboard(taskArgs).Install(),

                "CreateSnapshot" => new CreateSnapshotDashboard(taskArgs).CreateSnapshot(),
                "Import" => new CreateSnapshotDashboard(taskArgs).Import(),

                "ApplyTimeZone" => new SettingsDashboard(taskArgs).ApplyTimeZone(),
                "Uninstall" => new SettingsDashboard(taskArgs).Uninstall(),
                "ConfirmUninstall" => new SettingsDashboard(taskArgs).ConfirmUninstall(),

                "OpenComparisonViaSpreadsheet" => new ComparisonsDashboard(taskArgs).OpenViaSpreadsheet(),
                "DownloadComparison" => new ComparisonsDashboard(taskArgs).DownloadComparison(),
                "ClearSolutionFolder" => new ComparisonsDashboard(taskArgs).ClearSolutionFolder(),
                "ConfirmClearSolutionFolder" => new ComparisonsDashboard(taskArgs).ConfirmClearSolutionFolder(),

                "SelectSnapshot" => new SnapshotsDashboard(taskArgs).SetSnapshotFromSelection(),
                "DeleteSnapshot" => new SnapshotsDashboard(taskArgs).SnapshotDelete(),
                "ConfirmDeleteSnapshot" => new SnapshotsDashboard(taskArgs).ConfirmDeleteSnapshot(),
                "CompareSnapshots" => new SnapshotsDashboard(taskArgs).SnapshotCompare(),
                "SnapshotSelectionChanged" => new SnapshotsDashboard(taskArgs).SnapshotSelectionChanged(),
                "Extract" => new SnapshotsDashboard(taskArgs).Extract(),
                _ => null
            };
        }
        catch (Exception ex)
        {
            throw new XFException(si, ex);
        }
    }
}