# Survey: does the `lib/<framework>/` folder match the assembly inside it?

Evidence for [issue #4](https://github.com/fluentfoundation/redecker/issues/4), which proposes a rule and then gates it on whether the failure is common enough to justify parsing PE metadata. Run over the corpus cache with `Redecker.Corpus survey-tfm`.

**4205 packages, 9221 assemblies under `lib/`.**

| Verdict | Assemblies | Package's own | Bundled |
| --- | ---: | ---: | ---: |
| Match | 8557 | 6790 | 1767 |
| NoAttribute | 379 | 235 | 144 |
| Unmanaged | 0 | 0 | 0 |
| UnreadableFolder | 0 | 0 | 0 |
| Compatible | 198 | 99 | 99 |
| Incompatible | 87 | 35 | 52 |

Of the 87 incompatible pairings, 42 are between frameworks anyone still ships to; the other 45 sit in a dead platform — PCL profiles, Silverlight, Windows Phone, Windows Store, MonoAndroid, Xamarin, UAP, Tizen. Those comparisons are not wrong, but nobody can act on them, so a rule scoped to living frameworks fires on **22 of 4205 packages (0.52%)**.

## The findings a rule would produce

| Package | Folder | Assembly targets | Whose assembly |
| --- | --- | --- | --- |
| `EcoCore@7.2.0.17464` | `net40` | `.NETFramework,Version=v4.8` | bundled |
| `Hangfire.MemoryStorage@1.8.1.2` | `net40` | `.NETFramework,Version=v4.5` | its own |
| `Microsoft.AspNet.OData@7.8.0` | `net45` | `.NETFramework,Version=v4.5.2` | its own |
| `Microsoft.Azure.CosmosDB.BulkExecutor@1.8.9` | `net45` | `.NETFramework,Version=v4.6.1` | bundled |
| `Microsoft.Azure.CosmosDB.BulkExecutor@1.8.9` | `net451` | `.NETFramework,Version=v4.6.1` | bundled |
| `Microsoft.Azure.DataLake.USQL.SDK@1.4.211011` | `net45` | `.NETFramework,Version=v4.7.2` | bundled |
| `Microsoft.Azure.DataLake.USQL.SDK@1.4.211011` | `net45` | `.NETFramework,Version=v4.7.2` | bundled |
| `Microsoft.Azure.DataLake.USQL.SDK@1.4.211011` | `net45` | `.NETFramework,Version=v4.7.2` | bundled |
| `Microsoft.Azure.DataLake.USQL.SDK@1.4.211011` | `net45` | `.NETFramework,Version=v4.7.2` | bundled |
| `Microsoft.Azure.DataLake.USQL.SDK@1.4.211011` | `net45` | `.NETFramework,Version=v4.7.2` | bundled |
| `Microsoft.Azure.DataLake.USQL.SDK@1.4.211011` | `net45` | `.NETFramework,Version=v4.7.2` | bundled |
| `Microsoft.Azure.DataLake.USQL.SDK@1.4.211011` | `net45` | `.NETFramework,Version=v4.7.2` | bundled |
| `Microsoft.Azure.Workflows.WebJobs.Extension@1.44.16` | `netcoreapp3.1` | `.NETFramework,Version=v4.0` | bundled |
| `Microsoft.Azure.Workflows.WebJobs.Extension@1.44.16` | `netcoreapp3.1` | `.NETFramework,Version=v4.0` | bundled |
| `Microsoft.CodeCoverage@18.8.1` | `net8.0` | `.NETFramework,Version=v4.0` | bundled |
| `Microsoft.ReportingServices.ReportViewerControl.WebForms@150.1652.0` | `net40` | `.NETFramework,Version=v4.6` | bundled |
| `Microsoft.ReportingServices.ReportViewerControl.WebForms@150.1652.0` | `net40` | `.NETFramework,Version=v4.6` | bundled |
| `Microsoft.ReportingServices.ReportViewerControl.WebForms@150.1652.0` | `net40` | `.NETFramework,Version=v4.6` | bundled |
| `Microsoft.ReportingServices.ReportViewerControl.WebForms@150.1652.0` | `net40` | `.NETFramework,Version=v4.6` | bundled |
| `Microsoft.ReportingServices.ReportViewerControl.WebForms@150.1652.0` | `net40` | `.NETFramework,Version=v4.6` | bundled |
| `Microsoft.ReportingServices.ReportViewerControl.WebForms@150.1652.0` | `net40` | `.NETFramework,Version=v4.6` | bundled |
| `Microsoft.ReportingServices.ReportViewerControl.WebForms@150.1652.0` | `net40` | `.NETFramework,Version=v4.6` | bundled |
| `Microsoft.ReportingServices.ReportViewerControl.Winforms@150.1652.0` | `net40` | `.NETFramework,Version=v4.6` | bundled |
| `Microsoft.ReportingServices.ReportViewerControl.Winforms@150.1652.0` | `net40` | `.NETFramework,Version=v4.6` | bundled |
| `Microsoft.ReportingServices.ReportViewerControl.Winforms@150.1652.0` | `net40` | `.NETFramework,Version=v4.6` | bundled |
| `Microsoft.ReportingServices.ReportViewerControl.Winforms@150.1652.0` | `net40` | `.NETFramework,Version=v4.6` | bundled |
| `Microsoft.ReportingServices.ReportViewerControl.Winforms@150.1652.0` | `net40` | `.NETFramework,Version=v4.6` | bundled |
| `Microsoft.SharePointOnline.CSOM@16.1.27424.12000` | `net40-full` | `.NETFramework,Version=v4.5` | bundled |
| `Microsoft.VisualStudio.DesignTools.Extensibility@17.10.34916.79` | `net45` | `.NETStandard,Version=v2.0` | its own |
| `Microsoft.VisualStudio.DesignTools.Extensibility@17.10.34916.79` | `net45` | `.NETFramework,Version=v4.7.2` | bundled |
| `Microsoft.VisualStudio.DpiAwareness@7.10.34910` | `net46` | `.NETFramework,Version=v4.7.2` | its own |
| `Microsoft.VisualStudio.TextTemplating.15.0@16.10.31320.204` | `net45` | `.NETFramework,Version=v4.7.2` | its own |
| `Microsoft.VisualStudio.TextTemplating.Interfaces.10.0@17.0.32112.339` | `netstandard2.0` | `.NETFramework,Version=v4.0` | its own |
| `Microsoft.VisualStudio.TextTemplating.Interfaces.11.0@17.0.32112.339` | `netstandard2.0` | `.NETFramework,Version=v4.5` | its own |
| `Microsoft.VisualStudio.TextTemplating.Interfaces.15.0@16.10.31320.204` | `net45` | `.NETFramework,Version=v4.7.2` | its own |
| `Microsoft.VisualStudio.TextTemplating.VSHost.15.0@16.10.31321.278` | `net45` | `.NETFramework,Version=v4.7.2` | its own |
| `Microsoft.Web.Administration@11.1.0` | `netstandard1.5` | `.NETFramework,Version=v4.5` | its own |
| `System.Security.Cryptography.OpenSsl@5.0.0` | `net461` | `.NETFramework,Version=v4.7` | its own |
| `Xamarin.FFImageLoading.Forms@2.4.11.982` | `netstandard1.0` | `.NETStandard,Version=v1.1` | bundled |
| `Xamarin.FFImageLoading.Transformations@2.4.11.982` | `netstandard1.0` | `.NETStandard,Version=v1.1` | bundled |
| `Xamarin.FFImageLoading@2.4.11.982` | `netstandard1.0` | `.NETStandard,Version=v1.1` | bundled |
| `Xamarin.FFImageLoading@2.4.11.982` | `netstandard1.0` | `.NETStandard,Version=v1.1` | bundled |

## What it has to stay silent about

Folder and assembly differing is not the defect — a project targeting the folder being unable to load the assembly is. These pairings differ and are fine, and a rule that compared version numbers rather than asking NuGet about compatibility would report every one of them.

| Count | Folder | Assembly targets |
| ---: | --- | --- |
| 14 | `netcoreapp3.1` | `.NETStandard,Version=v2.0` |
| 11 | `netstandard2.1` | `.NETStandard,Version=v2.0` |
| 10 | `net8.0` | `.NETStandard,Version=v2.0` |
| 5 | `net452` | `.NETFramework,Version=v4.5` |
| 3 | `net472` | `.NETStandard,Version=v2.0` |
| 3 | `net472` | `.NETFramework,Version=v4.6.1` |
| 3 | `net472` | `.NETFramework,Version=v4.5` |
| 2 | `net10.0` | `.NETStandard,Version=v2.0` |
| 2 | `net46` | `.NETFramework,Version=v4.0` |
| 2 | `net472` | `.NETFramework,Version=v4.6.2` |
| 1 | `net46` | `.NETFramework,Version=v4.5.2` |
| 1 | `net461` | `.NETFramework,Version=v4.5.2` |
| 1 | `net461` | `.NETStandard,Version=v2.0` |
| 1 | `net462` | `.NETStandard,Version=v2.0` |
| 1 | `net462` | `.NETFramework,Version=v4.6.1` |
| 1 | `net462` | `.NETFramework,Version=v4.5.2` |
| 1 | `net47` | `.NETFramework,Version=v4.5.2` |
| 1 | `net471` | `.NETFramework,Version=v4.5.2` |
| 1 | `net472` | `.NETFramework,Version=v4.5.2` |
| 1 | `net472` | `.NETFramework,Version=v4.0` |
| 1 | `net48` | `.NETFramework,Version=v4.6.2` |
| 1 | `net8.0` | `.NETCoreApp,Version=v3.1` |
| 1 | `net8.0` | `.NETCoreApp,Version=v6.0` |
| 1 | `net8.0-android` | `.NETStandard,Version=v2.0` |
| 1 | `net8.0-ios` | `.NETStandard,Version=v2.0` |
| 1 | `net8.0-maccatalyst` | `.NETStandard,Version=v2.0` |
| 1 | `net8.0-windows10.0.17763.0` | `.NETCoreApp,Version=v6.0` |
| 1 | `net9.0` | `.NETCoreApp,Version=v8.0` |
| 1 | `net9.0` | `.NETStandard,Version=v2.0` |
| 1 | `netstandard2.0` | `.NETStandard,Version=v1.4` |
