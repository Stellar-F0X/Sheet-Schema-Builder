#include "SheetSchemaBuilderNativeLoader.h"
#include "HAL/PlatformProcess.h"
#include "Interfaces/IPluginManager.h"
#include "Misc/Paths.h"

namespace SheetSchemaBuilderNativeLoader
{
    static void* GNativeHandle = nullptr;
    static FSheetSchemaBuilderProcess GProcess = nullptr;

    static TArray<FString> GetNativeRelativePathCandidates()
    {
        TArray<FString> candidates;
#if PLATFORM_WINDOWS
        candidates.Add(TEXT("Binaries/ThirdParty/SheetSchemaBuilder/Win64/SheetSchemaBuilderNative.dll"));
#elif PLATFORM_LINUX
        candidates.Add(TEXT("Binaries/ThirdParty/SheetSchemaBuilder/Linux/libSheetSchemaBuilderNative.so"));
        candidates.Add(TEXT("Binaries/ThirdParty/SheetSchemaBuilder/Linux/SheetSchemaBuilderNative.so"));
#elif PLATFORM_MAC
        candidates.Add(TEXT("Binaries/ThirdParty/SheetSchemaBuilder/Mac/libSheetSchemaBuilderNative.dylib"));
        candidates.Add(TEXT("Binaries/ThirdParty/SheetSchemaBuilder/Mac/SheetSchemaBuilderNative.dylib"));
#endif
        return candidates;
    }
}

bool LoadSheetSchemaBuilderNative(FString& ErrorMessage)
{
#if SHEET_SCHEMA_BUILDER_WITH_NATIVE
    if (SheetSchemaBuilderNativeLoader::GNativeHandle != nullptr)
    {
        return SheetSchemaBuilderNativeLoader::GProcess != nullptr;
    }

    TSharedPtr<IPlugin> plugin = IPluginManager::Get().FindPlugin(TEXT("SheetSchemaBuilder"));
    if (!plugin.IsValid())
    {
        ErrorMessage = TEXT("SheetSchemaBuilder plugin was not found by Unreal PluginManager.");
        return false;
    }

    FString dllPath;
    for (const FString& relativePath : SheetSchemaBuilderNativeLoader::GetNativeRelativePathCandidates())
    {
        FString candidatePath = FPaths::Combine(plugin->GetBaseDir(), relativePath);
        if (FPaths::FileExists(candidatePath))
        {
            dllPath = candidatePath;
            break;
        }
    }

    if (dllPath.IsEmpty())
    {
        ErrorMessage = FString::Printf(TEXT("Native builder library was not found under plugin: %s"), *plugin->GetBaseDir());
        return false;
    }

    SheetSchemaBuilderNativeLoader::GNativeHandle = FPlatformProcess::GetDllHandle(*dllPath);
    if (SheetSchemaBuilderNativeLoader::GNativeHandle == nullptr)
    {
        ErrorMessage = FString::Printf(TEXT("Failed to load native builder library: %s"), *dllPath);
        return false;
    }

    SheetSchemaBuilderNativeLoader::GProcess = reinterpret_cast<FSheetSchemaBuilderProcess>(FPlatformProcess::GetDllExport(SheetSchemaBuilderNativeLoader::GNativeHandle, TEXT("SheetSchemaBuilder_Process")));
    if (SheetSchemaBuilderNativeLoader::GProcess == nullptr)
    {
        ErrorMessage = FString::Printf(TEXT("Native builder export was not found: SheetSchemaBuilder_Process (%s)"), *dllPath);
        UnloadSheetSchemaBuilderNative();
        return false;
    }

    return true;
#else
    ErrorMessage = TEXT("SheetSchemaBuilderNative.dll/lib was not found when the Unreal module was built.");
    return false;
#endif
}

FSheetSchemaBuilderProcess GetSheetSchemaBuilderNativeProcess()
{
    return SheetSchemaBuilderNativeLoader::GProcess;
}

void UnloadSheetSchemaBuilderNative()
{
#if SHEET_SCHEMA_BUILDER_WITH_NATIVE
    if (SheetSchemaBuilderNativeLoader::GNativeHandle != nullptr)
    {
        FPlatformProcess::FreeDllHandle(SheetSchemaBuilderNativeLoader::GNativeHandle);
        SheetSchemaBuilderNativeLoader::GNativeHandle = nullptr;
    }

    SheetSchemaBuilderNativeLoader::GProcess = nullptr;
#endif
}
