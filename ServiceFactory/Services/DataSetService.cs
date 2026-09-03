using System;
using System.Data;
using System.IO;
using System.Linq;

using DTS.Enums;
using DTS.Services;

using OneStream.Data.DataFrame;
using OneStream.Shared.Common;
using OneStream.Shared.Wcf;

using OneStreamWorkspacesApi.V800;

namespace DTS.ServiceFactory.Services;

public class DataSetService : IWsasDataSetV800
{
    public object GetDataSet(
        SessionInfo si,
        BRGlobals brGlobals,
        DashboardWorkspace workspace,
        DashboardDataSetArgs args)
    {
        try
        {
            if (brGlobals == null || workspace == null || args == null)
                return null;

            return args.DataSetName switch
            {
                "GetCubeNames" => GetCubeNames(si),
                "GetEntityNames" => GetEntityNames(si, args),
                "GetScenarioNames" => GetScenarioNames(si, args),
                "GetComparisons" => GetComparisons(si),
                _ => null
            };
        }
        catch (Exception ex)
        {
            throw new XFException(si, ex);
        }
    }

    private static DataTable GetComparisons(SessionInfo si)
    {
        var result = new DataFrame(
            "Comparisons",
            [
                new DataFrameColumn<string>("Name"),
                new DataFrameColumn<string>("Created"),
                new DataFrameColumn<int>("Num Lines")
            ]);

        var sFolder = FileService.GetFolder(si, FileType.Comparison);

        foreach (var fileInfoEx in BRApi.FileSystem.GetFilesInFolder(
                         si,
                         sFolder.XFFolder.FileSystemLocation,
                         sFolder.XFFolder.FullName,
                         XFFileType.All,
                         [""])
                    .OrderByDescending(f => f.XFFileInfo.TimeCreated))
        {
            if (!Path.GetExtension(fileInfoEx.XFFileInfo.FullName).XFEqualsIgnoreCase(".csv"))
                continue;

            var countLines = CountLines(
                BRApi.FileSystem.GetFile(
                    si,
                    fileInfoEx.XFFileInfo.FileSystemLocation,
                    fileInfoEx.XFFileInfo.FullName,
                    true,
                    true).XFFile.ContentFileBytes);

            result.AddRow(
                fileInfoEx.XFFileInfo.Name,
                fileInfoEx.XFFileInfo.TimeCreated.ToString("yyyy-MM-dd HH:mm:ss"),
                countLines);
        }

        return result.ToDataTable();
    }

    private static int CountLines(ReadOnlySpan<byte> data)
    {
        var count = 0;
        int index;

        while ((index = data.IndexOf((byte)'\n')) >= 0)
        {
            count++;
            data = data[(index + 1)..];
        }

        if (data.Length > 0)
            count++; // trailing partial line

        return count - 1;
    }

    private static DataTable GetCubeNames(SessionInfo si)
    {
        var result = new DataFrame(
            "CubeNames",
            [
                new DataFrameColumn<string>("Name"),
                new DataFrameColumn<string>("Description")
            ]
        );

        var cbInfos = BRApi.Finance.Cubes.GetCubeInfos(si);

        foreach (var cbInfo in cbInfos)
            result.AddRow(cbInfo.Cube.Name, cbInfo.Cube.Description);

        return result.ToDataTable();
    }

    private static DataTable GetEntityNames(SessionInfo si, DashboardDataSetArgs args)
    {
        var result = new DataFrame(
            "EntityNames",
            [
                new DataFrameColumn<string>("Name"),
                new DataFrameColumn<string>("Description")
            ]
        );

        var cbName = args.NameValuePairs.XFGetValue("cbName", string.Empty);
        if (string.IsNullOrWhiteSpace(cbName))
            return result.ToDataTable();

        var cbInfo = BRApi.Finance.Cubes.GetCubeInfo(si, cbName);
        if (cbInfo is null)
            throw new ArgumentException($"Invalid Cube Name: '{cbName}'");

        var etDimId = cbInfo.Cube.CubeDims.GetEntityDimId();
        var etDpk = new DimPk(DimType.Entity.Id, etDimId);
        var entities = BRApi.Finance.Members.GetAllMembers(si, etDpk, true);
        entities
           .RemoveAll(e => e.Name.XFEqualsIgnoreCase("None"));

        foreach (var et in entities.OrderBy(e => e.Name))
            result.AddRow(et.Name, et.NameAndDescription);

        return result.ToDataTable();
    }

    private static DataTable GetScenarioNames(SessionInfo si, DashboardDataSetArgs args)
    {
        var result = new DataFrame(
            "ScenarioNames",
            [
                new DataFrameColumn<string>("Name"),
                new DataFrameColumn<string>("Description")
            ]
        );

        var cbName = args.NameValuePairs.XFGetValue("cbName", string.Empty);
        if (string.IsNullOrWhiteSpace(cbName))
            return result.ToDataTable();

        var cbInfo = BRApi.Finance.Cubes.GetCubeInfo(si, cbName);
        if (cbInfo is null)
            throw new ArgumentException($"Invalid Cube Name: '{cbName}'");

        var snDimId = cbInfo.Cube.CubeDims.GetScenarioDimId();
        var snDpk = new DimPk(DimType.Scenario.Id, snDimId);
        var scenarios = BRApi.Finance.Members.GetAllMembers(si, snDpk, true);
        scenarios
           .RemoveAll(e => e.Name.XFEqualsIgnoreCase("None"));

        foreach (var sn in scenarios.OrderBy(e => e.Name))
            result.AddRow(sn.Name, sn.NameAndDescription);

        return result.ToDataTable();
    }
}