using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace Redecker.Tests;

/// <summary>
/// Compiles a real assembly declaring a chosen <c>TargetFrameworkAttribute</c>.
/// </summary>
/// <remarks>
/// <para>
/// RDK0010 reads PE metadata, so a hand-built byte array proves nothing — the point of the rule is
/// that it sees what the compiler actually wrote. Compiling per case also covers pairings no
/// package on nuget.org happens to ship, which is what makes it possible to test the compatible
/// direction as carefully as the incompatible one.
/// </para>
/// <para>
/// The attribute is a plain string with no validation behind it, so the assembly can claim any
/// framework — including one it could never run on. That is exactly the situation being tested.
/// </para>
/// </remarks>
internal static class CompiledAssembly
{
    /// <summary>An assembly whose <c>TargetFrameworkAttribute</c> reads <paramref name="targetFramework"/>.</summary>
    /// <param name="targetFramework">A long-form moniker, such as <c>.NETFramework,Version=v4.7.2</c>.</param>
    public static byte[] Targeting(string targetFramework)
    {
        var source = $"""
            [assembly: System.Runtime.Versioning.TargetFramework("{targetFramework}")]
            public class Marker;
            """;

        var compilation = CSharpCompilation.Create(
            "Contoso.Widgets",
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var buffer = new MemoryStream();
        var result = compilation.Emit(buffer);

        Assert.That(
            result.Success,
            Is.True,
            () => "fixture failed to compile: " +
                  string.Join("; ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        return buffer.ToArray();
    }

    /// <summary>An assembly with no <c>TargetFrameworkAttribute</c> at all, as pre-4.0 builds have.</summary>
    public static byte[] WithNoTargetFramework()
    {
        var compilation = CSharpCompilation.Create(
            "Contoso.Widgets",
            [CSharpSyntaxTree.ParseText("public class Marker;")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var buffer = new MemoryStream();
        var result = compilation.Emit(buffer);
        Assert.That(result.Success, Is.True);
        return buffer.ToArray();
    }
}
