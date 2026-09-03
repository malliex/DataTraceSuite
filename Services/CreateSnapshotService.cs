using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;

using DTS.Common;
using DTS.Constants;
using DTS.Enums;

using Microsoft.Data.SqlClient;

using OneStream.Shared.Common;
using OneStream.Shared.Database;
using OneStream.Shared.Wcf;

namespace DTS.Services;

internal static class CreateSnapshotService
{
    // Source columns holding member names, in the order the reader emits them.
    private readonly static string[] MemberColumns =
    [
        "Cube", "Entity", "Parent", "Cons", "Scenario", "Account", "Flow", "Origin", "IC",
        "UD1", "UD2", "UD3", "UD4", "UD5", "UD6", "UD7", "UD8"
    ];

    // Everything the extract must contain for the load to work.
    private readonly static string[] SourceColumns =
    [
        "Cube", "Entity", "Parent", "Cons", "Scenario", "Time", "View", "Account", "Flow", "Origin", "IC",
        "UD1", "UD2", "UD3", "UD4", "UD5", "UD6", "UD7", "UD8", "Amount"
    ];

    public static OperationResult CreateSnapshot(
        SessionInfo si,
        SnapshotCreateOptions options,
        string notes = null,
        ExtenderArgs args = null)
    {
        var result = new OperationResult();

        Func<SessionInfo, ExtenderArgs, string, decimal, bool> taUpdater = args is not null
            ? BRApi.TaskActivity.UpdateRunningTaskActivityAndCheckIfCanceled
            : null;

        const decimal pctToAdd = 99.99m / 7m;

        taUpdater?.Invoke(si, args, "Creating snapshot", pctToAdd);

        const string cnName = "Local";
        const string vwName = "YTD";
        const bool suppressNoData = true;
        const string auxFilter = null;
        const int parallelQueryCount = 8;
        const bool logStats = false;

        var etFilter = $"E#{options.Entity}";
        var snFilter = $"S#{options.Scenario}";
        var snId = BRApi.Finance.Members.GetMember(si, DimType.Scenario.Id, options.Scenario).MemberId;
        var snTypeId = BRApi.Finance.Scenario.GetScenarioType(si, snId).Id;

        taUpdater?.Invoke(si, args, "Retrieving source data", pctToAdd);

        var dt = BRApi.Import.Data.FdxExecuteDataUnit(
            si,
            options.Cube,
            etFilter,
            cnName,
            snTypeId,
            snFilter,
            options.TimeFilter,
            vwName,
            suppressNoData,
            auxFilter,
            parallelQueryCount,
            logStats);

        if (dt == null || dt.Rows.Count == 0)
        {
            result.AddMessage("Extract returned no rows - nothing to snapshot.");
            return result;
        }

        foreach (var requiredCol in SourceColumns)
            if (!dt.Columns.Contains(requiredCol))
            {
                result.AddMessage($"Extract is missing expected column '{requiredCol}'.");
                return result;
            }

        return LoadSnapshot(si, dt, options, notes, args, taUpdater, pctToAdd);
    }

    /// <summary>
    ///     Validates that a file selected for import is a .csv whose header
    ///     matches the expected snapshot column structure. Cheap - reads the
    ///     file once and inspects only the header row - so it is safe to run
    ///     synchronously from the dashboard, before the actual parse+load is
    ///     queued as a background Data Mgmt sequence (CreateSnapshotFromCsv).
    /// </summary>
    public static bool TryValidateCsvFile(SessionInfo si, string fileFullName, out string error)
    {
        error = null;

        if (!Path.GetExtension(fileFullName).XFEqualsIgnoreCase(".csv"))
        {
            error = $"'{Path.GetFileName(fileFullName)}' is not a .csv file.";
            return false;
        }

        if (!FileService.TryGetFileBytes(si, FileType.Import, fileFullName, out var fileBytes))
        {
            error = $"Import file not found: '{Path.GetFileName(fileFullName)}'.";
            return false;
        }

        var header = CsvService.ReadCsvHeader(fileBytes);

        if (header.Length != 0)
            return TryValidateCsvHeader(header, out error);

        error = "CSV file is empty.";
        return false;
    }

    /// <summary>
    ///     Same load path as <see cref="CreateSnapshot" />, sourced from a CSV
    ///     file instead of an FDX extract. AppName is fixed to "External" - a
    ///     CSV has no single Cube/Entity/Scenario/TimeFilter the way an FDX
    ///     extract does, so those fields are left blank and the source file
    ///     name is recorded in Notes instead.
    /// </summary>
    public static OperationResult CreateSnapshotFromCsv(SessionInfo si, string fileFullName, ExtenderArgs args = null)
    {
        var result = new OperationResult();

        Func<SessionInfo, ExtenderArgs, string, decimal, bool> taUpdater = args is not null
            ? BRApi.TaskActivity.UpdateRunningTaskActivityAndCheckIfCanceled
            : null;

        const decimal pctToAdd = 99.99m / 8m;

        taUpdater?.Invoke(si, args, "Creating snapshot", pctToAdd);
        taUpdater?.Invoke(si, args, "Reading import file", pctToAdd);

        if (!FileService.TryGetFileBytes(si, FileType.Import, fileFullName, out var fileBytes))
        {
            result.AddMessage($"Import file not found: '{Path.GetFileName(fileFullName)}'.");
            return result;
        }

        taUpdater?.Invoke(si, args, "Parsing CSV rows", pctToAdd);

        DataTable dt;

        try
        {
            dt = BuildDataTableFromCsv(fileBytes);
        }
        catch (Exception ex)
        {
            result.AddMessage($"CSV file is invalid: {ex.Message}");
            return result;
        }

        if (dt.Rows.Count == 0)
        {
            result.AddMessage("CSV file has no data rows - nothing to snapshot.");
            return result;
        }

        var fileName = Path.GetFileName(fileFullName);
        var options = new SnapshotCreateOptions("External", "", "", "", "");
        var notes = $"Imported from CSV file: {fileName}";

        return LoadSnapshot(si, dt, options, notes, args, taUpdater, pctToAdd);
    }

    internal const string SqlReserveSnapshot = @$"
        DECLARE @SnapshotId INT;
        SET @SnapshotId = NEXT VALUE FOR dbo.{Database.SequenceSnapshotId};

        ALTER PARTITION SCHEME {Database.PartSchemeSnapshotData} NEXT USED [PRIMARY];
        ALTER PARTITION FUNCTION {Database.PartFuncSnapshotData}() SPLIT RANGE (@SnapshotId);

        INSERT INTO dbo.{Database.TblSnapshotHeader} (SnapshotId, SnapshotGuid, SnapshotName, ExtractOptions, CreatedBy, [Status], Notes)
        VALUES (@SnapshotId, @SnapshotGuid, @SnapshotName, @ExtractOptions, @CreatedBy, 'Running', @Notes);

        INSERT INTO dbo.{Database.TblAudit} (SnapshotId, SnapshotGuid, [Action], ActionBy, Details)
        VALUES (@SnapshotId, @SnapshotGuid, 'Created', @CreatedBy, 'Snapshot initialized, partition opened');

        SELECT @SnapshotId AS NewSnapshotId;";

    // Upsert any new member names, then return the MemberId for every name in
    // this extract. Matching is by hash, so it stays byte-exact and agrees
    // with StringComparer.Ordinal on the C# side.
    internal const string SqlUpsertAndFetchDictionary = @$"
        INSERT INTO dbo.{Database.TblMemberDictionary} WITH (HOLDLOCK) (MemberName)
        SELECT m.MemberName
        FROM @Members m
        WHERE NOT EXISTS (
            SELECT 1
            FROM dbo.{Database.TblMemberDictionary} md WITH (HOLDLOCK)
            WHERE md.MemberNameHash = CONVERT(BINARY(32), HASHBYTES('SHA2_256', m.MemberName))
        );

        SELECT md.MemberId, md.MemberName
        FROM dbo.{Database.TblMemberDictionary} md
        JOIN @Members m
          ON md.MemberNameHash = CONVERT(BINARY(32), HASHBYTES('SHA2_256', m.MemberName));";

    internal const string SqlCompleteSnapshot = @$"
        UPDATE dbo.{Database.TblSnapshotHeader}
            SET [Status] = 'Completed', [RowCount] = @RowsLoaded
            WHERE SnapshotId = @SnapshotId;

        INSERT INTO dbo.{Database.TblAudit} (SnapshotId, SnapshotGuid, [Action], ActionBy, Details)
        VALUES (@SnapshotId, @SnapshotGuid, 'Completed', @CreatedBy, CONCAT('Row count: ', @RowsLoaded));";

    internal const string SqlCleanupFailed = @$"
        ALTER TABLE dbo.{Database.TblSnapshotData}
            SWITCH PARTITION $PARTITION.{Database.PartFuncSnapshotData}(@SnapshotId)
            TO dbo.{Database.TblStaging};

        TRUNCATE TABLE dbo.{Database.TblStaging};

        INSERT INTO dbo.{Database.TblAudit} (SnapshotId, SnapshotGuid, [Action], ActionBy, Details)
        SELECT SnapshotId, SnapshotGuid, 'Failed', @ActionBy, CONCAT('Rolled back: ', SnapshotName)
        FROM dbo.{Database.TblSnapshotHeader}
        WHERE SnapshotId = @SnapshotId;

        DELETE FROM dbo.{Database.TblSnapshotHeader} WHERE SnapshotId = @SnapshotId;";

    internal static bool TryValidateCsvHeader(string[] header, out string error)
    {
        error = null;

        var headerSet = new HashSet<string>(header, StringComparer.Ordinal);

        if (headerSet.Count != header.Length)
        {
            error = "CSV header contains duplicate column names.";
            return false;
        }

        var missing = SourceColumns.Where(c => !headerSet.Contains(c)).ToArray();

        if (missing.Length > 0)
        {
            error = $"CSV is missing required column(s): {string.Join(", ", missing)}.";
            return false;
        }

        var extra = header.Where(c => Array.IndexOf(SourceColumns, c) < 0).ToArray();

        if (extra.Length > 0)
        {
            error = $"CSV has unexpected column(s): {string.Join(", ", extra)}.";
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Parses the full CSV into the same shape FdxExecuteDataUnit
    ///     returns (SourceColumns, member columns as strings, Amount as
    ///     decimal), so it can be handed to the same downstream pipeline -
    ///     CollectDistinctMemberNames, UpsertAndFetchDictionary,
    ///     ResolvedSnapshotRowReader - completely unmodified.
    /// </summary>
    internal static DataTable BuildDataTableFromCsv(byte[] fileBytes)
    {
        using var records = CsvService.ParseCsv(fileBytes).GetEnumerator();

        if (!records.MoveNext())
            throw new InvalidOperationException("file is empty.");

        var header = records.Current;

        if (!TryValidateCsvHeader(header, out var headerError))
            throw new InvalidOperationException(headerError);

        var colIndex = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < header.Length; i++)
            colIndex[header[i]] = i;

        var dt = new DataTable();

        foreach (var col in SourceColumns)
            dt.Columns.Add(col, col == "Amount" ? typeof(decimal) : typeof(string));

        var lineNum = 1;

        while (records.MoveNext())
        {
            lineNum++;
            var record = records.Current;

            if (record.Length != header.Length)
            {
                throw new InvalidOperationException(
                    $"row {lineNum} has {record.Length} field(s), expected {header.Length}.");
            }

            var dr = dt.NewRow();

            foreach (var col in SourceColumns)
            {
                var raw = record[colIndex[col]];

                if (col == "Amount")
                {
                    if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                        throw new InvalidOperationException($"row {lineNum}: '{raw}' is not a valid Amount.");

                    dr[col] = amount;
                }
                else
                    dr[col] = raw ?? "";
            }

            dt.Rows.Add(dr);
        }

        return dt;
    }

    /// <summary>
    ///     One pass over the extract, gathering every distinct member name
    ///     across all 17 dimension columns. Ordinal comparison to stay in step
    ///     with the hash-based uniqueness in the dictionary table.
    /// </summary>
    internal static HashSet<string> CollectDistinctMemberNames(DataTable dt)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        var ordinals = new int[MemberColumns.Length];

        for (var i = 0; i < MemberColumns.Length; i++)
            ordinals[i] = dt.Columns.IndexOf(MemberColumns[i]);

        foreach (DataRow row in dt.Rows)
        foreach (var ord in ordinals)
        {
            var value = row[ord];

            // FDX can emit blanks (e.g. Parent on a top-level entity).
            // Store them as empty strings rather than NULL so the column
            // stays NOT NULL and the RowHash stays stable.
            names.Add(value == DBNull.Value ? string.Empty : Convert.ToString(value));
        }

        return names;
    }

    /// <summary>
    ///     Sends the distinct names as a table-valued parameter, inserts any
    ///     that are new, and returns name -> MemberId for all of them.
    /// </summary>
    internal static Dictionary<string, int> UpsertAndFetchDictionary(
        SqlConnection conn,
        SqlTransaction tx,
        HashSet<string> memberNames)
    {
        using var tvp = new DataTable();
        tvp.Columns.Add("MemberName", typeof(string));

        foreach (var name in memberNames)
            tvp.Rows.Add(name);

        var map = new Dictionary<string, int>(memberNames.Count, StringComparer.Ordinal);

        using (var cmd = new SqlCommand(SqlUpsertAndFetchDictionary, conn, tx))
        {
            cmd.CommandTimeout = 600;

            var p = cmd.Parameters.Add("@Members", SqlDbType.Structured);
            p.TypeName = $"dbo.{Database.TypeMemberNameList}";
            p.Value = tvp;

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
                map[reader.GetString(1)] = reader.GetInt32(0);
        }

        // Every name we sent must come back. A miss means the hash match and
        // the C# comparer disagree, which would silently corrupt the snapshot.
        if (map.Count != memberNames.Count)
        {
            throw new InvalidOperationException(
                $"ZDTS: dictionary resolved {map.Count} of {memberNames.Count} member names. " +
                "This usually indicates a collation or comparer mismatch between C# and SQL Server.");
        }

        return map;
    }

    /// <summary>
    ///     Streams the FDX extract to SqlBulkCopy, swapping member names for
    ///     MemberIds on the fly. Implemented as an IDataReader so no second
    ///     copy of a multi-million-row DataTable is ever materialized.
    ///     Emits exactly the columns of ZDTS_SnapshotData except RowHash,
    ///     which SQL Server computes itself.
    /// </summary>
    internal sealed class ResolvedSnapshotRowReader : IDataReader
    {
        private readonly static string[] OutputColumns =
        [
            "SnapshotId", "CubeId", "EntityId", "ParentId", "ConsId", "ScenarioId", "AccountId", "FlowId",
            "OriginId", "ICId", "UD1Id", "UD2Id", "UD3Id", "UD4Id", "UD5Id", "UD6Id", "UD7Id", "UD8Id",
            "TimePeriod", "ViewMember", "Amount"
        ];

        private readonly int _mAmountOrdinal;
        private readonly DataTable _mData;
        private readonly Dictionary<string, int> _mMemberIds;

        // Source ordinals for the 17 member columns, in output order.
        private readonly int[] _mMemberOrdinals;
        private readonly int _mSnapshotId;
        private readonly int _mTimeOrdinal;
        private readonly int _mViewOrdinal;

        private int _mRowIndex = -1;

        public int FieldCount => OutputColumns.Length;

        public bool Read()
        {
            _mRowIndex++;
            return _mRowIndex < _mData.Rows.Count;
        }

        public object GetValue(int i)
        {
            var row = _mData.Rows[_mRowIndex];

            // 0 = SnapshotId, 1..17 = resolved MemberIds, 18 = Time,
            // 19 = View, 20 = Amount.
            if (i == 0)
                return _mSnapshotId;

            if (i >= 1 && i <= MemberColumns.Length)
            {
                var raw = row[_mMemberOrdinals[i - 1]];
                var name = raw == DBNull.Value ? string.Empty : Convert.ToString(raw);

                if (!_mMemberIds.TryGetValue(name, out var id))
                {
                    throw new InvalidOperationException(
                        $"ZDTS: no MemberId for '{name}' in column '{MemberColumns[i - 1]}'.");
                }

                return id;
            }

            if (i == MemberColumns.Length + 1)
                return Convert.ToString(row[_mTimeOrdinal]);

            if (i == MemberColumns.Length + 2)
                return Convert.ToString(row[_mViewOrdinal]);

            return Convert.ToDecimal(row[_mAmountOrdinal]);
        }

        public string GetName(int i) => OutputColumns[i];

        public int GetOrdinal(string name) => Array.IndexOf(OutputColumns, name);

        public Type GetFieldType(int i) =>
            i <= MemberColumns.Length ? typeof(int) :
            i <= MemberColumns.Length + 2 ? typeof(string) : typeof(decimal);

        public bool IsDBNull(int i) => false;

        public void Close() { }

        public void Dispose() { }

        public bool NextResult() => false;

        public int Depth => 0;

        public bool IsClosed => false;

        public int RecordsAffected => -1;

        // SqlBulkCopy only uses the members above; the rest of IDataReader is
        // required by the interface but never called.
        public object this[int i] => GetValue(i);

        public object this[string name] => GetValue(GetOrdinal(name));

        public int GetValues(object[] values) => throw new NotSupportedException();

        public bool GetBoolean(int i) => throw new NotSupportedException();

        public byte GetByte(int i) => throw new NotSupportedException();

        public long GetBytes(int i, long fo, byte[] buf, int bo, int len) => throw new NotSupportedException();

        public char GetChar(int i) => throw new NotSupportedException();

        public long GetChars(int i, long fo, char[] buf, int bo, int len) => throw new NotSupportedException();

        public IDataReader GetData(int i) => throw new NotSupportedException();

        public string GetDataTypeName(int i) => throw new NotSupportedException();

        public DateTime GetDateTime(int i) => throw new NotSupportedException();

        public decimal GetDecimal(int i) => Convert.ToDecimal(GetValue(i));

        public double GetDouble(int i) => throw new NotSupportedException();

        public float GetFloat(int i) => throw new NotSupportedException();

        public Guid GetGuid(int i) => throw new NotSupportedException();

        public short GetInt16(int i) => throw new NotSupportedException();

        public int GetInt32(int i) => Convert.ToInt32(GetValue(i));

        public long GetInt64(int i) => throw new NotSupportedException();

        public string GetString(int i) => Convert.ToString(GetValue(i)) ?? "";

        public DataTable GetSchemaTable() => throw new NotSupportedException();

        public ResolvedSnapshotRowReader(DataTable data, Dictionary<string, int> memberIds, int snapshotId)
        {
            _mData = data;
            _mMemberIds = memberIds;
            _mSnapshotId = snapshotId;

            _mMemberOrdinals = new int[MemberColumns.Length];

            for (var i = 0; i < MemberColumns.Length; i++)
                _mMemberOrdinals[i] = data.Columns.IndexOf(MemberColumns[i]);

            _mTimeOrdinal = data.Columns.IndexOf("Time");
            _mViewOrdinal = data.Columns.IndexOf("View");
            _mAmountOrdinal = data.Columns.IndexOf("Amount");
        }
    }

    /// <summary>
    ///     STEPs 1-5 of the snapshot load, shared by the FDX extract path
    ///     (CreateSnapshot) and the CSV import path (CreateSnapshotFromCsv).
    ///     Reserves the SnapshotId/partition, resolves member names to IDs,
    ///     bulk copies the fact rows, and marks the snapshot complete -
    ///     cleaning up the partial partition on any failure.
    /// </summary>
    private static OperationResult LoadSnapshot(
        SessionInfo si,
        DataTable dt,
        SnapshotCreateOptions options,
        string notes,
        ExtenderArgs args,
        Func<SessionInfo, ExtenderArgs, string, decimal, bool> taUpdater,
        decimal pctToAdd)
    {
        var result = new OperationResult();

        var snapshotName = options.BuildSnapshotName();

        var createdBy = si.UserName;

        var snapshotId = 0;
        var partitionOpened = false;
        var snapshotGuid = Guid.NewGuid();

        using DbConnInfo dbConnInfo = BRApi.Database.CreateApplicationDbConnInfo(si);

        var conn = dbConnInfo.GetSqlServerSpecificConnection();
        var tx = dbConnInfo.GetSqlServerSpecificTransaction();

        try
        {
            // STEP 1: Reserve SnapshotId, open its partition, write the
            // header as 'Running'.
            taUpdater?.Invoke(si, args, "Reserving partition", pctToAdd);

            using (var cmd = new SqlCommand(SqlReserveSnapshot, conn, tx))
            {
                cmd.CommandTimeout = 120;
                cmd.Parameters.Add("@SnapshotGuid", SqlDbType.UniqueIdentifier).Value = snapshotGuid;
                cmd.Parameters.Add("@SnapshotName", SqlDbType.NVarChar, 200).Value = snapshotName;
                cmd.Parameters.Add("@ExtractOptions", SqlDbType.NVarChar, -1).Value = options.ToJson();
                cmd.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 100).Value = createdBy;
                cmd.Parameters.Add("@Notes", SqlDbType.NVarChar, 4000).Value = (object)notes ?? DBNull.Value;

                snapshotId = Convert.ToInt32(cmd.ExecuteScalar());
                partitionOpened = true;
            }

            // STEP 2: Collect the distinct member names from the extract.
            taUpdater?.Invoke(si, args, "Collecting distinct member names from extract", pctToAdd);
            var memberNames = CollectDistinctMemberNames(dt);

            // STEP 3: Upsert them into the dictionary and read back the IDs.
            taUpdater?.Invoke(si, args, "Upserting member names into the dictionary", pctToAdd);
            var memberIds = UpsertAndFetchDictionary(conn, tx, memberNames);

            // STEP 4: Stream the fact rows straight into the snapshot table,
            // resolving MemberIds in memory as they go.
            taUpdater?.Invoke(si, args, "Bulk copying snapshot details", pctToAdd);

            using (var reader = new ResolvedSnapshotRowReader(dt, memberIds, snapshotId))
            using (var bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, tx))
            {
                bulkCopy.DestinationTableName = $"dbo.{Database.TblSnapshotData}";
                bulkCopy.BatchSize = 50000;
                bulkCopy.BulkCopyTimeout = 1800;

                // Map by name so RowHash (computed) is simply never mapped.
                for (var i = 0; i < reader.FieldCount; i++)
                    bulkCopy.ColumnMappings.Add(reader.GetName(i), reader.GetName(i));

                bulkCopy.WriteToServer(reader);
            }

            // STEP 5: Mark it complete.
            taUpdater?.Invoke(si, args, "Marking complete state", pctToAdd);

            using (var cmd = new SqlCommand(SqlCompleteSnapshot, conn, tx))
            {
                cmd.CommandTimeout = 300;
                cmd.Parameters.Add("@SnapshotId", SqlDbType.Int).Value = snapshotId;
                cmd.Parameters.Add("@SnapshotGuid", SqlDbType.UniqueIdentifier).Value = snapshotGuid;
                cmd.Parameters.Add("@RowsLoaded", SqlDbType.Int).Value = dt.Rows.Count;
                cmd.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 100).Value = createdBy;
                cmd.ExecuteNonQuery();
            }

            return result;
        }
        catch (Exception ex)
        {
            BRApi.ErrorLog.LogMessage(si, $"ZDTS: snapshot creation failed (SnapshotId {snapshotId}): {ex}");

            if (partitionOpened && snapshotId > 0)
                TryCleanupFailedSnapshot(si, conn, tx, snapshotId, createdBy);

            result.AddMessage($"Snapshot creation failed: {ex.Message}");
            return result;
        }
    }

    private static void TryCleanupFailedSnapshot(
        SessionInfo si,
        SqlConnection conn,
        SqlTransaction tx,
        int snapshotId,
        string actionBy)
    {
        try
        {
            using (var cmd = new SqlCommand(SqlCleanupFailed, conn, tx))
            {
                cmd.CommandTimeout = 300;
                cmd.Parameters.Add("@SnapshotId", SqlDbType.Int).Value = snapshotId;
                cmd.Parameters.Add("@ActionBy", SqlDbType.NVarChar, 100).Value = actionBy;
                cmd.ExecuteNonQuery();
            }

            BRApi.ErrorLog.LogMessage(si, $"ZDTS: cleaned up failed SnapshotId {snapshotId}.");
        }
        catch (Exception cleanupEx)
        {
            BRApi.ErrorLog.LogMessage(
                si,
                $"ZDTS: cleanup of failed SnapshotId {snapshotId} did not complete: {cleanupEx}");
        }
    }
}