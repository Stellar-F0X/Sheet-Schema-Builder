#include "SheetSchemaBuilderEditorLibrary.h"
#include "SheetSchemaBuilderNativeLoader.h"
#include "Misc/Paths.h"

#if SHEET_SCHEMA_BUILDER_WITH_NATIVE
#include "SheetSchemaBuilderNative.h"
#endif

namespace SheetSchemaBuilderEditor
{
    static FString GLastOutput;

#if SHEET_SCHEMA_BUILDER_WITH_NATIVE
    static void __cdecl AppendLog(const wchar_t* Message)
    {
        if (Message != nullptr)
        {
            GLastOutput += FString(Message);
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
    return SheetSchemaBuilder_Process(*fullIniPath, bForce ? 1 : 0, &SheetSchemaBuilderEditor::AppendLog);
#else
    SheetSchemaBuilderEditor::GLastOutput = TEXT("SheetSchemaBuilderNative.dll/lib was not found. Publish Source.Native/Sheet-Schema-Builder.Native.csproj before building the Unreal plugin.");
    return 1;
#endif
}

FString USheetSchemaBuilderEditorLibrary::GetLastOutput()
{
    return SheetSchemaBuilderEditor::GLastOutput;
}
