import sys
from pathlib import Path

plugin_root = Path(__file__).resolve().parents[2]
editor_dir = plugin_root / "Editor"

if str(editor_dir) not in sys.path:
    sys.path.insert(0, str(editor_dir))

import SheetSchemaBuilderEditor

SheetSchemaBuilderEditor.register_menu()
