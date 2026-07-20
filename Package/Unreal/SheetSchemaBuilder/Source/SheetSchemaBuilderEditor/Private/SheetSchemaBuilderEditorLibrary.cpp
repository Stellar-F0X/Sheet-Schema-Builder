#include "SheetSchemaBuilderEditorLibrary.h"
#include "SheetSchemaBuilderNativeLoader.h"
#include "Misc/Paths.h"

namespace SheetSchemaBuilderEditor
{
    static FString GLastOutput;

#if SHEET_SCHEMA_BUILDER_WITH_NATIVE
    static void SHEET_SCHEMA_BUILDER_CALL AppendLog(const char* Message)
    {
        if (Message != nullptr)
        {
            GLastOutput += FString(UTF8_TO_TCHAR(Message));
        }
    }
#endif
}

int32 USheetSchemaBuilderEditorLibrary::RunSheetSchemaBuilder(const FString& IniPath, bool bForce)
{
    SheetSchemaBuilderEditor::GLastOutput.Empty();

#if SHEET_SCHEMA_BUILDER_WITH_NATIVE
    FString loadErrorMessage;
    if (!LoadSheetSchemaBuilderNative(loadErrorMessage))
    {
        SheetSchemaBuilderEditor::GLastOutput = loadErrorMessage;
        return 1;
    }

    FString fullIniPath = FPaths::ConvertRelativePathToFull(IniPath);
    FSheetSchemaBuilderProcess process = GetSheetSchemaBuilderNativeProcess();
    if (process == nullptr)
    {
        SheetSchemaBuilderEditor::GLastOutput = TEXT("SheetSchemaBuilder_Process export is not loaded.");
        return 1;
    }

    FTCHARToUTF8 iniPathUtf8(*fullIniPath);
    return process(iniPathUtf8.Get(), bForce ? 1 : 0, &SheetSchemaBuilderEditor::AppendLog);
#else
    SheetSchemaBuilderEditor::GLastOutput = TEXT("SheetSchemaBuilder native library is not supported on this Unreal target platform.");
    return 1;
#endif
}

FString USheetSchemaBuilderEditorLibrary::GetLastOutput()
{
    return SheetSchemaBuilderEditor::GLastOutput;
}
