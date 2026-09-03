using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using DTS.Enums;

using OneStream.Data.DataFrame.Abstractions;
using OneStream.Shared.Common;

namespace DTS.Services;

internal static class CsvService
{
    // Just the header row - cheap even on a huge file, since ParseCsv is
    // lazy and this stops after the first record.
    public static string[] ReadCsvHeader(byte[] fileBytes) => ParseCsv(fileBytes).FirstOrDefault() ?? [];

    // Streams records (header included, as the first one) out of a CSV file,
    // one at a time, so a caller doing a full parse never has to hold a
    // second copy of the file as a list of records.
    public static IEnumerable<string[]> ParseCsv(byte[] fileBytes) => ParseRecords(DecodeText(fileBytes));

    public static void SaveDataFrameAsCsv(SessionInfo si, IDataFrame dt, FileType fileType, string fileName)
    {
        using var ms = new MemoryStream();

        using (var sw = new StreamWriter(ms, leaveOpen: true))
        {
            var columnsCount = dt.Columns.Count();
            var headers = new string[columnsCount];
            for (var i = 0; i < columnsCount; i++)
                headers[i] = dt.Columns.ElementAt(i).Name;
            sw.WriteLine(ConvertToStringCsv(headers));

            foreach (var rw in dt.Rows)
                sw.WriteLine(ConvertToStringCsv([.. rw]));
        }

        var fileBytes = ms.ToArray();
        FileService.CreateCsvFile(si, fileBytes, fileType, fileName);
    }

    private static string DecodeText(byte[] fileBytes)
    {
        var text = Encoding.UTF8.GetString(fileBytes);
        return text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
    }

    // Minimal RFC4180 reader: comma-separated, double-quote quoting with ""
    // as an escaped quote, quoted fields may contain embedded commas and
    // newlines. Matches what SaveDataFrameAsCsv above writes (every field
    // always quoted), but also accepts unquoted fields for external files.
    private static IEnumerable<string[]> ParseRecords(string text)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                        inQuotes = false;
                }
                else
                    field.Append(c);

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    yield return fields.ToArray();

                    fields.Clear();
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            yield return fields.ToArray();
        }
    }

    private static string ConvertToStringCsv(object[] value)
    {
        for (var i = 0; i < value.Length; i++)
            value[i] = QuoteValue(value[i] ?? "");
        return string.Join(',', value);
    }

    private static string ConvertToStringCsv(string[] value)
    {
        for (var i = 0; i < value.Length; i++)
            value[i] = QuoteValue(value[i]);
        return string.Join(',', value);
    }

    private static string QuoteValue(object value) =>
        string.Concat("\"", value.ToString()?.Replace("\"", "\"\""), "\"");
}