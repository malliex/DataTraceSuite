using OneStream.Shared.Common;
using OneStream.Shared.Wcf;

namespace DTS.Common;

internal sealed class DashboardTaskArgs
    (SessionInfo si, BRGlobals brGlobals, DashboardWorkspace workspace, DashboardExtenderArgs args)
{
    public SessionInfo Si { get; } = si;
    public BRGlobals BrGlobals { get; } = brGlobals;
    public DashboardWorkspace Workspace { get; } = workspace;
    public DashboardExtenderArgs Args { get; } = args;
}