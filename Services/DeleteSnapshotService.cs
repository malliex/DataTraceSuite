using System;
using System.Data;

using DTS.Common;
using DTS.Constants;

using Microsoft.Data.SqlClient;

using OneStream.Shared.Common;
using OneStream.Shared.Database;
using OneStream.Shared.Wcf;

namespace DTS.Services;

internal static class DeleteSnapshotService
{
    public static OperationResult DeleteSnapshot(SessionInfo si, int snapshotId)
    {
        var result = new OperationResult();
        var actionBy = si.UserName;

        using DbConnInfo dbConnInfo = BRApi.Database.CreateApplicationDbConnInfo(si);

        var conn = dbConnInfo.GetSqlServerSpecificConnection();
        var tx = dbConnInfo.GetSqlServerSpecificTransaction();

        try
        {
            Guid snapshotGuid;
            string snapshotName;
            string status;

            using (var cmd = new SqlCommand(SqlFetchHeader, conn, tx))
            {
                cmd.CommandTimeout = 30;
                cmd.Parameters.Add("@SnapshotId", SqlDbType.Int).Value = snapshotId;

                using var reader = cmd.ExecuteReader();

                if (!reader.Read())
                {
                    result.AddMessage($"Snapshot {snapshotId} does not exist.");
                    return result;
                }

                snapshotGuid = reader.GetGuid(0);
                snapshotName = reader.GetString(1);
                status = reader.GetString(2);
            }

            // A snapshot mid-load is still being bulk-copied into on another
            // connection/transaction. SWITCH-ing its partition out from under
            // that load would corrupt or fail the in-progress insert.
            if (status == "Running")
            {
                result.AddMessage(
                    $"Snapshot {snapshotId} ('{snapshotName}') is still being created and cannot be deleted yet.");
                return result;
            }

            using (var cmd = new SqlCommand(SqlDeleteSnapshot, conn, tx))
            {
                cmd.CommandTimeout = 120;
                cmd.Parameters.Add("@SnapshotId", SqlDbType.Int).Value = snapshotId;
                cmd.Parameters.Add("@SnapshotGuid", SqlDbType.UniqueIdentifier).Value = snapshotGuid;
                cmd.Parameters.Add("@SnapshotName", SqlDbType.NVarChar, 200).Value = snapshotName;
                cmd.Parameters.Add("@ActionBy", SqlDbType.NVarChar, 100).Value = actionBy;

                cmd.ExecuteNonQuery();
            }

            return result;
        }
        catch (Exception ex)
        {
            BRApi.ErrorLog.LogMessage(si, $"ZDTS: snapshot deletion failed (SnapshotId {snapshotId}): {ex}");

            result.AddMessage($"Snapshot deletion failed: {ex.Message}");
            return result;
        }
    }

    // NOLOCK is fine here: we only need the header row to still exist and to
    // read its current Status/Guid/Name, and the caller-provided SnapshotId
    // is trusted input, not a range scan of live data.
    internal const string SqlFetchHeader = @$"
        SELECT SnapshotGuid, SnapshotName, [Status]
        FROM dbo.{Database.TblSnapshotHeader} WITH (NOLOCK)
        WHERE SnapshotId = @SnapshotId;";

    // SWITCH is metadata-only (see ZDTS_Architecture.md §3.3) so this deletes
    // millions of rows at the same cost as a handful. SnapshotGuid/Name are
    // passed in as parameters rather than re-selected from the header, since
    // the header row is gone by the time the audit INSERT runs (see the
    // known SnapshotGuid-NULL-on-audit-row bug in ZDTS_Architecture.md §8).
    internal const string SqlDeleteSnapshot = @$"
        ALTER TABLE dbo.{Database.TblSnapshotData}
            SWITCH PARTITION $PARTITION.{Database.PartFuncSnapshotData}(@SnapshotId)
            TO dbo.{Database.TblStaging};

        TRUNCATE TABLE dbo.{Database.TblStaging};

        DELETE FROM dbo.{Database.TblSnapshotHeader} WHERE SnapshotId = @SnapshotId;

        INSERT INTO dbo.{Database.TblAudit} (SnapshotId, SnapshotGuid, [Action], ActionBy, Details)
        VALUES (@SnapshotId, @SnapshotGuid, 'Deleted', @ActionBy, CONCAT('Snapshot deleted: ', @SnapshotName));";
}