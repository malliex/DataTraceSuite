using System;
using System.Collections.Generic;

using DTS.Common;
using DTS.Constants;
using DTS.Dashboards;
using DTS.Enums;
using DTS.Extensions;
using DTS.Interfaces;

using OneStream.Shared.Common;
using OneStream.Shared.Wcf;

namespace DTS.Services;

internal static class NavigationService
{
    internal static XFSelectionChangedTaskResult Navigate(
        DashboardTaskArgs taskArgs)
    {
        var navUnitValue = taskArgs.Args.SelectionChangedTaskInfo.CustomSubstVarsWithUserSelectedValues.XFGetValue(
            Parameters.PrmNavUnit,
            string.Empty);

        var dbrd = GetDashboardForNavUnitValue(navUnitValue, taskArgs);

        return Navigate(dbrd);
    }

    internal static XFSelectionChangedTaskResult Navigate(
        IContentDashboard dbrd,
        XFSelectionChangedTaskResult tResult = null)
    {
        tResult ??= new XFSelectionChangedTaskResult
        {
            ChangeCustomSubstVarsInDashboard = true,
            ChangeSelectionChangedUIActionInDashboard = true,
            ModifiedCustomSubstVars = new Dictionary<string, string>(),
            ModifiedSelectionChangedUIActionInfo = new XFSelectionChangedUIActionInfo
            {
                SelectionChangedUIActionType = XFSelectionChangedUIActionType.Refresh,
                DashboardsToRedraw = DashboardNames.Layout
            }
        };

        UpdateNavbarUi(tResult.ModifiedCustomSubstVars, dbrd);
        UpdateContentUi(tResult.ModifiedCustomSubstVars, dbrd);

        return tResult;
    }

    internal static XFLoadDashboardTaskResult Load(IContentDashboard dbrd, XFLoadDashboardTaskResult tResult = null)
    {
        tResult ??= new XFLoadDashboardTaskResult
        {
            ChangeCustomSubstVarsInDashboard = true, ModifiedCustomSubstVars = new Dictionary<string, string>()
        };

        UpdateNavbarUi(tResult.ModifiedCustomSubstVars, dbrd);
        UpdateContentUi(tResult.ModifiedCustomSubstVars, dbrd);

        return tResult;
    }

    private static void UpdateNavbarUi(Dictionary<string, string> csVars, IContentDashboard dbrd)
    {
        foreach (var navUnit in Enum.GetValues<NavigationUnit>())
        {
            var displayFormat = navUnit == dbrd.NavUnit
                ? $"BackgroundColor = {Color.Accent}, BorderColor = {Color.Border},  HoverColor = {Color.Accent},  TextColor = White"
                : $"BackgroundColor = White, BorderColor = {Color.Border},  HoverColor = {Color.Hover},  TextColor = {Color.Text}";

            var name = Enum.GetName(navUnit);
            csVars.Add($"prm_Nav_{name}_DTS", displayFormat);
        }
    }

    private static void UpdateContentUi(Dictionary<string, string> csVars, IContentDashboard dbrd)
    {
        csVars.InsertOrAppend(Parameters.PrmContentDbrd, dbrd.Name);
    }

    private static IContentDashboard GetDashboardForNavUnitValue(string navUnitValue, DashboardTaskArgs taskArgs)
    {
        if (!Enum.TryParse<NavigationUnit>(navUnitValue, true, out var navUnit))
            throw new ArgumentOutOfRangeException(nameof(navUnit), navUnitValue, null);

        switch (navUnit)
        {
            case NavigationUnit.Snapshots:
                return new SnapshotsDashboard(taskArgs);
            case NavigationUnit.Create:
                return new CreateSnapshotDashboard(taskArgs);
            case NavigationUnit.Audit:
                return new AuditDashboard(taskArgs);
            case NavigationUnit.Comparisons:
                return new ComparisonsDashboard(taskArgs);
            case NavigationUnit.Home:
                return new HomeDashboard(taskArgs);
            case NavigationUnit.Settings:
                return new SettingsDashboard(taskArgs);
            case NavigationUnit.Spreadsheet:
                return new SpreadsheetDashboard(taskArgs);

            case NavigationUnit.Help:
            case NavigationUnit.Install:
            default:
                throw new ArgumentOutOfRangeException(nameof(navUnit), navUnit, null);
        }
    }
}