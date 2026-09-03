using System;
using System.Collections.Generic;
using System.Data;

using DTS.Constants;
using DTS.Services;

using OneStream.Shared.Common;
using OneStream.Shared.Wcf;

using OneStreamWorkspacesApi.V800;

namespace DTS.ServiceFactory.Services;

public class DynamicGridService : IWsasDynamicGridV800
{
    public XFDynamicGridGetDataResult GetDynamicGridData(
        SessionInfo si,
        BRGlobals brGlobals,
        DashboardWorkspace workspace,
        DashboardDynamicGridArgs args)
    {
        try
        {
            if (brGlobals == null || workspace == null || args == null)
                return null;

            return args.Component.Name switch
            {
                DashboardNames.DgrdSnapshots => GetSnapshotsSummary(si, args),
                DashboardNames.DgrdAudit => GetAudit(si, args),
                _ => null
            };
        }
        catch (Exception ex)
        {
            throw new XFException(si, ex);
        }
    }

    public XFDynamicGridSaveDataResult SaveDynamicGridData(
        SessionInfo si,
        BRGlobals brGlobals,
        DashboardWorkspace workspace,
        DashboardDynamicGridArgs args)
    {
        try
        {
            if (brGlobals == null || workspace == null || args == null)
                return null;

            var getDataResult = GetDynamicGridData(si, brGlobals, workspace, args);

            var result = new XFDynamicGridSaveDataResult
            {
                DataTable = getDataResult?.DataTable,
                RowFormats = getDataResult?.RowFormats,
                PageIndex = args.GetDataArgs.StartRowIndex / args.GetDataArgs.PageSize,
                IndexOfSelectedRowOnPage = 0,
                SaveDataTaskResult = new XFDynamicGridSaveDataTaskResult
                {
                    IsOK = true, ShowMessageBox = false, Message = ""
                }
            };
            return result;
        }
        catch (Exception ex)
        {
            throw new XFException(si, ex);
        }
    }

    private static XFDynamicGridGetDataResult GetAudit(SessionInfo si, DashboardDynamicGridArgs args)
    {
        var startRowIndex = args.GetDataArgs.StartRowIndex;
        var pageSize = args.GetDataArgs.PageSize;

        using var dbConnApp = BRApi.Database.CreateApplicationDbConnInfo(si);
        using var dbCommand = dbConnApp.CreateCommand(false);

        var startRowNumber = startRowIndex + 1;
        var endRowNumber = startRowIndex + pageSize;

        dbCommand.CommandText = @$"
			SELECT [AuditId],[SnapshotId],[SnapshotGuid],[Action],[ActionBy],[ActionDateUtc] AS [ActionDate],[Details]
			FROM dbo.{Database.TblAudit} WITH (NOLOCK)
			ORDER BY [ActionDateUtc] DESC
			OFFSET ({startRowNumber} - 1) ROWS
			FETCH NEXT ({endRowNumber} - {startRowNumber} + 1) ROWS ONLY";

        var dt = BRApi.Database.ExecuteSql(dbConnApp, dbCommand.CommandText, true);

        var userTimeZone = TimeZoneService.GetUserTimeZone(si);
        foreach (DataRow row in dt.Rows)
            row["ActionDate"] = TimeZoneService.ToUserLocal((DateTime)row["ActionDate"], userTimeZone);

        const string sqlCount = @$"
			SELECT COUNT(*)
			FROM dbo.{Database.TblAudit} WITH (NOLOCK)";

        var dtCount = BRApi.Database.ExecuteSqlUsingReader(dbConnApp, sqlCount, true);

        var result =
            new XFDataTable(si, dt, null, -1) { TotalNumRowsInOriginalDataTable = Convert.ToInt32(dtCount.Rows[0][0]) };

        List<XFDynamicGridColumnDefinition> columnDefinitions =
        [
            new()
            {
                IsFromTable = TriStateBool.TrueValue, IsVisible = TriStateBool.FalseValue, ColumnName = "SnapshotId"
            },
            new() { IsFromTable = TriStateBool.TrueValue, IsVisible = TriStateBool.TrueValue, ColumnName = "Notes" }
        ];

        return new XFDynamicGridGetDataResult(result, columnDefinitions, null, DataAccessLevel.AllAccess);
    }

    private static XFDynamicGridGetDataResult GetSnapshotsSummary(SessionInfo si, DashboardDynamicGridArgs args)
    {
        var startRowIndex = args.GetDataArgs.StartRowIndex;
        var pageSize = args.GetDataArgs.PageSize;

        using var dbConnApp = BRApi.Database.CreateApplicationDbConnInfo(si);
        using var dbCommand = dbConnApp.CreateCommand(false);

        var startRowNumber = startRowIndex + 1;
        var endRowNumber = startRowIndex + pageSize;

        dbCommand.CommandText = @$"
			SELECT
			   [SnapshotName] as [Name]
			  ,[CreatedBy] as [User]
			  ,[CreatedDateUtc] as [Created]
			  ,[ExtractOptions] as [Options]
			  ,[Notes]
			  ,[Status]
			  ,[RowCount] as [Num Rows]
			  ,[SnapshotId]
			  ,[SnapshotGuid]
			FROM dbo.{Database.TblSnapshotHeader} WITH (NOLOCK)
			ORDER BY [CreatedDateUtc] DESC
			OFFSET ({startRowNumber} - 1) ROWS
			FETCH NEXT ({endRowNumber} - {startRowNumber} + 1) ROWS ONLY";

        var dt = BRApi.Database.ExecuteSql(dbConnApp, dbCommand.CommandText, true);

        var userTimeZone = TimeZoneService.GetUserTimeZone(si);
        foreach (DataRow row in dt.Rows)
            row["Created"] = TimeZoneService.ToUserLocal((DateTime)row["Created"], userTimeZone);

        const string sqlCount = @$"
			SELECT COUNT(*)
			FROM dbo.{Database.TblSnapshotHeader} WITH (NOLOCK)";

        var dtCount = BRApi.Database.ExecuteSqlUsingReader(dbConnApp, sqlCount, true);

        var result =
            new XFDataTable(si, dt, null, -1) { TotalNumRowsInOriginalDataTable = Convert.ToInt32(dtCount.Rows[0][0]) };

        List<XFDynamicGridColumnDefinition> columnDefinitions =
        [
            new()
            {
                ColumnName = "SnapshotId", IsFromTable = TriStateBool.TrueValue, IsVisible = TriStateBool.FalseValue
            },
            new()
            {
                ColumnName = "SnapshotGuid",
                IsFromTable = TriStateBool.TrueValue,
                IsVisible = TriStateBool.FalseValue
            }
        ];

        return new XFDynamicGridGetDataResult(result, columnDefinitions, null, DataAccessLevel.AllAccess);
    }
}