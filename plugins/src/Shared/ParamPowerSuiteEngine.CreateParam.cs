// Shared.csproj has both UseWPF and UseWindowsForms, so the bare name
// "Binding" is ambiguous between Autodesk.Revit.DB.Binding and
// System.Windows.Forms.Binding (same category of collision as the
// ComboBox/Brushes ambiguity fixed elsewhere in Shared) — this alias is
// only needed because InstanceBinding/TypeBinding need a common declared
// type for the ternary below; everywhere else the concrete subtype
// (InstanceBinding, TypeBinding) is unambiguous on its own.
using RevitBinding = Autodesk.Revit.DB.Binding;
using System.IO;
using Autodesk.Revit.DB;

namespace OttawaWork.Shared;

public static partial class ParamPowerSuiteEngine
{
    /// <summary>Curated, individually-verified subset of Revit's parameter data types — enough to
    /// cover the common cases (text, a few numeric/unit specs, yes/no, currency, url) without
    /// guessing at SpecTypeId members that were never actually confirmed to exist.</summary>
    public static readonly (string Label, ForgeTypeId Type)[] DataTypeOptions =
    {
        ("Text", SpecTypeId.String.Text),
        ("Multiline Text", SpecTypeId.String.MultilineText),
        ("Integer", SpecTypeId.Int.Integer),
        ("Number", SpecTypeId.Number),
        ("Yes/No", SpecTypeId.Boolean.YesNo),
        ("Length", SpecTypeId.Length),
        ("Area", SpecTypeId.Area),
        ("Volume", SpecTypeId.Volume),
        ("Angle", SpecTypeId.Angle),
        ("Currency", SpecTypeId.Currency),
        ("URL", SpecTypeId.String.Url),
    };

    /// <summary>Curated subset of GroupTypeId ("Discipline" in the spec — the parameter group a
    /// bound parameter is filed under in Revit's own Properties/Type Properties dialogs).</summary>
    public static readonly (string Label, ForgeTypeId Group)[] ParameterGroupOptions =
    {
        ("Text", GroupTypeId.Text),
        ("Data", GroupTypeId.Data),
        ("General", GroupTypeId.General),
        ("Identity Data", GroupTypeId.IdentityData),
        ("Construction", GroupTypeId.Construction),
        ("Materials", GroupTypeId.Materials),
        ("Graphics", GroupTypeId.Graphics),
        ("Mechanical", GroupTypeId.Mechanical),
        ("Electrical", GroupTypeId.Electrical),
        ("Plumbing", GroupTypeId.Plumbing),
        ("Structural", GroupTypeId.Structural),
    };

    /// <summary>True if a project parameter with this exact name is already bound to anything —
    /// used both to skip re-creating one that already exists and to warn the user up front, since
    /// binding a second definition under the same name is exactly the ambiguous state Jammer exists
    /// to clean up, not something Create+Bind should casually cause.</summary>
    public static bool IsParameterBound(Document doc, string parameterName)
    {
        var iterator = doc.ParameterBindings.ForwardIterator();
        while (iterator.MoveNext())
            if (iterator.Key.Name == parameterName) return true;
        return false;
    }

    /// <summary>
    /// Creates (or reuses) a shared parameter definition in the given shared
    /// parameter file/group and binds it into the project across the given
    /// categories — the standard, API-supported way to add a project
    /// parameter programmatically (there is no "create a pure project
    /// parameter" call; see Din276Engine.EnsureKostengruppeParameter for the
    /// same technique applied to one hardcoded parameter). Unlike that
    /// method, the shared parameter file here is whatever path the Create
    /// Param tab's file picker gave — this tool is general-purpose, so it
    /// doesn't own a fixed file the way the DIN 276 tool does.
    ///
    /// Application.SharedParametersFilename is a global, session-wide Revit
    /// setting, so the previous value is always restored afterward via
    /// try/finally, success or failure — otherwise this could silently leave
    /// the firm's own shared-parameter-file setting pointed somewhere else
    /// for the rest of the session.
    /// </summary>
    public static bool CreateAndBindParameter(
        Document doc,
        string sharedParameterFilePath,
        string groupName,
        string parameterName,
        ForgeTypeId dataType,
        ForgeTypeId parameterGroup,
        bool isInstance,
        IReadOnlyCollection<BuiltInCategory> categories)
    {
        if (IsParameterBound(doc, parameterName)) return true;

        var categorySet = doc.Application.Create.NewCategorySet();
        foreach (var builtIn in categories)
        {
            var category = Category.GetCategory(doc, builtIn);
            if (category is { AllowsBoundParameters: true }) categorySet.Insert(category);
        }
        if (categorySet.IsEmpty) return false;

        var definition = OpenOrCreateDefinition(doc, sharedParameterFilePath, groupName, parameterName, dataType);
        if (definition is null) return false;

        RevitBinding binding = isInstance ? doc.Application.Create.NewInstanceBinding(categorySet) : doc.Application.Create.NewTypeBinding(categorySet);
        return doc.ParameterBindings.Insert(definition, binding, parameterGroup);
    }

    /// <summary>Ensures the file at <paramref name="path"/> is at least a valid, empty shared
    /// parameter file (Revit needs the standard header before OpenSharedParameterFile will accept
    /// it) — used when the Create Param tab's picker points at a path that doesn't exist yet, so
    /// "pick a shared parameter file" can also mean "start a new one" without a separate button.</summary>
    public static void EnsureSharedParameterFileExists(string path)
    {
        if (File.Exists(path)) return;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        File.WriteAllText(path, "# This is a Revit shared parameter file.\r\n# Do not edit manually.\r\n*META\tVERSION\tMINVERSION\r\nMETA\t2\t1\r\n");
    }

    private static ExternalDefinition? OpenOrCreateDefinition(Document doc, string sharedParameterFilePath, string groupName, string parameterName, ForgeTypeId dataType)
    {
        var app = doc.Application;
        var previousFile = app.SharedParametersFilename;
        try
        {
            app.SharedParametersFilename = sharedParameterFilePath;
            var file = app.OpenSharedParameterFile();
            if (file is null) return null;

            var group = file.Groups.get_Item(groupName) ?? file.Groups.Create(groupName);
            var definition = group.Definitions.get_Item(parameterName) as ExternalDefinition;
            definition ??= group.Definitions.Create(new ExternalDefinitionCreationOptions(parameterName, dataType)) as ExternalDefinition;
            return definition;
        }
        finally
        {
            app.SharedParametersFilename = previousFile;
        }
    }
}
