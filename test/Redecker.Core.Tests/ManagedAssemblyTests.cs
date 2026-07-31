using NUnit.Framework;
using Redecker.Packages;

namespace Redecker.Tests;

[TestFixture]
public class ManagedAssemblyTests
{
    [TestCase(".NETFramework,Version=v4.7.2")]
    [TestCase(".NETCoreApp,Version=v8.0")]
    [TestCase(".NETStandard,Version=v2.0")]
    [TestCase(".NETCoreApp,Version=v8.0,Profile=windows")]
    public void Reads_the_attribute_the_compiler_wrote(string declared)
    {
        Assert.That(
            ManagedAssembly.TryReadTargetFramework(CompiledAssembly.Targeting(declared), out var read),
            Is.True);
        Assert.That(read, Is.EqualTo(declared));
    }

    [Test]
    public void Reads_a_managed_assembly_that_simply_has_no_attribute()
    {
        // Succeeds with null rather than failing: "no attribute" and "not an assembly" are
        // different facts, and only the second one means the caller should give up.
        Assert.That(
            ManagedAssembly.TryReadTargetFramework(CompiledAssembly.WithNoTargetFramework(), out var read),
            Is.True);
        Assert.That(read, Is.Null);
    }

    [TestCase("", TestName = "empty")]
    [TestCase("MZ", TestName = "a DOS header and nothing else")]
    [TestCase("this is a text file that somebody named .dll", TestName = "not a PE file at all")]
    public void Refuses_anything_that_is_not_a_managed_assembly(string content)
    {
        Assert.That(
            ManagedAssembly.TryReadTargetFramework(
                System.Text.Encoding.UTF8.GetBytes(content), out var read),
            Is.False);
        Assert.That(read, Is.Null);
    }

    [Test]
    public void Reads_a_real_assembly_off_disk()
    {
        // The fixtures above are all compiled by the same compiler in the same run, which would
        // hide a decoding bug that only shows up on somebody else's output. This assembly was
        // built by a different toolchain on a different machine.
        var path = typeof(NuGet.Frameworks.NuGetFramework).Assembly.Location;

        Assert.That(
            ManagedAssembly.TryReadTargetFramework(File.ReadAllBytes(path), out var read), Is.True);
        Assert.That(read, Does.StartWith(".NET"));
    }
}
