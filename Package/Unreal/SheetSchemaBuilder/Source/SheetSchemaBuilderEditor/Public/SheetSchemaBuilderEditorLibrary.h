#pragma once

#include "Kismet/BlueprintFunctionLibrary.h"
#include "SheetSchemaBuilderEditorLibrary.generated.h"

USTRUCT(BlueprintType)
struct SHEETSCHEMABUILDEREDITOR_API FSheetSchemaBuilderRunResult
{
    GENERATED_BODY()

    UPROPERTY(BlueprintReadOnly, Category = "Sheet Schema Builder")
    int32 ExitCode = 1;

    UPROPERTY(BlueprintReadOnly, Category = "Sheet Schema Builder")
    FString Output;
};

UCLASS()
class SHEETSCHEMABUILDEREDITOR_API USheetSchemaBuilderEditorLibrary : public UBlueprintFunctionLibrary
{
    GENERATED_BODY()

public:
    UFUNCTION(BlueprintCallable, Category = "Sheet Schema Builder")
    static FSheetSchemaBuilderRunResult RunSheetSchemaBuilderWithResult(const FString& IniPath, bool bForce);

    UFUNCTION(BlueprintCallable, Category = "Sheet Schema Builder")
    static int32 RunSheetSchemaBuilder(const FString& IniPath, bool bForce);

    UFUNCTION(BlueprintPure, Category = "Sheet Schema Builder")
    static FString GetLastOutput();
};
