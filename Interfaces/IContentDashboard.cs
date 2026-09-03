using DTS.Common;
using DTS.Enums;

namespace DTS.Interfaces;

internal interface IContentDashboard
{
    public string Name { get; }
    public DashboardTaskArgs TaskArgs { get; }
    public NavigationUnit NavUnit { get; }
}