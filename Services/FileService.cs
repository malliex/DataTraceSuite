using System;
using System.IO;

using DTS.Enums;

using OneStream.Shared.Common;
using OneStream.Shared.Wcf;

using StringHelper = DTS.Utils.StringHelper;

namespace DTS.Services;

internal static class FileService
{
    private const string SolutionFolder = "Data Trace Suite";
    private const string ComparisonsFolder = "Comparisons";
    private const string ImportFolder = "Import";
    private const string ExportFolder = "Export";

    public static void CreateCsvFile(SessionInfo si, byte[] fileBytes, FileType fileType, string fileName)
    {
        var xFolder = GetFolder(si, fileType);
        var filePath = Path.Combine(xFolder.XFFolder.FullName, $"{fileName}.csv");

        var xFileInfo = new XFFileInfo(xFolder.XFFolder.FileSystemLocation, filePath);
        var xFile = new XFFile(xFileInfo, "", fileBytes);
        BRApi.FileSystem.InsertOrUpdateFile(si, xFile);
    }

    // Reads a file's full content by its full path (as returned by a file
    // browse control, e.g. Parameters.PrmImportFile) rather than a bare name
    // combined with a known folder - see TryGetFileFullName for that case.
    public static bool TryGetFileBytes(SessionInfo si, FileType fileType, string fileFullName, out byte[] fileBytes)
    {
        fileBytes = null;

        if (string.IsNullOrEmpty(fileFullName))
            return false;

        var sFolder = GetFolder(si, fileType);

        var xFile = BRApi.FileSystem.GetFile(si, sFolder.XFFolder.FileSystemLocation, fileFullName, true, true);

        if (xFile?.XFFile?.ContentFileBytes == null)
            return false;

        fileBytes = xFile.XFFile.ContentFileBytes;
        return true;
    }

    // Resolves a file name (as stored in the solution folder, extension
    // included) to its full path, confirming the file actually exists first -
    // callers branch on the bool to show a "pick/wait for a file" message
    // instead of building a path to something that isn't there.
    public static bool TryGetFileFullName(SessionInfo si, FileType fileType, string fileName, out string fileFullName)
    {
        fileFullName = null;

        if (string.IsNullOrEmpty(fileName))
            return false;

        var sFolder = GetFolder(si, fileType);
        var candidateFullName = Path.Combine(sFolder.XFFolder.FullName, fileName);

        var xFile = BRApi.FileSystem.GetFile(
            si,
            sFolder.XFFolder.FileSystemLocation,
            candidateFullName,
            false,
            true);

        if (xFile == null)
            return false;

        fileFullName = candidateFullName;
        return true;
    }

    public static XFFolderEx GetFolder(SessionInfo si, FileType fileType)
    {
        return fileType switch
        {
            FileType.Comparison => GetComparisonsFolder(si),
            FileType.Import => GetImportFolder(si),
            FileType.Export => GetExportFolder(si),
            _ => throw new ArgumentOutOfRangeException(nameof(fileType), fileType, null)
        };
    }

    public static void CreateSolutionFolders(SessionInfo si)
    {
        GetSolutionFolder(si);
        GetComparisonsFolder(si);
        GetImportFolder(si);
        GetExportFolder(si);
    }

    public static void DeleteSolutionFolders(SessionInfo si)
    {
        var sFolder = GetSolutionFolder(si);
        BRApi.FileSystem.DeleteFolder(si, sFolder.XFFolder.FileSystemLocation, sFolder.XFFolder.FullName, true);
    }

    private static XFFolderEx GetSolutionFolder(SessionInfo si)
    {
        var pubFolder = GetDocumentsPublicFolder(si);

        BRApi.FileSystem.CreateFullFolderPathIfNecessary(
            si,
            pubFolder.XFFolder.FileSystemLocation,
            pubFolder.XFFolder.FullName,
            SolutionFolder);

        var sFolderFullname = Path.Combine(pubFolder.XFFolder.FullName, SolutionFolder);

        return BRApi.FileSystem.GetFolder(si, pubFolder.XFFolder.FileSystemLocation, sFolderFullname);
    }

    private static XFFolderEx GetComparisonsFolder(SessionInfo si)
    {
        var sFolder = GetSolutionFolder(si);

        BRApi.FileSystem.CreateFullFolderPathIfNecessary(
            si,
            sFolder.XFFolder.FileSystemLocation,
            sFolder.XFFolder.FullName,
            ComparisonsFolder);

        var sFolderFullname = Path.Combine(sFolder.XFFolder.FullName, ComparisonsFolder);

        return BRApi.FileSystem.GetFolder(si, sFolder.XFFolder.FileSystemLocation, sFolderFullname);
    }

    private static XFFolderEx GetImportFolder(SessionInfo si)
    {
        var sFolder = GetSolutionFolder(si);

        BRApi.FileSystem.CreateFullFolderPathIfNecessary(
            si,
            sFolder.XFFolder.FileSystemLocation,
            sFolder.XFFolder.FullName,
            ImportFolder);

        var sFolderFullname = Path.Combine(sFolder.XFFolder.FullName, ImportFolder);

        return BRApi.FileSystem.GetFolder(si, sFolder.XFFolder.FileSystemLocation, sFolderFullname);
    }

    private static XFFolderEx GetExportFolder(SessionInfo si)
    {
        var sFolder = GetSolutionFolder(si);

        BRApi.FileSystem.CreateFullFolderPathIfNecessary(
            si,
            sFolder.XFFolder.FileSystemLocation,
            sFolder.XFFolder.FullName,
            ExportFolder);

        var sFolderFullname = Path.Combine(sFolder.XFFolder.FullName, ExportFolder);

        return BRApi.FileSystem.GetFolder(si, sFolder.XFFolder.FileSystemLocation, sFolderFullname);
    }

    private static XFFolderEx GetDocumentsPublicFolder(SessionInfo si)
    {
        var documentsFolderPath = StringHelper.JoinWithBackSlash("Documents", "Public");
        var appDatabaseFolderPath = BRApi.FileSystem.GetFolder(
            si,
            FileSystemLocation.ApplicationDatabase,
            documentsFolderPath);

        return appDatabaseFolderPath;
    }
}