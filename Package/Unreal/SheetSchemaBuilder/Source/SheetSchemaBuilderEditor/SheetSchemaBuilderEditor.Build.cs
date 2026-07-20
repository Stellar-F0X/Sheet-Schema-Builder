using System.IO;
using System.Linq;
using UnrealBuildTool;

public class SheetSchemaBuilderEditor : ModuleRules
{
    public SheetSchemaBuilderEditor(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

        PublicDependencyModuleNames.AddRange(new[] { "Core", "CoreUObject", "Engine" });
        PrivateDependencyModuleNames.AddRange(new[] { "Projects", "UnrealEd" });

        string pluginDirectory = Path.GetFullPath(Path.Combine(ModuleDirectory, "..", ".."));
        PublicIncludePaths.Add(Path.Combine(pluginDirectory, "Source", "ThirdParty", "SheetSchemaBuilderNative"));

        string nativeDirectory = Path.Combine(pluginDirectory, "Binaries", "ThirdParty", "SheetSchemaBuilder", GetNativePlatformDirectory(Target.Platform));
        string nativeBinary = GetNativeBinaryPath(Target.Platform, nativeDirectory);
        bool supportsNativeBuilder = string.IsNullOrWhiteSpace(nativeBinary) == false;
        PublicDefinitions.Add("SHEET_SCHEMA_BUILDER_WITH_NATIVE=" + (supportsNativeBuilder ? "1" : "0"));

        if (supportsNativeBuilder && File.Exists(nativeBinary))
        {
            RuntimeDependencies.Add(nativeBinary);
        }
    }

    private static string GetNativePlatformDirectory(UnrealTargetPlatform platform)
    {
        if (platform == UnrealTargetPlatform.Win64)
        {
            return "Win64";
        }

        if (platform == UnrealTargetPlatform.Linux)
        {
            return "Linux";
        }

        if (platform == UnrealTargetPlatform.Mac)
        {
            return "Mac";
        }

        return string.Empty;
    }

    private static string GetNativeBinaryPath(UnrealTargetPlatform platform, string nativeDirectory)
    {
        if (string.IsNullOrWhiteSpace(nativeDirectory))
        {
            return string.Empty;
        }

        string[] candidates;
        if (platform == UnrealTargetPlatform.Win64)
        {
            candidates = new[] { "SheetSchemaBuilderNative.dll" };
        }
        else if (platform == UnrealTargetPlatform.Linux)
        {
            candidates = new[] { "libSheetSchemaBuilderNative.so", "SheetSchemaBuilderNative.so" };
        }
        else if (platform == UnrealTargetPlatform.Mac)
        {
            candidates = new[] { "libSheetSchemaBuilderNative.dylib", "SheetSchemaBuilderNative.dylib" };
        }
        else
        {
            return string.Empty;
        }

        return candidates.Select(candidate => Path.Combine(nativeDirectory, candidate)).FirstOrDefault(File.Exists) ?? Path.Combine(nativeDirectory, candidates[0]);
    }
}
