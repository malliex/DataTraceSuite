using System;

using DTS.ServiceFactory.Services;

using OneStream.Shared.Common;
using OneStream.Shared.Wcf;

using OneStreamWorkspacesApi;

// ReSharper disable once CheckNamespace
namespace Workspace.__WsNamespacePrefix.__WsAssemblyName;

public class ServiceFactory : IWsAssemblyServiceFactory
{
    public IWsAssemblyServiceBase CreateWsAssemblyServiceInstance(
        SessionInfo si,
        BRGlobals brGlobals,
        DashboardWorkspace workspace,
        WsAssemblyServiceType wsAssemblyServiceType,
        string itemName)
    {
        try
        {
            return wsAssemblyServiceType switch
            {
                WsAssemblyServiceType.Dashboard => new DashboardService(),
                WsAssemblyServiceType.Component => new ComponentService(),
                WsAssemblyServiceType.DataSet => new DataSetService(),
                WsAssemblyServiceType.DynamicGrid => new DynamicGridService(),
                _ => null
            };
        }
        catch (Exception ex)
        {
            throw new XFException(si, ex);
        }
    }
}