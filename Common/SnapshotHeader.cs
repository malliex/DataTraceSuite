using System;

namespace DTS.Common;

internal sealed class SnapshotHeader
(
    int snapshotId,
    Guid snapshotGuid,
    string snapshotName,
    string extractOptions,
    string createdBy,
    DateTime createdDateLocal,
    string status,
    int? rowCount,
    string notes
)
{
    public int SnapshotId { get; } = snapshotId;
    public Guid SnapshotGuid { get; } = snapshotGuid;
    public string SnapshotName { get; } = snapshotName;
    public string ExtractOptions { get; } = extractOptions;
    public string CreatedBy { get; } = createdBy;
    public DateTime CreatedDateLocal { get; } = createdDateLocal;
    public string Status { get; } = status;
    public int? RowCount { get; } = rowCount;
    public string Notes { get; } = notes;
}