using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Redecker.MSBuild
{
    /// <summary>
    /// Reports package families that must carry one version but have been split across several.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately self-contained rather than referencing Redecker.Core. An MSBuild task is
    /// loaded into a long-lived host that may already have other versions of the same assemblies
    /// loaded, so every dependency a task drags in is a chance to break someone else's build.
    /// The rule is small enough that duplicating it costs less than that risk.
    /// </para>
    /// <para>
    /// The families are an input rather than a constant, so the same task expresses the default
    /// EF Core policy and any policy a repository states for itself.
    /// </para>
    /// </remarks>
    public sealed class LockstepFamilyCheck : Task
    {
        /// <summary>The declared package versions, normally <c>@(PackageVersion)</c>.</summary>
        public ITaskItem[] PackageVersions { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>Family prefixes whose members must all share one version.</summary>
        public ITaskItem[] LockstepPrefixes { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>Whether a split family fails the build rather than warning.</summary>
        public bool TreatAsError { get; set; } = true;

        /// <summary>The metadata holding the version; <c>Version</c> unless overridden.</summary>
        public string VersionMetadata { get; set; } = "Version";

        /// <summary>Runs the check.</summary>
        /// <returns>False when a split family is found and errors are requested.</returns>
        public override bool Execute()
        {
            if (LockstepPrefixes.Length == 0 || PackageVersions.Length == 0)
            {
                return true;
            }

            var split = false;

            foreach (var prefix in LockstepPrefixes.Select(p => p.ItemSpec).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var members = new List<KeyValuePair<string, string>>();

                foreach (var item in PackageVersions)
                {
                    var id = item.ItemSpec;
                    if (id == null ||
                        id.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        continue;
                    }

                    var version = item.GetMetadata(VersionMetadata);

                    // A reference with no version is governed by central package management; the
                    // PackageVersion item carries the constraint, so this one says nothing.
                    if (!string.IsNullOrWhiteSpace(version))
                    {
                        members.Add(new KeyValuePair<string, string>(id, version));
                    }
                }

                var versions = members
                    .Select(m => m.Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(v => v, StringComparer.Ordinal)
                    .ToList();

                if (versions.Count <= 1)
                {
                    continue;
                }

                split = true;

                var declared = string.Join(
                    ", ",
                    members.OrderBy(m => m.Key, StringComparer.Ordinal)
                           .Select(m => m.Key + " " + m.Value));

                var message =
                    prefix + "* packages are split across " + versions.Count + " versions: " +
                    string.Join(", ", versions) + ". Every package in this family must carry the " +
                    "same version. Declared: " + declared + ". Restore succeeds regardless, so " +
                    "this surfaces at run time as a missing type or a provider that does not " +
                    "match the core package.";

                if (TreatAsError)
                {
                    Log.LogError(
                        subcategory: null, errorCode: "RDK0003", helpKeyword: null,
                        file: null, lineNumber: 0, columnNumber: 0,
                        endLineNumber: 0, endColumnNumber: 0, message: message);
                }
                else
                {
                    Log.LogWarning(
                        subcategory: null, warningCode: "RDK0003", helpKeyword: null,
                        file: null, lineNumber: 0, columnNumber: 0,
                        endLineNumber: 0, endColumnNumber: 0, message: message);
                }
            }

            return !(split && TreatAsError);
        }
    }
}
