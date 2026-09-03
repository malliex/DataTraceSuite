using System;
using System.Data;

using DTS.Common;
using DTS.Constants;

using Microsoft.Data.SqlClient;

using OneStream.Shared.Common;
using OneStream.Shared.Database;
using OneStream.Shared.Wcf;

namespace DTS.Services;

internal static class DatabaseService
{
    public static bool SolutionTableExist(SessionInfo si)
    {
        var sql = @$"
            SELECT 'SEQUENCE: {Database.SequenceSnapshotId}' AS MissingObject
            WHERE NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = '{Database.SequenceSnapshotId}')

            UNION ALL
            SELECT 'PARTITION FUNCTION: {Database.PartFuncSnapshotData}'
            WHERE NOT EXISTS (SELECT 1 FROM sys.partition_functions WHERE name = '{Database.PartFuncSnapshotData}')

            UNION ALL
            SELECT 'PARTITION SCHEME: {Database.PartSchemeSnapshotData}'
            WHERE NOT EXISTS (SELECT 1 FROM sys.partition_schemes WHERE name = '{Database.PartSchemeSnapshotData}')

            UNION ALL
            SELECT 'TABLE: ' + t.name
            FROM (VALUES
                ('{Database.TblSnapshotHeader}'),
                ('{Database.TblMemberDictionary}'),
                ('{Database.TblSnapshotData}'),
                ('{Database.TblStaging}'),
                ('{Database.TblAudit}')
            ) AS t(name)
            WHERE OBJECT_ID('dbo.' + t.name, 'U') IS NULL;";

        using var dbConnApp = BRApi.Database.CreateApplicationDbConnInfo(si);
        return BRApi.Database.ExecuteSqlUsingReader(dbConnApp, sql, false).Rows.Count == 0;
    }

    public static SnapshotHeader GetSnapshotHeader(SessionInfo si, int snapshotId)
    {
        using DbConnInfo dbConnInfo = BRApi.Database.CreateApplicationDbConnInfo(si);

        var conn = dbConnInfo.GetSqlServerSpecificConnection();
        var tx = dbConnInfo.GetSqlServerSpecificTransaction();

        using var cmd = new SqlCommand(SqlGetSnapshotHeader, conn, tx);
        cmd.CommandTimeout = 30;
        cmd.Parameters.Add("@SnapshotId", SqlDbType.Int).Value = snapshotId;

        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
            return null;

        return new SnapshotHeader(
            Convert.ToInt32(reader["SnapshotId"]),
            (Guid)reader["SnapshotGuid"],
            Convert.ToString(reader["SnapshotName"]),
            Convert.ToString(reader["ExtractOptions"]),
            Convert.ToString(reader["CreatedBy"]),
            TimeZoneService.ToUserLocal(si, Convert.ToDateTime(reader["CreatedDateUtc"])),
            Convert.ToString(reader["Status"]),
            reader["RowCount"] == DBNull.Value ? null : Convert.ToInt32(reader["RowCount"]),
            reader["Notes"] == DBNull.Value ? null : Convert.ToString(reader["Notes"]));
    }

    // Shared by every service that reads snapshot data (compare, extract, ...)
    // so a snapshot mid-load or already deleted can never be read.
    public static void ValidateSnapshotIsCompleted(SessionInfo si, int snapshotId, string actionVerb)
    {
        var header = GetSnapshotHeader(si, snapshotId);

        if (header is null)
            throw new ArgumentException($"Snapshot {snapshotId} does not exist.");

        if (header.Status != "Completed")
        {
            throw new ArgumentException(
                $"Snapshot {snapshotId} ('{header.SnapshotName}') is not Completed " +
                $"(Status = '{header.Status}') and cannot be {actionVerb}.");
        }
    }

    internal const string SqlGetSnapshotHeader = @$"
        SELECT [SnapshotId], [SnapshotGuid], [SnapshotName], [ExtractOptions],
               [CreatedBy], [CreatedDateUtc], [Status], [RowCount], [Notes]
        FROM dbo.{Database.TblSnapshotHeader} WITH (NOLOCK)
        WHERE [SnapshotId] = @SnapshotId;";
}