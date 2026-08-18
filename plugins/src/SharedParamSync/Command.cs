using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMFlow.Shared;
using System.Windows.Forms;

namespace BIMFlow.SharedParamSync;

/// <summary>
/// Pure file-based tool — diffs two shared parameter text files, flags GUID
/// and name conflicts, and writes a merged file. Doesn't touch the open
/// document, so it works the same regardless of what's currently loaded.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class Command : BimFlowCommand
{
    protected override string PluginSlug => "sharedparamsync";

    protected override Result Run(ExternalCommandData commandData, ref string message)
    {
        var pathA = PickFile("Select the first shared parameter file (this one wins on conflicts)");
        if (pathA is null) return Result.Cancelled;

        var pathB = PickFile("Select the second shared parameter file to merge in");
        if (pathB is null) return Result.Cancelled;

        var fileA = SharedParameterFile.Parse(pathA);
        var fileB = SharedParameterFile.Parse(pathB);
        var diff = SharedParamDiff.Compare(fileA, fileB);

        var resultsWindow = new SharedParamDiffWindow(pathA, pathB, diff);
        if (resultsWindow.ShowDialog() != true)
            return Result.Cancelled;

        var (groups, mergedParams) = SharedParamDiff.Merge(fileA, fileB);

        var savePath = PickSaveFile();
        if (savePath is null) return Result.Cancelled;

        SharedParameterFile.Write(savePath, groups, mergedParams);

        TaskDialog.Show(
            "BIMFlow — Shared Param Sync",
            $"Merged file written to:\n{savePath}\n\n{mergedParams.Count} total parameter(s).");

        return Result.Succeeded;
    }

    private static string? PickFile(string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Shared parameter files (*.txt)|*.txt|All files (*.*)|*.*",
        };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
    }

    private static string? PickSaveFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save merged shared parameter file",
            Filter = "Shared parameter files (*.txt)|*.txt",
            FileName = "merged-shared-parameters.txt",
        };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
    }
}
