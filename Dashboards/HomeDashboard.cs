using DTS.Common;
using DTS.Constants;
using DTS.Enums;
using DTS.Interfaces;

namespace DTS.Dashboards;

internal sealed class HomeDashboard(DashboardTaskArgs taskArgs) : IContentDashboard
{
    public string Name => DashboardNames.Home;
    public DashboardTaskArgs TaskArgs { get; } = taskArgs;
    public NavigationUnit NavUnit => NavigationUnit.Home;
}