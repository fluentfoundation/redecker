using System.Xml.Linq;
using Ratchet.Hints;

namespace Ratchet.Projects;

/// <summary>A package version declared by a project or by central package management.</summary>
/// <param name="PackageId">The package identifier.</param>
/// <param name="Version">The declared version, if the item carries one.</param>
/// <param name="File">The file the declaration came from.</param>
/// <param name="Line">The line it appears on, for reporting.</param>
/// <param name="ItemType">Either <c>PackageVersion</c> or <c>PackageReference</c>.</param>
/// <param name="Condition">The condition on the item or its item group, if any.</param>
/// <param name="Label">The raw label, if any.</param>
/// <param name="Hint">The parsed hint, when the label carries one.</param>
/// <param name="HintError">Why a label that looked like a hint could not be parsed.</param>
public sealed record PackagePin(
    string PackageId,
    string? Version,
    string File,
    int Line,
    string ItemType,
    string? Condition,
    string? Label,
    Hint? Hint,
    string? HintError);

/// <summary>Reads package declarations, and their hints, out of MSBuild files.</summary>
public static class PinReader
{
    /// <summary>Reads every <c>PackageVersion</c> and <c>PackageReference</c> from a file.</summary>
    public static IReadOnlyList<PackagePin> ReadFile(string path)
    {
        var document = XDocument.Load(path, LoadOptions.SetLineInfo);
        return Read(document, path);
    }

    /// <summary>Reads every <c>PackageVersion</c> and <c>PackageReference</c> from a document.</summary>
    public static IReadOnlyList<PackagePin> Read(XDocument document, string path)
    {
        var pins = new List<PackagePin>();

        foreach (var element in document.Descendants())
        {
            var name = element.Name.LocalName;
            if (!name.Equals("PackageVersion", StringComparison.Ordinal) &&
                !name.Equals("PackageReference", StringComparison.Ordinal))
            {
                continue;
            }

            var id = Attribute(element, "Include") ?? Attribute(element, "Update");
            if (id is null)
            {
                continue;
            }

            // A label on the containing ItemGroup applies to everything inside it, which is the
            // natural place to put a hint that covers a whole family of packages.
            var label = Attribute(element, "Label") ?? Attribute(element.Parent, "Label");
            var condition = Attribute(element, "Condition") ?? Attribute(element.Parent, "Condition");

            Hint? hint = null;
            string? hintError = null;
            if (label is not null)
            {
                HintParser.TryParse(label, out hint, out hintError);
            }

            pins.Add(new PackagePin(
                id,
                Attribute(element, "Version") ?? element.Element(element.Name.Namespace + "Version")?.Value,
                path,
                (element as System.Xml.IXmlLineInfo).HasLineInfo()
                    ? ((System.Xml.IXmlLineInfo)element).LineNumber
                    : 0,
                name,
                condition,
                label,
                hint,
                hintError));
        }

        return pins;
    }

    private static string? Attribute(XElement? element, string name) =>
        element?.Attributes().FirstOrDefault(
            a => a.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;
}
