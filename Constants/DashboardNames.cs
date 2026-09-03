namespace DTS.Constants;

internal static class DashboardNames
{
    // "One place" container special values
    public const string OnePlace = "00_OnePlace_DTS";
    public const string Layout = "00_Layout_DTS";

    // Content dashboards (IContentDashboard.Name)
    public const string Home = "01_Home_DTS";
    public const string Install = "01_Install_DTS";
    public const string Create = "01_Create_DTS";
    public const string Snapshots = "01_Snapshots_DTS";
    public const string Audit = "01_Audit_DTS";
    public const string Comparisons = "01_Comparisons_DTS";
    public const string Settings = "01_Settings_DTS";
    public const string Spreadsheet = "01_Spreadsheet_DTS";

    // Popups / dialogs
    public const string PopupInstall = "99_Install_Popup_DTS";
    public const string PopupSnapshots = "99_Snapshots_Popup_DTS";
    public const string PopupComparisons = "99_Comparisons_Popup_DTS";
    public const string PopupUninstall = "99_Uninstall_Popup_DTS";

    // Grids/panels redrawn by name
    public const string GridComparisons = "02_Comparisons_Grid_DTS";
    public const string GridSnapshotsA = "05a_Snapshots_A_DTS";
    public const string GridSnapshotsB = "05b_Snapshots_B_DTS";
    public const string GridSnapshotsSelected = "05c_Snapshots_Selected_DTS";

    // Dynamic grid component names (DashboardDynamicGridArgs.Component.Name)
    public const string DgrdSnapshots = "dgrd_Snapshots_DTS";
    public const string DgrdAudit = "dgrd_Audit_DTS";
}