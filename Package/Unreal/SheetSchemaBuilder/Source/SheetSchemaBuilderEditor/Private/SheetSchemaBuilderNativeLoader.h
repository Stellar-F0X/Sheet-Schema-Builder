#pragma once

#include "CoreMinimal.h"
#include "SheetSchemaBuilderNative.h"

bool LoadSheetSchemaBuilderNative(FString& ErrorMessage);
FSheetSchemaBuilderProcess GetSheetSchemaBuilderNativeProcess();
void UnloadSheetSchemaBuilderNative();
