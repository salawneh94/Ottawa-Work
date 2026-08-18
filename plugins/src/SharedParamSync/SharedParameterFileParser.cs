// UseWPF+UseWindowsForms together drops System.IO from implicit global
// usings (see Shared/LicenseStore.cs) — needed here for File/StreamWriter.
using File = System.IO.File;
using StreamWriter = System.IO.StreamWriter;

namespace BIMFlow.SharedParamSync;

public record SharedParamGroup(int Id, string Name);

public record SharedParamRecord(string Guid, string Name, int GroupId, string[] RawFields);

public class SharedParameterFile
{
    public List<SharedParamGroup> Groups { get; } = new();
    public List<SharedParamRecord> Params { get; } = new();

    public static SharedParameterFile Parse(string path)
    {
        var file = new SharedParameterFile();

        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;

            var fields = line.Split('\t');
            switch (fields[0])
            {
                case "GROUP" when fields.Length >= 3:
                    file.Groups.Add(new SharedParamGroup(int.Parse(fields[1]), fields[2]));
                    break;
                case "PARAM" when fields.Length >= 6:
                    file.Params.Add(new SharedParamRecord(fields[1], fields[2], int.Parse(fields[5]), fields));
                    break;
            }
        }

        return file;
    }

    /// <summary>Writes a valid shared parameter file from the given groups/params.</summary>
    public static void Write(string path, List<SharedParamGroup> groups, List<SharedParamRecord> parameters)
    {
        using var writer = new StreamWriter(path, false);
        writer.WriteLine("# This is a Revit shared parameter file.");
        writer.WriteLine("# Do not edit manually.");
        writer.WriteLine("*META\tVERSION\tMINVERSION");
        writer.WriteLine("META\t2\t1");
        writer.WriteLine("*GROUP\tID\tNAME");
        foreach (var group in groups)
            writer.WriteLine($"GROUP\t{group.Id}\t{group.Name}");

        writer.WriteLine("*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\tHIDEWHENNOVALUE");
        foreach (var param in parameters)
            writer.WriteLine(string.Join('\t', param.RawFields));
    }
}
