#include "SheetSchemaBuilderNativeLoader.h"
#include "HAL/PlatformProcess.h"
#include "Interfaces/IPluginManager.h"
#include "Misc/Paths.h"

namespace SheetSchemaBuilderNativeLoader
{
    static void* GNativeHandle = nullptr;
}

bool LoadSheetSchemaBuilderNative(FString& ErrorMessage)
{
#if SHEET_SCHEMA_BUILDER_WITH_NATIVE
    if (SheetSchemaBuilderNativeLoader::GNativeHandle != nullptr)
    {
        return true;
    }

    TSharedPtr<IPlugin> plugin = IPluginManager::Get().FindPlugin(TEXT("SheetSchemaBuilder"));
    if (!plugin.IsValid())
    {
        ErrorMessage = TEXT("SheetSchemaBuilder plugin was not found by Unreal PluginManager.");
        return false;
    }

    FString dllPath = FPaths::Combine(plugin->GetBaseDir(), TEXT("Binaries/ThirdParty/SheetSchemaBuilder/Win64/SheetSchemaBuilderNative.dll"));
    SheetSchemaBuilderNativeLoader::GNativeHandle = FPlatformProcess::GetDllHandle(*dllPath);
    if (SheetSchemaBuilderNativeLoader::GNativeHandle == nullptr)
    {
        ErrorMessage = FString::Printf(TEXT("Failed to load native builder DLL: %s"), *dllPath);
        return false;
    }

    return true;
#else
    ErrorMessage = TEXT("SheetSchemaBuilderNative.dll/lib was not found when the Unreal module was built.");
    return false;
#endif
}

void UnloadSheetSchemaBuilderNative()
{
#if SHEET_SCHEMA_BUILDER_WITH_NATIVE
    if (SheetSchemaBuilderNativeLoader::GNativeHandle != nullptr)
    {
        FPlatformProcess::FreeDllHandle(SheetSchemaBuilderNativeLoader::GNativeHandle);
        SheetSchemaBuilderNativeLoader::GNativeHandle = nullptr;
    }
#endif
}
