using DTS.Constants;

using OneStream.Shared.Common;
using OneStream.Shared.Wcf;

namespace DTS.Services;

internal static class InstallationService
{
    public static void Install(SessionInfo si)
    {
        var sql = GetSqlInstallTables();
        using var dbConnApp = BRApi.Database.CreateApplicationDbConnInfo(si);
        BRApi.Database.ExecuteActionQuery(dbConnApp, sql, false, false);
        FileService.CreateSolutionFolders(si);
    }

    internal static string GetSqlInstallTables() => @$"
        BEGIN TRY
            DECLARE @LockResult INT;
            EXEC @LockResult = sp_getapplock
                @Resource = 'ZDTS_SchemaInstall',
                @LockMode = 'Exclusive',
                @LockOwner = 'Session',
                @LockTimeout = 30000;  -- 30s;

            IF @LockResult < 0
            BEGIN
                RAISERROR('ZDTS_EnsureSchema: could not acquire schema lock (result %d). Another process may be holding it too long.', 16, 1, @LockResult);
            END

            -- -------------------------------------------------------------
            -- Sequence
            -- -------------------------------------------------------------
            IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = '{Database.SequenceSnapshotId}')
            BEGIN
                CREATE SEQUENCE dbo.{Database.SequenceSnapshotId} AS INT START WITH 1 INCREMENT BY 1;
                PRINT 'Created SEQUENCE {Database.SequenceSnapshotId}';
            END

            -- -------------------------------------------------------------
            -- Partition function / scheme
            -- -------------------------------------------------------------
            IF NOT EXISTS (SELECT 1 FROM sys.partition_functions WHERE name = '{Database.PartFuncSnapshotData}')
            BEGIN
                CREATE PARTITION FUNCTION {Database.PartFuncSnapshotData} (INT) AS RANGE RIGHT FOR VALUES ();
                PRINT 'Created PARTITION FUNCTION {Database.PartFuncSnapshotData}';
            END

            IF NOT EXISTS (SELECT 1 FROM sys.partition_schemes WHERE name = '{Database.PartSchemeSnapshotData}')
            BEGIN
                CREATE PARTITION SCHEME {Database.PartSchemeSnapshotData}
                    AS PARTITION {Database.PartFuncSnapshotData} ALL TO ([PRIMARY]);
                PRINT 'Created PARTITION SCHEME {Database.PartSchemeSnapshotData}';
            END

            -- -------------------------------------------------------------
            -- {Database.TblSnapshotHeader}
            -- -------------------------------------------------------------
            IF OBJECT_ID('dbo.{Database.TblSnapshotHeader}', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.{Database.TblSnapshotHeader} (
                    SnapshotId       INT              NOT NULL CONSTRAINT PK_{Database.TblSnapshotHeader} PRIMARY KEY,
                    SnapshotGuid     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
                    SnapshotName     NVARCHAR(200)    NOT NULL,
                    ExtractOptions   NVARCHAR(MAX)    NULL,
                    CreatedBy        NVARCHAR(100)    NOT NULL,
                    CreatedDateUtc   DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
                    [RowCount]       INT              NULL,
                    [Status]         VARCHAR(20)      NOT NULL DEFAULT 'Running',
                    Notes            NVARCHAR(4000)   NULL
                );
                PRINT 'Created TABLE {Database.TblSnapshotHeader}';
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = 'IX_{Database.TblSnapshotHeader}_Name'
                             AND object_id = OBJECT_ID('dbo.{Database.TblSnapshotHeader}'))
            BEGIN
                CREATE INDEX IX_{Database.TblSnapshotHeader}_Name ON dbo.{Database.TblSnapshotHeader}(SnapshotName);
                PRINT 'Created INDEX IX_{Database.TblSnapshotHeader}_Name';
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = 'IX_{Database.TblSnapshotHeader}_CreatedDateUtc'
                             AND object_id = OBJECT_ID('dbo.{Database.TblSnapshotHeader}'))
            BEGIN
                CREATE INDEX IX_{Database.TblSnapshotHeader}_CreatedDateUtc ON dbo.{Database.TblSnapshotHeader}(CreatedDateUtc DESC);
                PRINT 'Created INDEX IX_{Database.TblSnapshotHeader}_CreatedDateUtc';
            END

            -- -------------------------------------------------------------
            -- {Database.TblMemberDictionary}
            -- -------------------------------------------------------------
            IF OBJECT_ID('dbo.{Database.TblMemberDictionary}', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.{Database.TblMemberDictionary} (
                    MemberId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_{Database.TblMemberDictionary} PRIMARY KEY,
                    MemberName     NVARCHAR(1000) NOT NULL,
                    MemberNameHash AS CONVERT(BINARY(32), HASHBYTES('SHA2_256', MemberName)) PERSISTED
                );
                PRINT 'Created TABLE {Database.TblMemberDictionary}';
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = 'UX_{Database.TblMemberDictionary}_Hash'
                             AND object_id = OBJECT_ID('dbo.{Database.TblMemberDictionary}'))
            BEGIN
                CREATE UNIQUE INDEX UX_{Database.TblMemberDictionary}_Hash ON dbo.{Database.TblMemberDictionary}(MemberNameHash);
                PRINT 'Created INDEX UX_{Database.TblMemberDictionary}_Hash';
            END

            -- -------------------------------------------------------------
            -- {Database.TblSnapshotData} (partitioned)
            --
            -- NOTE: TimePeriod is VARCHAR(7), NOT CHAR(7). CHAR pads
            -- ('2025M1' -> '2025M1 ') and that padding feeds into RowHash
            -- via CONCAT_WS. Changing this type after snapshots exist would
            -- silently break every cross-snapshot comparison, because old
            -- and new hashes would no longer match. Locked in as VARCHAR.
            -- -------------------------------------------------------------
            IF OBJECT_ID('dbo.{Database.TblSnapshotData}', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.{Database.TblSnapshotData} (
                    SnapshotId   INT           NOT NULL,
                    CubeId       INT           NOT NULL,
                    EntityId     INT           NOT NULL,
                    ParentId     INT           NOT NULL,
                    ConsId       INT           NOT NULL,
                    ScenarioId   INT           NOT NULL,
                    AccountId    INT           NOT NULL,
                    FlowId       INT           NOT NULL,
                    OriginId     INT           NOT NULL,
                    ICId         INT           NOT NULL,
                    UD1Id        INT           NOT NULL,
                    UD2Id        INT           NOT NULL,
                    UD3Id        INT           NOT NULL,
                    UD4Id        INT           NOT NULL,
                    UD5Id        INT           NOT NULL,
                    UD6Id        INT           NOT NULL,
                    UD7Id        INT           NOT NULL,
                    UD8Id        INT           NOT NULL,
                    TimePeriod   VARCHAR(7)    NOT NULL,
                    ViewMember   VARCHAR(50)   NOT NULL,
                    Amount       DECIMAL(28,9) NOT NULL,
                    RowHash AS ISNULL(CONVERT(BINARY(32), HASHBYTES('SHA2_256',
                        CONCAT_WS('|', CubeId, EntityId, ParentId, ConsId, ScenarioId, AccountId,
                                       FlowId, OriginId, ICId, UD1Id, UD2Id, UD3Id, UD4Id, UD5Id,
                                       UD6Id, UD7Id, UD8Id, TimePeriod, ViewMember)
                    )), 0x00) PERSISTED
                ) ON {Database.PartSchemeSnapshotData}(SnapshotId);
                PRINT 'Created TABLE {Database.TblSnapshotData}';
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = 'CIX_{Database.TblSnapshotData}'
                             AND object_id = OBJECT_ID('dbo.{Database.TblSnapshotData}'))
            BEGIN
                CREATE CLUSTERED INDEX CIX_{Database.TblSnapshotData} ON dbo.{Database.TblSnapshotData} (SnapshotId, RowHash)
                    ON {Database.PartSchemeSnapshotData}(SnapshotId);
                PRINT 'Created INDEX CIX_{Database.TblSnapshotData}';
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = 'IX_{Database.TblSnapshotData}_Entity'
                             AND object_id = OBJECT_ID('dbo.{Database.TblSnapshotData}'))
            BEGIN
                CREATE INDEX IX_{Database.TblSnapshotData}_Entity ON dbo.{Database.TblSnapshotData} (SnapshotId, EntityId)
                    INCLUDE (Amount) ON {Database.PartSchemeSnapshotData}(SnapshotId);
                PRINT 'Created INDEX IX_{Database.TblSnapshotData}_Entity';
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = 'IX_{Database.TblSnapshotData}_Account'
                             AND object_id = OBJECT_ID('dbo.{Database.TblSnapshotData}'))
            BEGIN
                CREATE INDEX IX_{Database.TblSnapshotData}_Account ON dbo.{Database.TblSnapshotData} (SnapshotId, AccountId)
                    INCLUDE (Amount) ON {Database.PartSchemeSnapshotData}(SnapshotId);
                PRINT 'Created INDEX IX_{Database.TblSnapshotData}_Account';
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = 'IX_{Database.TblSnapshotData}_Time'
                             AND object_id = OBJECT_ID('dbo.{Database.TblSnapshotData}'))
            BEGIN
                CREATE INDEX IX_{Database.TblSnapshotData}_Time ON dbo.{Database.TblSnapshotData} (SnapshotId, TimePeriod)
                    INCLUDE (Amount) ON {Database.PartSchemeSnapshotData}(SnapshotId);
                PRINT 'Created INDEX IX_{Database.TblSnapshotData}_Time';
            END

            -- -------------------------------------------------------------
            -- {Database.TblStaging} (partition-switch target for deletes)
            --
            -- MUST be structurally identical to {Database.TblSnapshotData},
            -- because ALTER TABLE ... SWITCH is metadata-only and SQL Server
            -- verifies every column matches on type, nullability, and
            -- computed-ness before allowing the page reassignment.
            --
            -- RowHash therefore has to be the SAME computed expression, not
            -- a plain BINARY(32). A computed column's nullability is inferred
            -- from its expression, and since HASHBYTES can return NULL the
            -- column is nullable — a plain 'BINARY(32) NOT NULL' here fails
            -- with error 4985 (nullability mismatch), and 'BINARY(32) NULL'
            -- fails the computed-vs-plain check instead.
            -- -------------------------------------------------------------
            IF OBJECT_ID('dbo.{Database.TblStaging}', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.{Database.TblStaging} (
                    SnapshotId   INT           NOT NULL,
                    CubeId       INT           NOT NULL,
                    EntityId     INT           NOT NULL,
                    ParentId     INT           NOT NULL,
                    ConsId       INT           NOT NULL,
                    ScenarioId   INT           NOT NULL,
                    AccountId    INT           NOT NULL,
                    FlowId       INT           NOT NULL,
                    OriginId     INT           NOT NULL,
                    ICId         INT           NOT NULL,
                    UD1Id        INT           NOT NULL,
                    UD2Id        INT           NOT NULL,
                    UD3Id        INT           NOT NULL,
                    UD4Id        INT           NOT NULL,
                    UD5Id        INT           NOT NULL,
                    UD6Id        INT           NOT NULL,
                    UD7Id        INT           NOT NULL,
                    UD8Id        INT           NOT NULL,
                    TimePeriod   VARCHAR(7)    NOT NULL,
                    ViewMember   VARCHAR(50)   NOT NULL,
                    Amount       DECIMAL(28,9) NOT NULL,
                    RowHash AS ISNULL(CONVERT(BINARY(32), HASHBYTES('SHA2_256',
                        CONCAT_WS('|', CubeId, EntityId, ParentId, ConsId, ScenarioId, AccountId,
                                       FlowId, OriginId, ICId, UD1Id, UD2Id, UD3Id, UD4Id, UD5Id,
                                       UD6Id, UD7Id, UD8Id, TimePeriod, ViewMember)
                    )), 0x00) PERSISTED
                );
                PRINT 'Created TABLE {Database.TblStaging}';
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = 'CIX_{Database.TblStaging}'
                             AND object_id = OBJECT_ID('dbo.{Database.TblStaging}'))
            BEGIN
                CREATE CLUSTERED INDEX CIX_{Database.TblStaging} ON dbo.{Database.TblStaging} (SnapshotId, RowHash);
                PRINT 'Created INDEX CIX_{Database.TblStaging}';
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = 'IX_{Database.TblStaging}_Entity'
                             AND object_id = OBJECT_ID('dbo.{Database.TblStaging}'))
            BEGIN
                CREATE INDEX IX_{Database.TblStaging}_Entity ON dbo.{Database.TblStaging} (SnapshotId, EntityId) INCLUDE (Amount);
                PRINT 'Created INDEX IX_{Database.TblStaging}_Entity';
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = 'IX_{Database.TblStaging}_Account'
                             AND object_id = OBJECT_ID('dbo.{Database.TblStaging}'))
            BEGIN
                CREATE INDEX IX_{Database.TblStaging}_Account ON dbo.{Database.TblStaging} (SnapshotId, AccountId) INCLUDE (Amount);
                PRINT 'Created INDEX IX_{Database.TblStaging}_Account';
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = 'IX_{Database.TblStaging}_Time'
                             AND object_id = OBJECT_ID('dbo.{Database.TblStaging}'))
            BEGIN
                CREATE INDEX IX_{Database.TblStaging}_Time ON dbo.{Database.TblStaging} (SnapshotId, TimePeriod) INCLUDE (Amount);
                PRINT 'Created INDEX IX_{Database.TblStaging}_Time';
            END

            -- -------------------------------------------------------------
            -- {Database.TypeMemberNameList} (table type)
            --
            -- Replaces the old bulk-load landing table. Only the DISTINCT
            -- member NAMES now go to SQL Server (thousands of rows), not the
            -- full extract (millions). Fact rows go straight into
            -- {Database.TblSnapshotData} via SqlBulkCopy with the MemberIds
            -- already resolved in C#.
            -- -------------------------------------------------------------
            IF NOT EXISTS (SELECT 1 FROM sys.table_types WHERE name = '{Database.TypeMemberNameList}')
            BEGIN
                CREATE TYPE dbo.{Database.TypeMemberNameList} AS TABLE (
                    MemberName NVARCHAR(1000) NOT NULL
                );
                PRINT 'Created TYPE {Database.TypeMemberNameList}';
            END

            -- -------------------------------------------------------------
            -- {Database.TblAudit}
            -- -------------------------------------------------------------
            IF OBJECT_ID('dbo.{Database.TblAudit}', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.{Database.TblAudit} (
                    AuditId        BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_{Database.TblAudit} PRIMARY KEY,
                    SnapshotId     INT              NULL,
                    SnapshotGuid   UNIQUEIDENTIFIER NULL,
                    [Action]       VARCHAR(20)      NOT NULL,
                    ActionBy       NVARCHAR(100)    NOT NULL,
                    ActionDateUtc  DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
                    Details        NVARCHAR(4000)   NULL
                );
                PRINT 'Created TABLE {Database.TblAudit}';
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = 'IX_{Database.TblAudit}_ActionDateUtc'
                             AND object_id = OBJECT_ID('dbo.{Database.TblAudit}'))
            BEGIN
                CREATE INDEX IX_{Database.TblAudit}_ActionDateUtc ON dbo.{Database.TblAudit}(ActionDateUtc DESC);
                PRINT 'Created INDEX IX_{Database.TblAudit}_ActionDateUtc';
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

            RAISERROR('ZDTS_EnsureSchema failed: %s', @ErrSeverity, @ErrState, @ErrMsg);
        END CATCH";
}