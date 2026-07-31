# Corpus sweep: prefix-microsoft-system

Generated 2026-07-31 20:45 UTC from nuget.org, ids starting with Microsoft. or System., published within 6 years.

Examined **2682** of 2682 selected packages, skipped 0.

| Rule | What it checks | Packages | Rate | Reading |
| --- | --- | ---: | ---: | --- |
| RDK0001 | dangling asset reference | 22 | 0.8% | plausible |
| RDK0005 | tool package not installable | 0 | 0.0% | no findings |
| RDK0006 | unimportable build file | 9 | 0.3% | plausible |
| RDK0007 | untracked output copy | 20 | 0.7% | plausible |
| RDK0008 | analyzer under a framework folder | 0 | 0.0% | no findings |
| RDK0010 | assembly does not match its framework folder | 16 | 0.6% | plausible |

## RDK0001 — dangling asset reference

- `Microsoft.BotFramework.Orchestrator@4.14.3` — build/native/Microsoft.BotFramework.Orchestrator.props references runtimes/win-x64/native/oc_abi.lib, which the package does not contain
- `Microsoft.BotFramework.Orchestrator@4.14.3` — build/native/Microsoft.BotFramework.Orchestrator.props references runtimes/win-x86/native/oc_abi.lib, which the package does not contain
- `Microsoft.BotFramework.Orchestrator@4.14.3` — build/netstandard2.1/Microsoft.BotFramework.Orchestrator.props references runtimes/win-x64/native/oc_abi.lib, which the package does not contain
- `Microsoft.BotFramework.Orchestrator@4.14.3` — build/netstandard2.1/Microsoft.BotFramework.Orchestrator.props references runtimes/win-x86/native/oc_abi.lib, which the package does not contain
- `Microsoft.CodeDom.Providers.DotNetCompilerPlatform@4.1.0` — build/net472/Microsoft.CodeDom.Providers.DotNetCompilerPlatform.targets references tools/roslyn-4.1.0, which the package does not contain
- `Microsoft.CognitiveServices.Speech@1.51.1` — build/monoandroid/Microsoft.CognitiveServices.Speech.targets references build/monoandroid/libs/arm64-v8a/libMicrosoft.CognitiveServices.Speech.core.so, which the package does not contain
- `Microsoft.CognitiveServices.Speech@1.51.1` — build/monoandroid/Microsoft.CognitiveServices.Speech.targets references build/monoandroid/libs/arm64-v8a/libMicrosoft.CognitiveServices.Speech.extension.audio.sys.so, which the package does not contain
- `Microsoft.CognitiveServices.Speech@1.51.1` — build/monoandroid/Microsoft.CognitiveServices.Speech.targets references build/monoandroid/libs/arm64-v8a/libMicrosoft.CognitiveServices.Speech.extension.kws.so, which the package does not contain
- `Microsoft.CognitiveServices.Speech@1.51.1` — build/monoandroid/Microsoft.CognitiveServices.Speech.targets references build/monoandroid/libs/arm64-v8a/libMicrosoft.CognitiveServices.Speech.extension.kws.ort.so, which the package does not contain
- `Microsoft.CognitiveServices.Speech@1.51.1` — build/monoandroid/Microsoft.CognitiveServices.Speech.targets references build/monoandroid/libs/armeabi-v7a/libMicrosoft.CognitiveServices.Speech.core.so, which the package does not contain
- `Microsoft.CognitiveServices.Speech@1.51.1` — build/monoandroid/Microsoft.CognitiveServices.Speech.targets references build/monoandroid/libs/armeabi-v7a/libMicrosoft.CognitiveServices.Speech.extension.audio.sys.so, which the package does not contain
- `Microsoft.CognitiveServices.Speech@1.51.1` — build/monoandroid/Microsoft.CognitiveServices.Speech.targets references build/monoandroid/libs/armeabi-v7a/libMicrosoft.CognitiveServices.Speech.extension.kws.so, which the package does not contain
- `Microsoft.CognitiveServices.Speech@1.51.1` — build/monoandroid/Microsoft.CognitiveServices.Speech.targets references build/monoandroid/libs/armeabi-v7a/libMicrosoft.CognitiveServices.Speech.extension.kws.ort.so, which the package does not contain
- `Microsoft.CognitiveServices.Speech@1.51.1` — build/monoandroid/Microsoft.CognitiveServices.Speech.targets references build/monoandroid/libs/x86_64/libMicrosoft.CognitiveServices.Speech.core.so, which the package does not contain
- `Microsoft.CognitiveServices.Speech@1.51.1` — build/monoandroid/Microsoft.CognitiveServices.Speech.targets references build/monoandroid/libs/x86_64/libMicrosoft.CognitiveServices.Speech.extension.audio.sys.so, which the package does not contain
- `Microsoft.CognitiveServices.Speech@1.51.1` — build/monoandroid/Microsoft.CognitiveServices.Speech.targets references build/monoandroid/libs/x86_64/libMicrosoft.CognitiveServices.Speech.extension.kws.so, which the package does not contain
- `Microsoft.CognitiveServices.Speech@1.51.1` — build/monoandroid/Microsoft.CognitiveServices.Speech.targets references build/monoandroid/libs/x86_64/libMicrosoft.CognitiveServices.Speech.extension.kws.ort.so, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/dyn/rt-dyn/arm/Debug/gtestd.lib, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/dyn/rt-dyn/arm/Debug/gtest_maind.lib, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/dyn/rt-dyn/arm/Release/gtest.lib, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/dyn/rt-dyn/arm/Release/gtest_main.lib, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/dyn/rt-dyn/arm/Debug/gtestd.dll, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/dyn/rt-dyn/arm/Debug/gtest_maind.dll, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/dyn/rt-dyn/arm/Debug/gtestd.pdb, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/dyn/rt-dyn/arm/Debug/gtest_maind.pdb, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/dyn/rt-dyn/arm/Release/gtest.dll, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/dyn/rt-dyn/arm/Release/gtest_main.dll, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/dyn/rt-dyn/arm/Release/gtest.pdb, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.dyn.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/dyn/rt-dyn/arm/Release/gtest_main.pdb, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.static.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.static.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/static/rt-dyn/arm/Debug/gtestd.lib, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.static.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.static.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/static/rt-dyn/arm/Debug/gtest_maind.lib, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.static.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.static.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/static/rt-dyn/arm/Release/gtest.lib, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.static.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.static.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/static/rt-dyn/arm/Release/gtest_main.lib, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.static.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.static.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/static/rt-dyn/arm/Debug/gtest.pdb, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.static.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.static.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/static/rt-dyn/arm/Debug/gtest_main.pdb, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.static.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.static.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/static/rt-dyn/arm/Release/gtest.pdb, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.static.rt-dyn@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.static.rt-dyn.targets references lib/native/v140/windesktop/msvcstl/static/rt-dyn/arm/Release/gtest_main.pdb, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.static.rt-static@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.static.rt-static.targets references lib/native/v140/windesktop/msvcstl/static/rt-static/arm/Debug/gtestd.lib, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.static.rt-static@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.static.rt-static.targets references lib/native/v140/windesktop/msvcstl/static/rt-static/arm/Debug/gtest_maind.lib, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.static.rt-static@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.static.rt-static.targets references lib/native/v140/windesktop/msvcstl/static/rt-static/arm/Release/gtest.lib, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.static.rt-static@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.static.rt-static.targets references lib/native/v140/windesktop/msvcstl/static/rt-static/arm/Release/gtest_main.lib, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.static.rt-static@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.static.rt-static.targets references lib/native/v140/windesktop/msvcstl/static/rt-static/arm/Debug/gtest.pdb, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.static.rt-static@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.static.rt-static.targets references lib/native/v140/windesktop/msvcstl/static/rt-static/arm/Debug/gtest_main.pdb, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.static.rt-static@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.static.rt-static.targets references lib/native/v140/windesktop/msvcstl/static/rt-static/arm/Release/gtest.pdb, which the package does not contain
- `Microsoft.googletest.v140.windesktop.msvcstl.static.rt-static@1.8.1.8` — build/native/Microsoft.googletest.v140.windesktop.msvcstl.static.rt-static.targets references lib/native/v140/windesktop/msvcstl/static/rt-static/arm/Release/gtest_main.pdb, which the package does not contain
- `Microsoft.Maui.Controls.Build.Tasks@10.0.90` — buildTransitive/netstandard2.0/Microsoft.Maui.Controls.targets references buildTransitive/netstandard2.0/MonoAndroid10/proguard.cfg, which the package does not contain
- `Microsoft.ML.OnnxRuntime@1.28.0` — build/native/Microsoft.ML.OnnxRuntime.props references runtimes/win-arm/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime@1.28.0` — build/netstandard2.0/Microsoft.ML.OnnxRuntime.props references runtimes/win-arm/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime@1.28.0` — build/netstandard2.1/Microsoft.ML.OnnxRuntime.props references runtimes/win-arm/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.DirectML@1.24.4` — build/native/Microsoft.ML.OnnxRuntime.DirectML.props references runtimes/win-arm/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.DirectML@1.24.4` — build/netstandard2.0/Microsoft.ML.OnnxRuntime.DirectML.props references runtimes/win-arm/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.DirectML@1.24.4` — build/netstandard2.1/Microsoft.ML.OnnxRuntime.DirectML.props references runtimes/win-arm/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Foundry@1.26.0` — build/netstandard2.0/Microsoft.ML.OnnxRuntime.Foundry.props references runtimes/win-arm/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Foundry@1.26.0` — build/netstandard2.1/Microsoft.ML.OnnxRuntime.Foundry.props references runtimes/win-arm/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu@1.28.0` — buildTransitive/native/Microsoft.ML.OnnxRuntime.Gpu.props references runtimes/win-arm64/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu@1.28.0` — buildTransitive/native/Microsoft.ML.OnnxRuntime.Gpu.props references runtimes/win-arm/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu@1.28.0` — buildTransitive/native/Microsoft.ML.OnnxRuntime.Gpu.props references runtimes/win-x64/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu@1.28.0` — buildTransitive/netstandard2.0/Microsoft.ML.OnnxRuntime.Gpu.props references runtimes/win-arm64/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu@1.28.0` — buildTransitive/netstandard2.0/Microsoft.ML.OnnxRuntime.Gpu.props references runtimes/win-arm/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu@1.28.0` — buildTransitive/netstandard2.0/Microsoft.ML.OnnxRuntime.Gpu.props references runtimes/win-x64/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu@1.28.0` — buildTransitive/netstandard2.1/Microsoft.ML.OnnxRuntime.Gpu.props references runtimes/win-arm64/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu@1.28.0` — buildTransitive/netstandard2.1/Microsoft.ML.OnnxRuntime.Gpu.props references runtimes/win-arm/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu@1.28.0` — buildTransitive/netstandard2.1/Microsoft.ML.OnnxRuntime.Gpu.props references runtimes/win-x64/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu.Linux@1.28.0` — buildTransitive/native/Microsoft.ML.OnnxRuntime.Gpu.Linux.props references runtimes/win-arm64/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu.Linux@1.28.0` — buildTransitive/native/Microsoft.ML.OnnxRuntime.Gpu.Linux.props references runtimes/win-arm/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu.Linux@1.28.0` — buildTransitive/native/Microsoft.ML.OnnxRuntime.Gpu.Linux.props references runtimes/win-x64/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu.Linux@1.28.0` — buildTransitive/netstandard2.0/Microsoft.ML.OnnxRuntime.Gpu.Linux.props references runtimes/win-arm64/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu.Linux@1.28.0` — buildTransitive/netstandard2.0/Microsoft.ML.OnnxRuntime.Gpu.Linux.props references runtimes/win-arm/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu.Linux@1.28.0` — buildTransitive/netstandard2.0/Microsoft.ML.OnnxRuntime.Gpu.Linux.props references runtimes/win-x64/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu.Linux@1.28.0` — buildTransitive/netstandard2.1/Microsoft.ML.OnnxRuntime.Gpu.Linux.props references runtimes/win-arm64/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu.Linux@1.28.0` — buildTransitive/netstandard2.1/Microsoft.ML.OnnxRuntime.Gpu.Linux.props references runtimes/win-arm/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu.Linux@1.28.0` — buildTransitive/netstandard2.1/Microsoft.ML.OnnxRuntime.Gpu.Linux.props references runtimes/win-x64/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu.Windows@1.28.0` — buildTransitive/native/Microsoft.ML.OnnxRuntime.Gpu.Windows.props references runtimes/win-arm64/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu.Windows@1.28.0` — buildTransitive/native/Microsoft.ML.OnnxRuntime.Gpu.Windows.props references runtimes/win-arm/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu.Windows@1.28.0` — buildTransitive/netstandard2.0/Microsoft.ML.OnnxRuntime.Gpu.Windows.props references runtimes/win-arm64/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu.Windows@1.28.0` — buildTransitive/netstandard2.0/Microsoft.ML.OnnxRuntime.Gpu.Windows.props references runtimes/win-arm/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu.Windows@1.28.0` — buildTransitive/netstandard2.1/Microsoft.ML.OnnxRuntime.Gpu.Windows.props references runtimes/win-arm64/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.Gpu.Windows@1.28.0` — buildTransitive/netstandard2.1/Microsoft.ML.OnnxRuntime.Gpu.Windows.props references runtimes/win-arm/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.MKLML@1.6.0` — build/native/Microsoft.ML.OnnxRuntime.MKLML.props references runtimes/win-x86/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.MKLML@1.6.0` — build/native/Microsoft.ML.OnnxRuntime.MKLML.props references runtimes/win-x86/native/onnxruntime.dll, which the package does not contain
- `Microsoft.ML.OnnxRuntime.MKLML@1.6.0` — build/netstandard1.1/Microsoft.ML.OnnxRuntime.MKLML.props references runtimes/win-x86/native/onnxruntime.lib, which the package does not contain
- `Microsoft.ML.OnnxRuntime.MKLML@1.6.0` — build/netstandard1.1/Microsoft.ML.OnnxRuntime.MKLML.props references runtimes/win-x86/native/onnxruntime.dll, which the package does not contain
- `Microsoft.ProjectReunion.InteractiveExperiences@0.8.12` — build/Microsoft.InteractiveExperiences.Capabilities.targets references build/build/Microsoft.InteractiveExperiences.Capabilities.props, which the package does not contain
- `Microsoft.ProjectReunion.InteractiveExperiences@0.8.12` — build/Microsoft.InteractiveExperiences.Capabilities.targets references build/build/Microsoft.InteractiveExperiences.Capabilities.props, which the package does not contain
- `Microsoft.ProjectReunion.WinUI@0.8.12` — build/Microsoft.ProjectReunion.Foundation.targets references build/Microsoft.ApplicationModel.Resources.targets, which the package does not contain
- `Microsoft.ProjectReunion.WinUI@0.8.12` — build/Microsoft.WinUI.targets references build/LiftedWinRTClassRegistrationsUnpackaged.xml, which the package does not contain
- `Microsoft.ProjectReunion.WinUI@0.8.12` — buildTransitive/Microsoft.ProjectReunion.Foundation.targets references buildTransitive/Microsoft.ApplicationModel.Resources.targets, which the package does not contain
- `Microsoft.ProjectReunion.WinUI@0.8.12` — buildTransitive/Microsoft.WinUI.targets references buildTransitive/LiftedWinRTClassRegistrationsUnpackaged.xml, which the package does not contain
- `Microsoft.Web.LibraryManager.Build@3.0.114` — build/Microsoft.Web.LibraryManager.Build.props references tools/netstandard2.0/Microsoft.Web.LibraryManager.dll, which the package does not contain
- `Microsoft.Windows.CsWin32@0.3.298` — build/Microsoft.Windows.CsWin32.targets references analyzers/dotnet/roslyn5.0, which the package does not contain
- `Microsoft.Windows.CsWinRT@2.3.1` — build/Microsoft.Windows.CsWinRT.Authoring.Transitive.targets references build/native/WinRT.Host.runtimeconfig.json, which the package does not contain
- `Microsoft.WindowsAppSDK.Foundation@2.3.5` — build/Microsoft.WindowsAppSDK.Foundation.targets references include/WindowsAppSDK-VersionInfo.cs, which the package does not contain
- `Microsoft.WindowsAppSDK.Foundation@2.3.5` — build/native/Microsoft.WindowsAppSDK.Foundation.targets references lib/uap10.0, which the package does not contain
- `Microsoft.WindowsAppSDK.Foundation@2.3.5` — build/native/WindowsAppSDK-Nuget-Native.WinRt.props references lib/uap10.0, which the package does not contain
- `Microsoft.WindowsAppSDK.Foundation@2.3.5` — buildTransitive/Microsoft.WindowsAppSDK.Foundation.targets references include/WindowsAppSDK-VersionInfo.cs, which the package does not contain
- `Microsoft.WindowsAppSDK.Foundation@2.3.5` — buildTransitive/native/Microsoft.WindowsAppSDK.Foundation.targets references lib/uap10.0, which the package does not contain
- `Microsoft.WindowsAppSDK.Foundation@2.3.5` — buildTransitive/native/WindowsAppSDK-Nuget-Native.WinRt.props references lib/uap10.0, which the package does not contain
- `Microsoft.WindowsAppSDK.InteractiveExperiences@2.1.3` — build/Microsoft.InteractiveExperiences.Capabilities.targets references build/build/Microsoft.InteractiveExperiences.Capabilities.props, which the package does not contain
- `Microsoft.WindowsAppSDK.InteractiveExperiences@2.1.3` — build/Microsoft.InteractiveExperiences.Capabilities.targets references build/build/Microsoft.InteractiveExperiences.Capabilities.props, which the package does not contain
- `Microsoft.WindowsAppSDK.InteractiveExperiences@2.1.3` — build/Microsoft.InteractiveExperiences.Common.targets references manifests/Microsoft.InteractiveExperiences.manifest, which the package does not contain
- `Microsoft.WindowsAppSDK.InteractiveExperiences@2.1.3` — buildTransitive/Microsoft.InteractiveExperiences.Capabilities.targets references buildTransitive/build/Microsoft.InteractiveExperiences.Capabilities.props, which the package does not contain
- `Microsoft.WindowsAppSDK.InteractiveExperiences@2.1.3` — buildTransitive/Microsoft.InteractiveExperiences.Capabilities.targets references buildTransitive/build/Microsoft.InteractiveExperiences.Capabilities.props, which the package does not contain
- `Microsoft.WindowsAppSDK.InteractiveExperiences@2.1.3` — buildTransitive/Microsoft.InteractiveExperiences.Common.targets references manifests/Microsoft.InteractiveExperiences.manifest, which the package does not contain
- `Microsoft.WindowsAppSDK.WinUI@2.3.2` — build/Microsoft.WinUI.References.targets references lib/uap10.0, which the package does not contain
- `Microsoft.WindowsAppSDK.WinUI@2.3.2` — buildTransitive/Microsoft.WinUI.References.targets references lib/uap10.0, which the package does not contain

## RDK0006 — unimportable build file

- `Microsoft.Azure.DataLake.USQL.SDK@1.4.211011` — build/ ships MSBuild files with no entry point named after the package
- `Microsoft.Azure.DataLake.USQL.SDK@1.4.211011` — build/runtime/ ships MSBuild files with no entry point named after the package
- `Microsoft.Azure.StreamAnalytics.CICD@3.0.0` — build/ ships MSBuild files with no entry point named after the package
- `Microsoft.DotNet.ILCompiler@10.0.10` — build/Microsoft.NETCore.Native.Publish.targets is not imported by anything inside the package
- `Microsoft.DotNet.ILCompiler@10.0.10` — build/Microsoft.NETCore.Native.Unix.targets is not imported by anything inside the package
- `Microsoft.DotNet.ILCompiler@10.0.10` — build/Microsoft.NETCore.Native.Windows.targets is not imported by anything inside the package
- `Microsoft.DotNet.ILCompiler@10.0.10` — build/Microsoft.NETCore.Native.targets is not imported by anything inside the package
- `Microsoft.Maui.Core@10.0.90` — buildTransitive/Microsoft.Maui.Core.BundledVersions.targets is not imported by anything inside the package
- `Microsoft.Maui.Core@10.0.90` — buildTransitive/WinUI.targets is not imported by anything inside the package
- `Microsoft.NET.Sdk.Razor@3.1.32` — build/netstandard2.0/Microsoft.NET.Sdk.Razor.CodeGeneration.targets is not imported by anything inside the package
- `Microsoft.NET.Sdk.Razor@3.1.32` — build/netstandard2.0/Microsoft.NET.Sdk.Razor.Compilation.targets is not imported by anything inside the package
- `Microsoft.NET.Sdk.Razor@3.1.32` — build/netstandard2.0/Microsoft.NET.Sdk.Razor.Component.targets is not imported by anything inside the package
- `Microsoft.NET.Sdk.Razor@3.1.32` — build/netstandard2.0/Microsoft.NET.Sdk.Razor.Configuration.targets is not imported by anything inside the package
- `Microsoft.NET.Sdk.Razor@3.1.32` — build/netstandard2.0/Microsoft.NET.Sdk.Razor.DesignTime.targets is not imported by anything inside the package
- `Microsoft.NET.Sdk.Razor@3.1.32` — build/netstandard2.0/Microsoft.NET.Sdk.Razor.GenerateAssemblyInfo.targets is not imported by anything inside the package
- `Microsoft.NET.Sdk.Razor@3.1.32` — build/netstandard2.0/Microsoft.NET.Sdk.Razor.MvcApplicationPartsDiscovery.targets is not imported by anything inside the package
- `Microsoft.NET.Sdk.Razor@3.1.32` — build/netstandard2.0/Microsoft.NET.Sdk.Razor.StaticWebAssets.targets is not imported by anything inside the package
- `Microsoft.ProjectReunion@0.8.12` — build/Microsoft.ProjectReunion.Metapackage.props is not imported by anything inside the package
- `Microsoft.ProjectReunion.InteractiveExperiences@0.8.12` — build/native/Microsoft.ProjectReunion.InteractiveExperiences.TransportPackage.targets is not imported by anything inside the package
- `Microsoft.ProjectReunion.InteractiveExperiences@0.8.12` — build/net5.0-windows10.0.17763.0/Microsoft.ProjectReunion.InteractiveExperiences.TransportPackage.targets is not imported by anything inside the package
- `Microsoft.ReactNative.Debug@0.78.15` — build/native/ ships MSBuild files with no entry point named after the package
- `Microsoft.VisualStudio.Azure.Fabric.MSBuild@1.7.9` — build/ ships MSBuild files with no entry point named after the package

## RDK0007 — untracked output copy

- `Microsoft.ApplicationInsights.DependencyCollector@2.23.0` — build/Microsoft.ApplicationInsights.DependencyCollector.targets copies 1 time(s) into the output directory without recording FileWrites
- `Microsoft.ApplicationInsights.PerfCounterCollector@2.23.0` — build/Microsoft.ApplicationInsights.PerfCounterCollector.targets copies 1 time(s) into the output directory without recording FileWrites
- `Microsoft.ApplicationInsights.Web@3.1.2` — build/Microsoft.ApplicationInsights.Web.targets copies 1 time(s) into the output directory without recording FileWrites
- `Microsoft.ApplicationInsights.WindowsServer@2.23.0` — build/Microsoft.ApplicationInsights.WindowsServer.targets copies 1 time(s) into the output directory without recording FileWrites
- `Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel@2.23.0` — build/Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel.targets copies 1 time(s) into the output directory without recording FileWrites
- `Microsoft.AspNetCore.Mvc.Testing@10.0.10` — build/net10.0/Microsoft.AspNetCore.Mvc.Testing.targets copies 1 time(s) into the output directory without recording FileWrites
- `Microsoft.Azure.Security.KeyGuardAttestation@1.1.6` — build/native/Microsoft.Azure.Security.KeyGuardAttestation.targets copies 1 time(s) into the output directory without recording FileWrites
- `Microsoft.CognitiveServices.Speech@1.51.1` — build/native/Microsoft.CognitiveServices.Speech.targets copies 5 time(s) into the output directory without recording FileWrites
- `Microsoft.Data.SqlClient.SNI@6.0.2` — build/net462/Microsoft.Data.SqlClient.SNI.targets copies 1 time(s) into the output directory without recording FileWrites
- `Microsoft.Data.SqlClient.SNI@6.0.2` — buildTransitive/net462/Microsoft.Data.SqlClient.SNI.targets copies 1 time(s) into the output directory without recording FileWrites
- `Microsoft.InformationProtection.File@1.18.124` — build/native/Microsoft.InformationProtection.File.targets copies 6 time(s) into the output directory without recording FileWrites
- `Microsoft.InformationProtection.Policy@1.18.124` — build/native/Microsoft.InformationProtection.Policy.targets copies 6 time(s) into the output directory without recording FileWrites
- `Microsoft.NET.Sdk.Functions@4.6.0` — build/Microsoft.NET.Sdk.Functions.Build.targets copies 2 time(s) into the output directory without recording FileWrites
- `Microsoft.ProjectReunion.WinUI@0.8.12` — build/Microsoft.UI.Xaml.Markup.Compiler.interop.targets copies 1 time(s) into the output directory without recording FileWrites
- `Microsoft.ProjectReunion.WinUI@0.8.12` — buildTransitive/Microsoft.UI.Xaml.Markup.Compiler.interop.targets copies 1 time(s) into the output directory without recording FileWrites
- `Microsoft.TeamFoundationServer.ExtendedClient@20.256.2` — build/Microsoft.TeamFoundationServer.ExtendedClient.targets copies 2 time(s) into the output directory without recording FileWrites
- `Microsoft.UI.Xaml@2.8.7` — build/Common.targets copies 1 time(s) into the output directory without recording FileWrites
- `Microsoft.UI.Xaml@2.8.7` — buildTransitive/Common.targets copies 1 time(s) into the output directory without recording FileWrites
- `Microsoft.Windows.CsWinRT@2.3.1` — build/Microsoft.Windows.CsWinRT.Authoring.targets copies 1 time(s) into the output directory without recording FileWrites
- `Microsoft.WindowsAppSDK.Foundation@2.3.5` — build/native/WindowsAppSDK-Nuget-Native.C.props copies 1 time(s) into the output directory without recording FileWrites
- `Microsoft.WindowsAppSDK.Foundation@2.3.5` — buildTransitive/native/WindowsAppSDK-Nuget-Native.C.props copies 1 time(s) into the output directory without recording FileWrites
- `Microsoft.WindowsAppSDK.WinUI@2.3.2` — build/Microsoft.UI.Xaml.Markup.Compiler.interop.targets copies 1 time(s) into the output directory without recording FileWrites
- `Microsoft.WindowsAppSDK.WinUI@2.3.2` — buildTransitive/Microsoft.UI.Xaml.Markup.Compiler.interop.targets copies 1 time(s) into the output directory without recording FileWrites
- `Microsoft.XmlSerializer.Generator@10.0.10` — build/Microsoft.XmlSerializer.Generator.targets copies 2 time(s) into the output directory without recording FileWrites
- `System.Data.SQLite.Core.FTS5@1.0.114.1` — build/net45/System.Data.SQLite.Core.FTS5.targets copies 1 time(s) into the output directory without recording FileWrites

## RDK0010 — assembly does not match its framework folder

- `Microsoft.AspNet.OData@7.8.0` — lib/net45/ contains an assembly built for .NETFramework,Version=v4.5.2
- `Microsoft.Azure.CosmosDB.BulkExecutor@1.8.9` — lib/net45/ contains an assembly built for .NETFramework,Version=v4.6.1
- `Microsoft.Azure.CosmosDB.BulkExecutor@1.8.9` — lib/net451/ contains an assembly built for .NETFramework,Version=v4.6.1
- `Microsoft.Azure.DataLake.USQL.SDK@1.4.211011` — lib/net45/ contains an assembly built for .NETFramework,Version=v4.7.2
- `Microsoft.Azure.Workflows.WebJobs.Extension@1.44.16` — lib/netcoreapp3.1/ contains an assembly built for .NETFramework,Version=v4.0
- `Microsoft.CodeCoverage@18.8.1` — lib/net8.0/ contains an assembly built for .NETFramework,Version=v4.0
- `Microsoft.ReportingServices.ReportViewerControl.WebForms@150.1652.0` — lib/net40/ contains an assembly built for .NETFramework,Version=v4.6
- `Microsoft.ReportingServices.ReportViewerControl.Winforms@150.1652.0` — lib/net40/ contains an assembly built for .NETFramework,Version=v4.6
- `Microsoft.SharePointOnline.CSOM@16.1.27424.12000` — lib/net40-full/ contains an assembly built for .NETFramework,Version=v4.5
- `Microsoft.VisualStudio.DesignTools.Extensibility@17.10.34916.79` — lib/net45/ contains an assembly built for .NETFramework,Version=v4.7.2
- `Microsoft.VisualStudio.DesignTools.Extensibility@17.10.34916.79` — lib/net45/ contains an assembly built for .NETStandard,Version=v2.0
- `Microsoft.VisualStudio.DpiAwareness@7.10.34910` — lib/net46/ contains an assembly built for .NETFramework,Version=v4.7.2
- `Microsoft.VisualStudio.TextTemplating.15.0@16.10.31320.204` — lib/net45/ contains an assembly built for .NETFramework,Version=v4.7.2
- `Microsoft.VisualStudio.TextTemplating.Interfaces.10.0@17.0.32112.339` — lib/netstandard2.0/ contains an assembly built for .NETFramework,Version=v4.0
- `Microsoft.VisualStudio.TextTemplating.Interfaces.11.0@17.0.32112.339` — lib/netstandard2.0/ contains an assembly built for .NETFramework,Version=v4.5
- `Microsoft.VisualStudio.TextTemplating.Interfaces.15.0@16.10.31320.204` — lib/net45/ contains an assembly built for .NETFramework,Version=v4.7.2
- `Microsoft.VisualStudio.TextTemplating.VSHost.15.0@16.10.31321.278` — lib/net45/ contains an assembly built for .NETFramework,Version=v4.7.2
- `System.Security.Cryptography.OpenSsl@5.0.0` — lib/net461/ contains an assembly built for .NETFramework,Version=v4.7
