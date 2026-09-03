using DTS.Common;
using DTS.Constants;
using DTS.Enums;
using DTS.Interfaces;
using DTS.Services;

using OneStream.Shared.Common;
using OneStream.Shared.Wcf;

namespace DTS.Dashboards;

internal sealed class ComparisonsDashboard(DashboardTaskArgs taskArgs) : IContentDashboard
{
    public string Name => DashboardNames.Comparisons;
    public DashboardTaskArgs TaskArgs { get; } = taskArgs;
    public NavigationUnit NavUnit => NavigationUnit.Comparisons;

    public XFSelectionChangedTaskResult OpenViaSpreadsheet()
    {
        var tResult = new XFSelectionChangedTaskResult();

        var prms = TaskArgs.Args.NameValuePairs;
        var fileName = prms.XFGetValue("fileName", string.Empty);

        if (!FileService.TryGetFileFullName(TaskArgs.Si, FileType.Comparison, fileName, out var fileFullName))
        {
            tResult.ShowMessageBox = true;
            tResult.Message = "Please select a file from grid";
            return tResult;
        }

        tResult = NavigationService.Navigate(new SpreadsheetDashboard(TaskArgs));
        tResult.ModifiedCustomSubstVars.Add(Parameters.PrmComparisonFullName, fileFullName);

        return tResult;
    }

    public XFSelectionChangedTaskResult DownloadComparison()
    {
        var prms = TaskArgs.Args.NameValuePairs;
        var fileName = prms.XFGetValue("fileName", string.Empty);

        if (!FileService.TryGetFileFullName(TaskArgs.Si, FileType.Comparison, fileName, out var fileFullName))
        {
            return new XFSelectionChangedTaskResult
            {
                ShowMessageBox = true, Message = "Please select a file from grid"
            };
        }

        return new XFSelectionChangedTaskResult
        {
            ChangeSelectionChangedNavigationInDashboard = true,
            ModifiedSelectionChangedNavigationInfo = new XFSelectionChangedNavigationInfo
            {
                SelectionChangedNavigationType = XFSelectionChangedNavigationType.OpenFile,
                SelectionChangedNavigationArgs =
                    $"FileSourceType=Application, UrlOrFullFileName=[{fileFullName}], OpenInXFPageIfPossible=False"
            }
        };
    }

    public XFSelectionChangedTaskResult ClearSolutionFolder()
    {
        var tResult = new XFSelectionChangedTaskResult
        {
            ChangeSelectionChangedUIActionInDashboard = true,
            ModifiedSelectionChangedUIActionInfo = new XFSelectionChangedUIActionInfo
            {
                SelectionChangedUIActionType = XFSelectionChangedUIActionType.OpenDialogWithNoButtonsAndRefresh,
                DashboardForDialog = DashboardNames.PopupComparisons,
                DashboardsToRedraw = DashboardNames.GridComparisons
            }
        };

        return tResult;
    }

    public XFSelectionChangedTaskResult ConfirmClearSolutionFolder()
    {
        var tResult = new XFSelectionChangedTaskResult
        {
            ChangeSelectionChangedUIActionInDashboard = true,
            ModifiedSelectionChangedUIActionInfo = new XFSelectionChangedUIActionInfo
            {
                SelectionChangedUIActionType = XFSelectionChangedUIActionType.CloseDialog,
                DashboardsToHide = DashboardNames.PopupComparisons
            }
        };

        var sFolder = FileService.GetFolder(TaskArgs.Si, FileType.Comparison);

        foreach (var xfFileInfoEx in BRApi.FileSystem.GetFilesInFolder(
                     TaskArgs.Si,
                     sFolder.XFFolder.FileSystemLocation,
                     sFolder.XFFolder.FullName,
                     XFFileType.All,
                     null))
            BRApi.FileSystem.DeleteFile(
                TaskArgs.Si,
                sFolder.XFFolder.FileSystemLocation,
                xfFileInfoEx.XFFileInfo.FullName);

        return tResult;
    }
}