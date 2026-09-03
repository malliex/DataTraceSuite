using System;

using DTS.Common;
using DTS.Constants;

using OneStream.Shared.Common;
using OneStream.Shared.Wcf;

namespace DTS.Services;

internal static class UninstallService
{
    public static OperationResult Uninstall(SessionInfo si)
    {
        var result = new OperationResult();

        try
        {
            var sql = GetSqlUninstallTables();
            using var dbConnApp = BRApi.Database.CreateApplicationDbConnInfo(si);
            BRApi.Database.ExecuteActionQuery(dbConnApp, sql, false, false);
            FileService.DeleteSolutionFolders(si);
        }
        catch (Exception ex)
        {
            BRApi.ErrorLog.LogMessage(si, $"ZDTS: uninstall failed: {ex}");
            result.AddMessage($"Uninstall failed: {ex.Message}");
        }

        return result;
    }

    internal static string GetSqlUninstallTables() => @$"
        BEGIN TRY
            DECLARE @LockResult INT;
            EXEC @LockResult = sp_getapplock
                @Resource = 'ZDTS_SchemaInstall',
                @LockMode = 'Exclusive',
                @LockOwner = 'Session',
                @LockTimeout = 30000;  -- 30s;

            IF @LockResult < 0
            BEGIN
                RAISERROR('ZDTS_Uninstall: could not acquire schema lock (result %d). Another process may be holding it too long.', 16, 1, @LockResult);
            END

            -- Refuse to drop out from under a snapshot that is mid-bulk-copy.
            -- This only closes the window against another Install/Uninstall,
            -- since CreateSnapshotService/DeleteSnapshotService don't take
            -- this same applock -- a create can still start immediately after
            -- this check passes and race the drops below.
            IF OBJECT_ID('dbo.{Database.TblSnapshotHeader}', 'U') IS NOT NULL
               AND EXISTS (SELECT 1 FROM dbo.{Database.TblSnapshotHeader} WITH (NOLOCK) WHERE [Status] = 'Running')
            BEGIN
                RAISERROR('ZDTS_Uninstall: cannot uninstall while a snapshot is still being created (Status = ''Running''). Wait for it to finish or fail, then retry.', 16, 1);
            END

            -- -------------------------------------------------------------
            -- Tables. {Database.TblSnapshotData} must be dropped before the
            -- partition scheme it's ON, since a scheme can't be dropped while
            -- any object still uses it.
            -- -------------------------------------------------------------
            IF OBJECT_ID('dbo.{Database.TblSnapshotData}', 'U') IS NOT NULL
            BEGIN
                DROP TABLE dbo.{Database.TblSnapshotData};
                PRINT 'Dropped TABLE {Database.TblSnapshotData}';
            END

            IF OBJECT_ID('dbo.{Database.TblStaging}', 'U') IS NOT NULL
            BEGIN
                DROP TABLE dbo.{Database.TblStaging};
                PRINT 'Dropped TABLE {Database.TblStaging}';
            END

            IF OBJECT_ID('dbo.{Database.TblSnapshotHeader}', 'U') IS NOT NULL
            BEGIN
                DROP TABLE dbo.{Database.TblSnapshotHeader};
                PRINT 'Dropped TABLE {Database.TblSnapshotHeader}';
            END

            IF OBJECT_ID('dbo.{Database.TblMemberDictionary}', 'U') IS NOT NULL
            BEGIN
                DROP TABLE dbo.{Database.TblMemberDictionary};
                PRINT 'Dropped TABLE {Database.TblMemberDictionary}';
            END

            IF OBJECT_ID('dbo.{Database.TblAudit}', 'U') IS NOT NULL
            BEGIN
                DROP TABLE dbo.{Database.TblAudit};
                PRINT 'Dropped TABLE {Database.TblAudit}';
            END

            -- -------------------------------------------------------------
            -- Table type
            -- -------------------------------------------------------------
            IF EXISTS (SELECT 1 FROM sys.table_types WHERE name = '{Database.TypeMemberNameList}')
            BEGIN
                DROP TYPE dbo.{Database.TypeMemberNameList};
                PRINT 'Dropped TYPE {Database.TypeMemberNameList}';
            END

            -- -------------------------------------------------------------
            -- Partition scheme / function. Scheme depends on the function,
            -- so it must go first. Sequence has no dependency on either.
            -- -------------------------------------------------------------
            IF EXISTS (SELECT 1 FROM sys.partition_schemes WHERE name = '{Database.PartSchemeSnapshotData}')
            BEGIN
                DROP PARTITION SCHEME {Database.PartSchemeSnapshotData};
                PRINT 'Dropped PARTITION SCHEME {Database.PartSchemeSnapshotData}';
            END

            IF EXISTS (SELECT 1 FROM sys.partition_functions WHERE name = '{Database.PartFuncSnapshotData}')
            BEGIN
                DROP PARTITION FUNCTION {Database.PartFuncSnapshotData};
                PRINT 'Dropped PARTITION FUNCTION {Database.PartFuncSnapshotData}';
            END

            IF EXISTS (SELECT 1 FROM sys.sequences WHERE name = '{Database.SequenceSnapshotId}')
            BEGIN
                DROP SEQUENCE dbo.{Database.SequenceSnapshotId};
                PRINT 'Dropped SEQUENCE {Database.SequenceSnapshotId}';
            END

            EXEC sp_releaseapplock @Resource = 'ZDTS_SchemaInstall', @LockOwner = 'Session';

        END TRY
        BEGIN CATCH
            -- Always release the lock, even on failure, then surface the real error.
            IF APPLOCK_MODE('public', 'ZDTS_SchemaInstall', 'Session') <> 'NoLock'
                EXEC sp_releaseapplock @Resource = 'ZDTS_SchemaInstall', @LockOwner = 'Session';

            DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
            DECLARE @ErrState INT = ERROR_STATE();

            -- Clamp severity: re-raising 20+ requires sysadmin and WITH LOG,
            -- which would throw a different error and lose the real message.
            DECLARE @ErrSeverity INT = CASE WHEN ERROR_SEVERITY() > 18 THEN 16 ELSE ERROR_SEVERITY() END;

            RAISERROR('ZDTS_Uninstall failed: %s', @ErrSeverity, @ErrState, @ErrMsg);
        END CATCH";
}