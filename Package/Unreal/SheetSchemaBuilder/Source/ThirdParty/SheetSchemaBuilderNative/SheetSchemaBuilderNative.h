#pragma once

#include <stdint.h>

#if defined(_WIN32)
#define SHEET_SCHEMA_BUILDER_CALL __cdecl
#else
#define SHEET_SCHEMA_BUILDER_CALL
#endif

typedef void(SHEET_SCHEMA_BUILDER_CALL* FSheetSchemaBuilderLogCallback)(const char* Message);
typedef int32_t(SHEET_SCHEMA_BUILDER_CALL* FSheetSchemaBuilderProcess)(const char* IniPath, int32_t Force, FSheetSchemaBuilderLogCallback LogCallback);
