using DTS.Common;
using DTS.Constants;
using DTS.Enums;
using DTS.Interfaces;

namespace DTS.Dashboards;

internal sealed class SpreadsheetDashboard(DashboardTaskArgs taskArgs) : IContentDashboard
{
    public string Name => DashboardNames.Spreadsheet;
    public DashboardTaskArgs TaskArgs { get; } = taskArgs;
    public NavigationUnit NavUnit => NavigationUnit.Spreadsheet;
}