#pragma once

#include "Kismet/BlueprintFunctionLibrary.h"
#include "SheetSchemaBuilderEditorLibrary.generated.h"

UCLASS()
class SHEETSCHEMABUILDEREDITOR_API USheetSchemaBuilderEditorLibrary : public UBlueprintFunctionLibrary
{
    GENERATED_BODY()

public:
    UFUNCTION(BlueprintCallable, Category = "Sheet Schema Builder")
    static int32 RunSheetSchemaBuilder(const FString& IniPath, bool bForce);

    UFUNCTION(BlueprintPure, Category = "Sheet Schema Builder")
    static FString GetLastOutput();
};
