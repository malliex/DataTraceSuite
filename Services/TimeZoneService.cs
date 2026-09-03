using System;

using DTS.Constants;

using OneStream.Shared.Common;
using OneStream.Shared.Wcf;

namespace DTS.Services;

internal static class TimeZoneService
{
    public static DateTime ToUserLocal(SessionInfo si, DateTime utcDateTime) =>
        ToUserLocal(utcDateTime, GetUserTimeZone(si));

    // Takes an already-resolved TimeZoneInfo so callers converting many
    // timestamps (e.g. every row of a grid page) can look it up once instead
    // of once per row.
    public static DateTime ToUserLocal(DateTime utcDateTime, TimeZoneInfo userTimeZone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), userTimeZone);

    public static DateTime ToUtc(SessionInfo si, DateTime userLocalDateTime) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(userLocalDateTime, DateTimeKind.Unspecified),
            GetUserTimeZone(si));

    public static TimeZoneInfo GetUserTimeZone(SessionInfo si)
    {
        var ws = BRApi.Dashboards.Workspaces.GetWorkspace(si, false, "Data Trace Suite (DTS)");
        var timeZoneSetting = BRApi.Dashboards.Parameters.GetLiteralParameterValue(
            si,
            false,
            ws.WorkspaceID,
            Parameters.PrmTimeZone).XFConvertToDouble();

        var sign = timeZoneSetting >= 0 ? "+" : "-";
        var totalMinutes = (int)(Math.Abs(timeZoneSetting) * 60);
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;

        var id = $"UTC{sign}{hours:D2}:{minutes:D2}";
        var offset = TimeSpan.FromHours(timeZoneSetting);

        return TimeZoneInfo.CreateCustomTimeZone(id, offset, id, id);
    }
}