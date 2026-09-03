using DTS.Common;
using DTS.Constants;
using DTS.Enums;
using DTS.Interfaces;

namespace DTS.Dashboards;

internal sealed class AuditDashboard(DashboardTaskArgs taskArgs) : IContentDashboard
{
    public string Name => DashboardNames.Audit;
    public DashboardTaskArgs TaskArgs { get; } = taskArgs;
    public NavigationUnit NavUnit => NavigationUnit.Audit;
}