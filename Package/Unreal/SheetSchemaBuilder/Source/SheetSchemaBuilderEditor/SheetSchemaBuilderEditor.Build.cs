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

        bool supportsNativeBuilder = Target.Platform == UnrealTargetPlatform.Win64;
        PublicDefinitions.Add("SHEET_SCHEMA_BUILDER_WITH_NATIVE=" + (supportsNativeBuilder ? "1" : "0"));

        if (supportsNativeBuilder)
        {
            string nativeBinary = Path.Combine(
                pluginDirectory,
                "Binaries",
                "ThirdParty",
                "SheetSchemaBuilder",
                "Win64",
                "SheetSchemaBuilderNative.dll");
            RuntimeDependencies.Add(nativeBinary);
        }
    }
}
