using System;

using DTS.Common;
using DTS.Dashboards;
using DTS.Services;

using OneStream.Shared.Common;
using OneStream.Shared.Wcf;

using OneStreamWorkspacesApi.V800;

namespace DTS.ServiceFactory.Services;

public class DashboardService : IWsasDashboardV800
{
    public XFLoadDashboardTaskResult ProcessLoadDashboardTask(
        SessionInfo si,
        BRGlobals brGlobals,
        DashboardWorkspace workspace,
        DashboardExtenderArgs args)
    {
        try
        {
            if (!BRApi.Security.Authorization.IsUserInAdminGroup(si))
                throw new XFException($"ERROR{Environment.NewLine}You are not authorized to access this Dashboard.");

            if (brGlobals == null || workspace == null || args?.LoadDashboardTaskInfo == null)
                return null;

            var taskArgs = new DashboardTaskArgs(si, brGlobals, workspace, args);

            return args.FunctionName switch
            {
                "OnLoadMainDashboard" => DatabaseService.SolutionTableExist(si)
                    ? new SnapshotsDashboard(taskArgs).OnLoad()
                    : new InstallDashboard(taskArgs).OnLoad(),
                _ => null
            };
        }
        catch (Exception ex)
        {
            throw new XFException(si, ex);
        }
    }
}