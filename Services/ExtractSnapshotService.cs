using System.Collections.Generic;

using DTS.Constants;

using OneStream.Data.DataFrame.Abstractions;
using OneStream.Shared.Common;
using OneStream.Shared.Wcf;

namespace DTS.Services;

internal static class ExtractSnapshotService
{
    private const string SqlExtract = @$"
        SELECT
            md_Cube.MemberName     AS [Cube],
            md_Entity.MemberName   AS [Entity],
            md_Parent.MemberName   AS [Parent],
            md_Cons.MemberName     AS [Cons],
            md_Scenario.MemberName AS [Scenario],
            d.TimePeriod            AS [Time],
            d.ViewMember            AS [View],
            md_Account.MemberName  AS [Account],
            md_Flow.MemberName     AS [Flow],
            md_Origin.MemberName   AS [Origin],
            md_IC.MemberName       AS [IC],
            md_UD1.MemberName AS [UD1], md_UD2.MemberName AS [UD2], md_UD3.MemberName AS [UD3],
            md_UD4.MemberName AS [UD4], md_UD5.MemberName AS [UD5], md_UD6.MemberName AS [UD6],
            md_UD7.MemberName AS [UD7], md_UD8.MemberName AS [UD8],
            d.Amount AS [Amount]
        FROM dbo.{Database.TblSnapshotData} d WITH (NOLOCK)
        JOIN dbo.{Database.TblMemberDictionary} md_Cube     ON d.CubeId     = md_Cube.MemberId
        JOIN dbo.{Database.TblMemberDictionary} md_Entity   ON d.EntityId   = md_Entity.MemberId
        JOIN dbo.{Database.TblMemberDictionary} md_Parent   ON d.ParentId   = md_Parent.MemberId
        JOIN dbo.{Database.TblMemberDictionary} md_Cons     ON d.ConsId     = md_Cons.MemberId
        JOIN dbo.{Database.TblMemberDictionary} md_Scenario ON d.ScenarioId = md_Scenario.MemberId
        JOIN dbo.{Database.TblMemberDictionary} md_Account  ON d.AccountId  = md_Account.MemberId
        JOIN dbo.{Database.TblMemberDictionary} md_Flow     ON d.FlowId     = md_Flow.MemberId
        JOIN dbo.{Database.TblMemberDictionary} md_Origin   ON d.OriginId   = md_Origin.MemberId
        JOIN dbo.{Database.TblMemberDictionary} md_IC       ON d.ICId       = md_IC.MemberId
        JOIN dbo.{Database.TblMemberDictionary} md_UD1 ON d.UD1Id = md_UD1.MemberId
        JOIN dbo.{Database.TblMemberDictionary} md_UD2 ON d.UD2Id = md_UD2.MemberId
        JOIN dbo.{Database.TblMemberDictionary} md_UD3 ON d.UD3Id = md_UD3.MemberId
        JOIN dbo.{Database.TblMemberDictionary} md_UD4 ON d.UD4Id = md_UD4.MemberId
        JOIN dbo.{Database.TblMemberDictionary} md_UD5 ON d.UD5Id = md_UD5.MemberId
        JOIN dbo.{Database.TblMemberDictionary} md_UD6 ON d.UD6Id = md_UD6.MemberId
        JOIN dbo.{Database.TblMemberDictionary} md_UD7 ON d.UD7Id = md_UD7.MemberId
        JOIN dbo.{Database.TblMemberDictionary} md_UD8 ON d.UD8Id = md_UD8.MemberId
        WHERE d.SnapshotId = @SnapshotId;";

    public static IDataFrame GetExtractResult(SessionInfo si, int snapshotId)
    {
        DatabaseService.ValidateSnapshotIsCompleted(si, snapshotId, "extracted");

        using var dbConnApp = BRApi.Database.CreateApplicationDbConnInfo(si);

        var dbParamInfos = new List<DbParamInfo> { new("@SnapshotId", snapshotId) };

        return BRApi.Database.GetDataFrame(dbConnApp, "result", SqlExtract, dbParamInfos, true);
    }
}