using System.IO;
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

        string nativeDirectory = Path.Combine(pluginDirectory, "Binaries", "ThirdParty", "SheetSchemaBuilder", "Win64");
        string nativeLibrary = Path.Combine(nativeDirectory, "SheetSchemaBuilderNative.lib");
        string nativeDll = Path.Combine(nativeDirectory, "SheetSchemaBuilderNative.dll");
        bool hasNativeLibrary = Target.Platform == UnrealTargetPlatform.Win64 && File.Exists(nativeLibrary) && File.Exists(nativeDll);
        PublicDefinitions.Add("SHEET_SCHEMA_BUILDER_WITH_NATIVE=" + (hasNativeLibrary ? "1" : "0"));

        if (hasNativeLibrary)
        {
            PublicAdditionalLibraries.Add(nativeLibrary);
            PublicDelayLoadDLLs.Add("SheetSchemaBuilderNative.dll");
            RuntimeDependencies.Add(nativeDll);
        }
    }
}
