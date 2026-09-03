using System;
using System.Collections.Generic;

using DTS.Constants;

using OneStream.Data.DataFrame.Abstractions;
using OneStream.Shared.Common;
using OneStream.Shared.Wcf;

namespace DTS.Services;

internal static class CompareSnapshotsService
{
    public static IDataFrame GetComparisonResult(SessionInfo si, int snapIdA, int snapIdB)
    {
        if (snapIdA == snapIdB)
            throw new ArgumentException($"Cannot compare snapshot {snapIdA} to itself.");

        DatabaseService.ValidateSnapshotIsCompleted(si, snapIdA, "compared");
        DatabaseService.ValidateSnapshotIsCompleted(si, snapIdB, "compared");

        using var dbConnApp = BRApi.Database.CreateApplicationDbConnInfo(si);

        var dbParamInfos = new List<DbParamInfo> { new("@IdA", snapIdA), new("@IdB", snapIdB) };

        return BRApi.Database.GetDataFrame(dbConnApp, "result", SqlCompare, dbParamInfos, true);
    }

    // Join on RowHash (see ZDTS_Architecture.md §3.5/§10) so the 19-column
    // natural key collapses to one equality test. Both sides are outer-joined
    // so Added/Removed rows survive; COALESCE picks the non-NULL id in every
    // dictionary join since a matched RowHash guarantees both sides agree on
    // every dimension anyway. WHERE excludes Unchanged rows (equal Amount on
    // both sides) so only differences come back.
    internal const string SqlCompare = @$"
        ;WITH A AS (
            SELECT * FROM dbo.{Database.TblSnapshotData} WITH (NOLOCK) WHERE SnapshotId = @IdA
        ),
        B AS (
            SELECT * FROM dbo.{Database.TblSnapshotData} WITH (NOLOCK) WHERE SnapshotId = @IdB
        )
        SELECT
            ISNULL(A.Amount, 0) - ISNULL(B.Amount, 0) AS [Difference],
            CASE WHEN A.Amount IS NULL THEN 'Added'
                 WHEN B.Amount IS NULL THEN 'Removed'
                 ELSE 'Changed' END AS [ChangeType],
            A.Amount AS [Amount A],
            B.Amount AS [Amount B],
            md_Cube.MemberName     AS [Cube],
            md_Entity.MemberName   AS [Entity],
            md_Parent.MemberName   AS [Parent],
            md_Cons.MemberName     AS [Cons],
            md_Scenario.MemberName AS [Scenario],
            COALESCE(A.TimePeriod, B.TimePeriod) AS [Time],
            COALESCE(A.ViewMember, B.ViewMember) AS [View],
            md_Account.MemberName  AS [Account],
            md_Flow.MemberName     AS [Flow],
            md_Origin.MemberName   AS [Origin],
            md_IC.MemberName       AS [IC],
            md_UD1.MemberName AS [UD1], md_UD2.MemberName AS [UD2], md_UD3.MemberName AS [UD3],
            md_UD4.MemberName AS [UD4], md_UD5.MemberName AS [UD5], md_UD6.MemberName AS [UD6],
            md_UD7.MemberName AS [UD7], md_UD8.MemberName AS [UD8]
        FROM A
        FULL OUTER JOIN B ON A.RowHash = B.RowHash
        LEFT JOIN dbo.{Database.TblMemberDictionary} md_Cube     ON COALESCE(A.CubeId, B.CubeId)         = md_Cube.MemberId
        LEFT JOIN dbo.{Database.TblMemberDictionary} md_Entity   ON COALESCE(A.EntityId, B.EntityId)     = md_Entity.MemberId
        LEFT JOIN dbo.{Database.TblMemberDictionary} md_Parent   ON COALESCE(A.ParentId, B.ParentId)     = md_Parent.MemberId
        LEFT JOIN dbo.{Database.TblMemberDictionary} md_Cons     ON COALESCE(A.ConsId, B.ConsId)         = md_Cons.MemberId
        LEFT JOIN dbo.{Database.TblMemberDictionary} md_Scenario ON COALESCE(A.ScenarioId, B.ScenarioId) = md_Scenario.MemberId
        LEFT JOIN dbo.{Database.TblMemberDictionary} md_Account  ON COALESCE(A.AccountId, B.AccountId)   = md_Account.MemberId
        LEFT JOIN dbo.{Database.TblMemberDictionary} md_Flow     ON COALESCE(A.FlowId, B.FlowId)         = md_Flow.MemberId
        LEFT JOIN dbo.{Database.TblMemberDictionary} md_Origin   ON COALESCE(A.OriginId, B.OriginId)     = md_Origin.MemberId
        LEFT JOIN dbo.{Database.TblMemberDictionary} md_IC       ON COALESCE(A.ICId, B.ICId)             = md_IC.MemberId
        LEFT JOIN dbo.{Database.TblMemberDictionary} md_UD1 ON COALESCE(A.UD1Id, B.UD1Id) = md_UD1.MemberId
        LEFT JOIN dbo.{Database.TblMemberDictionary} md_UD2 ON COALESCE(A.UD2Id, B.UD2Id) = md_UD2.MemberId
        LEFT JOIN dbo.{Database.TblMemberDictionary} md_UD3 ON COALESCE(A.UD3Id, B.UD3Id) = md_UD3.MemberId
        LEFT JOIN dbo.{Database.TblMemberDictionary} md_UD4 ON COALESCE(A.UD4Id, B.UD4Id) = md_UD4.MemberId
        LEFT JOIN dbo.{Database.TblMemberDictionary} md_UD5 ON COALESCE(A.UD5Id, B.UD5Id) = md_UD5.MemberId
        LEFT JOIN dbo.{Database.TblMemberDictionary} md_UD6 ON COALESCE(A.UD6Id, B.UD6Id) = md_UD6.MemberId
        LEFT JOIN dbo.{Database.TblMemberDictionary} md_UD7 ON COALESCE(A.UD7Id, B.UD7Id) = md_UD7.MemberId
        LEFT JOIN dbo.{Database.TblMemberDictionary} md_UD8 ON COALESCE(A.UD8Id, B.UD8Id) = md_UD8.MemberId
        WHERE A.Amount IS NULL OR B.Amount IS NULL OR A.Amount <> B.Amount;";
}