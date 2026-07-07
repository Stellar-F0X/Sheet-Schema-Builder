#pragma once

#include <stdint.h>

#ifndef SHEET_SCHEMA_BUILDER_NATIVE_API
#define SHEET_SCHEMA_BUILDER_NATIVE_API __declspec(dllimport)
#endif

extern "C"
{
    typedef void(__cdecl* FSheetSchemaBuilderLogCallback)(const wchar_t* Message);
    SHEET_SCHEMA_BUILDER_NATIVE_API int32_t __cdecl SheetSchemaBuilder_Process(const wchar_t* IniPath, int32_t Force, FSheetSchemaBuilderLogCallback LogCallback);
}
