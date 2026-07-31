using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Redecker.Packages;

/// <summary>
/// The little bit of assembly metadata Redecker needs, read without loading anything.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="System.Reflection.Metadata"/> is in the shared framework, so this costs no dependency
/// — and unlike <c>Assembly.LoadFrom</c> it neither runs code, nor resolves references, nor cares
/// whether the assembly targets a framework this process could host. Reading a .NET Framework 4.0
/// assembly from a .NET 8 tool has to work, because that mismatch is exactly what this is for.
/// </para>
/// </remarks>
public static class ManagedAssembly
{
    /// <summary>
    /// Reads <c>System.Runtime.Versioning.TargetFrameworkAttribute</c> from an assembly image.
    /// </summary>
    /// <param name="image">The raw bytes of a <c>.dll</c>.</param>
    /// <param name="targetFramework">
    /// The attribute value, such as <c>.NETCoreApp,Version=v8.0</c>; <see langword="null"/> when the
    /// assembly carries no such attribute, which is normal for anything built before .NET 4.0.
    /// </param>
    /// <returns>
    /// <see langword="false"/> when <paramref name="image"/> is not a managed assembly at all — a
    /// native binary, or a resource-only module. Callers should stay silent rather than guess.
    /// </returns>
    public static bool TryReadTargetFramework(byte[] image, out string? targetFramework)
    {
        targetFramework = null;

        try
        {
            using var stream = new MemoryStream(image, writable: false);
            using var pe = new PEReader(stream);

            if (!pe.HasMetadata)
            {
                return false;
            }

            var metadata = pe.GetMetadataReader();
            if (!metadata.IsAssembly)
            {
                // A netmodule, or a manifest-less satellite: no assembly-level attributes to read.
                return false;
            }

            foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
            {
                var attribute = metadata.GetCustomAttribute(handle);
                if (!IsTargetFrameworkAttribute(metadata, attribute))
                {
                    continue;
                }

                var blob = metadata.GetBlobReader(attribute.Value);

                // Fixed layout: a two-byte prolog of 0x0001, then a length-prefixed UTF-8 string.
                // Decoded by hand rather than through CustomAttribute.DecodeValue, which would want
                // a type provider to resolve a signature already known to be a single string.
                if (blob.Length < 2 || blob.ReadUInt16() != 1)
                {
                    return true;
                }

                targetFramework = blob.ReadSerializedString();
                return true;
            }

            return true;
        }
        catch (Exception ex) when (ex is BadImageFormatException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool IsTargetFrameworkAttribute(MetadataReader metadata, CustomAttribute attribute)
    {
        StringHandle @namespace;
        StringHandle name;

        switch (attribute.Constructor.Kind)
        {
            case HandleKind.MemberReference:
                var member = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                if (member.Parent.Kind != HandleKind.TypeReference)
                {
                    return false;
                }

                var reference = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
                @namespace = reference.Namespace;
                name = reference.Name;
                break;

            case HandleKind.MethodDefinition:
                var method = metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
                var declaring = metadata.GetTypeDefinition(method.GetDeclaringType());
                @namespace = declaring.Namespace;
                name = declaring.Name;
                break;

            default:
                return false;
        }

        return metadata.StringComparer.Equals(name, "TargetFrameworkAttribute") &&
               metadata.StringComparer.Equals(@namespace, "System.Runtime.Versioning");
    }
}
