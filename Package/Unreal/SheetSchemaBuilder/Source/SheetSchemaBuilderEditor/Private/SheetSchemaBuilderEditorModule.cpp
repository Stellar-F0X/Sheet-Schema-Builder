#include "SheetSchemaBuilderNativeLoader.h"
#include "Modules/ModuleManager.h"

class FSheetSchemaBuilderEditorModule : public IModuleInterface
{
public:
    virtual void ShutdownModule() override
    {
        UnloadSheetSchemaBuilderNative();
    }
};

IMPLEMENT_MODULE(FSheetSchemaBuilderEditorModule, SheetSchemaBuilderEditor)
