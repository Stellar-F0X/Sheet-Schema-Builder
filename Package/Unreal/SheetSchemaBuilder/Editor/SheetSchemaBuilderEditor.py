import os
import sys
from dataclasses import dataclass, field
from pathlib import Path

try:
    import unreal
except ImportError:
    unreal = None


PLUGIN_ROOT = Path(__file__).resolve().parents[1]
INI_PATH = PLUGIN_ROOT / "Sheet-Schema-Builder.ini"
TARGET_NAME = "Unreal"

_ACTIVE_WINDOW = None

_DARK_THEME = {
    "background": "#202020",
    "surface": "#2b2b2b",
    "input": "#303030",
    "foreground": "#f0f0f0",
    "muted_foreground": "#bdbdbd",
    "border": "#4a4a4a",
    "accent": "#3d7eff",
    "accent_active": "#5a92ff",
    "selection": "#264f78",
}


@dataclass
class GoogleSheetSettings:
    auth_mode: str = "ServiceAccount"
    spreadsheet_id: str = ""
    service_account_json_path: str = "./credentials/service-account.json"
    api_key: str = ""
    local_directory: str = ""
    sheets: str = ""


@dataclass
class CodeGenSettings:
    target: str = TARGET_NAME
    namespace: str = "BS.Data"
    database_class_name: str = "SheetDataBase"
    database_output_directory: str = "./Generated/Database"
    struct_output_directory: str = "./Generated/Database/Structs"


@dataclass
class JsonSettings:
    output_path: str = "./Generated/SheetDataBase.json"


@dataclass
class BuilderIniSettings:
    google_sheet: GoogleSheetSettings = field(default_factory=GoogleSheetSettings)
    code_gen: CodeGenSettings = field(default_factory=CodeGenSettings)
    json: JsonSettings = field(default_factory=JsonSettings)

    @classmethod
    def load(cls, path):
        settings = cls()
        if not path.exists():
            return settings

        current_section = ""
        for raw_line in path.read_text(encoding="utf-8-sig").splitlines():
            line = raw_line.strip()
            if not line or line.startswith("#") or line.startswith(";"):
                continue

            if line.startswith("[") and line.endswith("]"):
                current_section = line[1:-1].strip()
                continue

            key, separator, value = line.partition("=")
            if separator:
                settings.apply_value(current_section, key.strip(), value.strip())

        settings.code_gen.target = TARGET_NAME
        return settings

    def save(self, path):
        self.code_gen.target = TARGET_NAME
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(self.to_ini_text(), encoding="utf-8")

    def apply_value(self, section, key, value):
        if section == "GoogleSheet":
            if key == "AuthMode":
                self.google_sheet.auth_mode = value
            elif key == "SpreadsheetId":
                self.google_sheet.spreadsheet_id = value
            elif key == "ServiceAccountJsonPath":
                self.google_sheet.service_account_json_path = value
            elif key == "ApiKey":
                self.google_sheet.api_key = value
            elif key == "LocalDirectory":
                self.google_sheet.local_directory = value
            elif key == "Sheets":
                self.google_sheet.sheets = value
        elif section == "CodeGen":
            if key == "Namespace":
                self.code_gen.namespace = value
            elif key == "DatabaseClassName":
                self.code_gen.database_class_name = value
            elif key == "DatabaseOutputDirectory":
                self.code_gen.database_output_directory = value
            elif key == "StructOutputDirectory":
                self.code_gen.struct_output_directory = value
        elif section == "Json" and key == "OutputPath":
            self.json.output_path = value

    def to_ini_text(self):
        return os.linesep.join([
            "[GoogleSheet]",
            "# ServiceAccount | ApiKey | Local",
            f"AuthMode = {self.google_sheet.auth_mode}",
            f"SpreadsheetId = {self.google_sheet.spreadsheet_id}",
            f"ServiceAccountJsonPath = {self.google_sheet.service_account_json_path}",
            f"ApiKey = {self.google_sheet.api_key}",
            f"LocalDirectory = {self.google_sheet.local_directory}",
            f"Sheets = {self.google_sheet.sheets}",
            "",
            "[CodeGen]",
            "# Unity | Unreal",
            f"Target = {self.code_gen.target}",
            f"Namespace = {self.code_gen.namespace}",
            f"DatabaseClassName = {self.code_gen.database_class_name}",
            f"DatabaseOutputDirectory = {self.code_gen.database_output_directory}",
            f"StructOutputDirectory = {self.code_gen.struct_output_directory}",
            "",
            "[Json]",
            f"OutputPath = {self.json.output_path}",
            "",
        ])


@dataclass
class BuilderRunResult:
    returncode: int
    stdout: str = ""
    stderr: str = ""


class BuilderRunner:
    def run(self, ini_path, force):
        if unreal is None or not hasattr(unreal, "SheetSchemaBuilderEditorLibrary"):
            return BuilderRunResult(1, "", "SheetSchemaBuilderEditor C++ module is not available.")

        library = unreal.SheetSchemaBuilderEditorLibrary
        run = getattr(library, "run_sheet_schema_builder", None)
        get_last_output = getattr(library, "get_last_output", None)
        if run is None or get_last_output is None:
            return BuilderRunResult(1, "", "SheetSchemaBuilderEditor C++ API is not available.")

        exit_code = run(str(ini_path), bool(force))
        return BuilderRunResult(exit_code, get_last_output(), "")


def register_menu():
    if unreal is None:
        return

    command = (
        "import sys; "
        f"sys.path.insert(0, r'{PLUGIN_ROOT / 'Editor'}'); "
        "import SheetSchemaBuilderEditor; "
        "SheetSchemaBuilderEditor.open_window()"
    )

    menus = unreal.ToolMenus.get()
    menu = menus.extend_menu("LevelEditor.MainMenu.Tools")

    try:
        menu.add_section("SheetSchemaBuilder", "Sheet Schema Builder")
    except Exception:
        pass

    entry = unreal.ToolMenuEntry(name="SheetSchemaBuilder.OpenSettings", type=unreal.MultiBlockType.MENU_ENTRY)
    entry.set_label("Sheet Schema Builder")
    entry.set_tool_tip("Edit Sheet-Schema-Builder.ini and run the native builder.")
    entry.set_string_command(unreal.ToolMenuStringCommandType.PYTHON, "", command)
    menu.add_menu_entry("SheetSchemaBuilder", entry)
    menus.refresh_all_widgets()


def open_window():
    global _ACTIVE_WINDOW

    if _ACTIVE_WINDOW is not None and _ACTIVE_WINDOW.is_open():
        _ACTIVE_WINDOW.focus()
        return

    try:
        import tkinter as tk
        from tkinter import filedialog, messagebox, ttk
    except Exception as exception:
        if unreal is not None:
            unreal.EditorDialog.show_message("Sheet Schema Builder", f"Tkinter is not available in this Unreal Python environment.\n\n{exception}", unreal.AppMsgType.OK)
        else:
            print(f"Sheet Schema Builder: {exception}")
        return

    _ACTIVE_WINDOW = SheetSchemaBuilderWindow(tk, ttk, filedialog, messagebox)
    _ACTIVE_WINDOW.show()


class SheetSchemaBuilderWindow:
    def __init__(self, tk, ttk, filedialog, messagebox):
        self.tk = tk
        self.ttk = ttk
        self.filedialog = filedialog
        self.messagebox = messagebox
        self.ini_path = INI_PATH
        self.settings = BuilderIniSettings.load(self.ini_path)
        self.runner = BuilderRunner()
        self.vars = {}
        self.field_widgets = {}
        self.root = None
        self.output = None
        self.style = None
        self.slate_tick_handle = None

    def show(self):
        if self.is_open():
            self.focus()
            return

        self.root = self.tk.Tk()
        self.root.title("Sheet Schema Builder")
        self.root.geometry("720x680")
        self.apply_dark_theme()
        self.root.protocol("WM_DELETE_WINDOW", self.close)
        self.build_ui()

        if unreal is not None and hasattr(unreal, "register_slate_post_tick_callback"):
            self.slate_tick_handle = unreal.register_slate_post_tick_callback(self.process_tk_events)
        else:
            self.root.mainloop()

    def is_open(self):
        if self.root is None:
            return False

        try:
            return bool(self.root.winfo_exists())
        except self.tk.TclError:
            return False

    def focus(self):
        if not self.is_open():
            return

        self.root.deiconify()
        self.root.lift()
        self.root.focus_force()

    def apply_dark_theme(self):
        self.root.configure(background=_DARK_THEME["background"])
        self.root.option_add("*TCombobox*Listbox.background", _DARK_THEME["input"])
        self.root.option_add("*TCombobox*Listbox.foreground", _DARK_THEME["foreground"])
        self.root.option_add("*TCombobox*Listbox.selectBackground", _DARK_THEME["selection"])
        self.root.option_add("*TCombobox*Listbox.selectForeground", _DARK_THEME["foreground"])

        self.style = self.ttk.Style(self.root)
        try:
            self.style.theme_use("clam")
        except self.tk.TclError:
            pass

        self.style.configure(".", background=_DARK_THEME["surface"], foreground=_DARK_THEME["foreground"])
        self.style.configure("TFrame", background=_DARK_THEME["background"])
        self.style.configure("TLabel", background=_DARK_THEME["background"], foreground=_DARK_THEME["foreground"])
        self.style.configure(
            "TButton",
            background=_DARK_THEME["surface"],
            foreground=_DARK_THEME["foreground"],
            bordercolor=_DARK_THEME["border"],
            lightcolor=_DARK_THEME["border"],
            darkcolor=_DARK_THEME["border"],
            padding=(8, 4),
        )
        self.style.map(
            "TButton",
            background=[("active", _DARK_THEME["accent"]), ("pressed", _DARK_THEME["accent_active"])],
            foreground=[("disabled", _DARK_THEME["muted_foreground"])],
        )
        self.style.configure(
            "TEntry",
            fieldbackground=_DARK_THEME["input"],
            foreground=_DARK_THEME["foreground"],
            bordercolor=_DARK_THEME["border"],
            insertcolor=_DARK_THEME["foreground"],
        )
        self.style.configure(
            "TCombobox",
            fieldbackground=_DARK_THEME["input"],
            background=_DARK_THEME["input"],
            foreground=_DARK_THEME["foreground"],
            arrowcolor=_DARK_THEME["foreground"],
            bordercolor=_DARK_THEME["border"],
        )
        self.style.map(
            "TCombobox",
            fieldbackground=[("readonly", _DARK_THEME["input"])],
            foreground=[("readonly", _DARK_THEME["foreground"])],
        )

    def process_tk_events(self, *_args):
        if not self.is_open():
            self.close()
            return

        try:
            self.root.update()
        except self.tk.TclError:
            self.close()

    def close(self):
        global _ACTIVE_WINDOW

        if self.slate_tick_handle is not None and unreal is not None:
            unregister = getattr(unreal, "unregister_slate_post_tick_callback", None)
            if unregister is not None:
                try:
                    unregister(self.slate_tick_handle)
                except Exception:
                    pass
            self.slate_tick_handle = None

        if self.root is not None:
            try:
                self.root.destroy()
            except self.tk.TclError:
                pass
            self.root = None

        if _ACTIVE_WINDOW is self:
            _ACTIVE_WINDOW = None

    def build_ui(self):
        container = self.ttk.Frame(self.root, padding=12)
        container.pack(fill="both", expand=True)

        path_frame = self.ttk.Frame(container)
        path_frame.pack(fill="x", pady=(0, 10))
        self.ttk.Label(path_frame, text="INI File", width=22).pack(side="left")
        self.ini_var = self.tk.StringVar(value=str(self.ini_path))
        self.ttk.Entry(path_frame, textvariable=self.ini_var).pack(side="left", fill="x", expand=True)
        self.ttk.Button(path_frame, text="...", width=3, command=self.browse_ini).pack(side="left", padx=(6, 0))

        self.add_section(container, "Google Sheet")

        auth_frame = self.ttk.Frame(container)
        auth_frame.pack(fill="x", pady=2)
        self.ttk.Label(auth_frame, text="Auth Mode", width=22).pack(side="left")
        auth_var = self.tk.StringVar(value=self.settings.google_sheet.auth_mode)
        self.vars[("google_sheet", "auth_mode")] = auth_var
        auth_combo = self.ttk.Combobox(auth_frame, textvariable=auth_var, values=["ServiceAccount", "ApiKey", "Local"], state="readonly")
        auth_combo.pack(side="left", fill="x", expand=True)
        auth_combo.bind("<<ComboboxSelected>>", self.update_auth_fields)

        self.add_entry_row(container, "google_sheet", "spreadsheet_id", "Spreadsheet ID")
        self.add_entry_row(container, "google_sheet", "service_account_json_path", "Service Account JSON", browse="file")
        self.add_entry_row(container, "google_sheet", "api_key", "API Key")
        self.add_entry_row(container, "google_sheet", "local_directory", "Local TSV Directory", browse="directory")
        self.add_entry_row(container, "google_sheet", "sheets", "Sheets")
        self.update_auth_fields()

        self.add_section(container, "Code Generation")
        self.add_entry_row(container, "code_gen", "namespace", "Namespace")
        self.add_entry_row(container, "code_gen", "database_class_name", "Database Class Name")
        self.add_entry_row(container, "code_gen", "database_output_directory", "Database Output Directory", browse="directory")
        self.add_entry_row(container, "code_gen", "struct_output_directory", "Struct Output Directory", browse="directory")

        self.add_section(container, "Json")
        self.add_entry_row(container, "json", "output_path", "Output Path", browse="file")

        button_frame = self.ttk.Frame(container)
        button_frame.pack(fill="x", pady=(12, 8))
        self.ttk.Button(button_frame, text="Reload", command=self.reload).pack(side="left")
        self.ttk.Button(button_frame, text="Save INI", command=self.save).pack(side="left", padx=6)
        self.ttk.Button(button_frame, text="Run", command=lambda: self.run_builder(False)).pack(side="right")
        self.ttk.Button(button_frame, text="Run Force", command=lambda: self.run_builder(True)).pack(side="right", padx=6)

        self.output = self.tk.Text(
            container,
            height=8,
            background=_DARK_THEME["input"],
            foreground=_DARK_THEME["foreground"],
            insertbackground=_DARK_THEME["foreground"],
            selectbackground=_DARK_THEME["selection"],
            selectforeground=_DARK_THEME["foreground"],
            relief="flat",
            borderwidth=1,
        )
        self.output.pack(fill="both", expand=True)

    def add_section(self, parent, title):
        self.ttk.Label(parent, text=title, font=("TkDefaultFont", 10, "bold")).pack(anchor="w", pady=(10, 4))

    def add_entry_row(self, parent, section_name, field_name, label, browse=None):
        frame = self.ttk.Frame(parent)
        frame.pack(fill="x", pady=2)
        self.ttk.Label(frame, text=label, width=22).pack(side="left")
        var = self.tk.StringVar(value=getattr(getattr(self.settings, section_name), field_name))
        self.vars[(section_name, field_name)] = var
        entry = self.ttk.Entry(frame, textvariable=var)
        entry.pack(side="left", fill="x", expand=True)
        widgets = [entry]

        if browse is not None:
            browse_button = self.ttk.Button(frame, text="...", width=3, command=lambda: self.browse_path(var, browse))
            browse_button.pack(side="left", padx=(6, 0))
            widgets.append(browse_button)

        self.field_widgets[(section_name, field_name)] = widgets

    def update_auth_fields(self, *_args):
        auth_mode = self.vars[("google_sheet", "auth_mode")].get()
        enabled_fields = {
            "spreadsheet_id": auth_mode != "Local",
            "service_account_json_path": auth_mode == "ServiceAccount",
            "api_key": auth_mode == "ApiKey",
            "local_directory": auth_mode == "Local",
        }

        for field_name, enabled in enabled_fields.items():
            state = "normal" if enabled else "disabled"
            for widget in self.field_widgets.get(("google_sheet", field_name), []):
                widget.configure(state=state)

    def browse_ini(self):
        selected = self.filedialog.askopenfilename(initialdir=str(self.ini_path.parent), filetypes=[("INI", "*.ini"), ("All", "*.*")])
        if selected:
            self.ini_path = Path(selected)
            self.ini_var.set(selected)
            self.reload()

    def browse_path(self, var, mode):
        initial_dir = str(self.ini_path.parent)
        selected = self.filedialog.askdirectory(initialdir=initial_dir) if mode == "directory" else self.filedialog.askopenfilename(initialdir=initial_dir)
        if selected:
            try:
                var.set("./" + Path(selected).relative_to(self.ini_path.parent).as_posix())
            except ValueError:
                var.set(Path(selected).as_posix())

    def reload(self):
        self.ini_path = Path(self.ini_var.get())
        self.settings = BuilderIniSettings.load(self.ini_path)

        for (section_name, field_name), var in self.vars.items():
            var.set(getattr(getattr(self.settings, section_name), field_name))

        self.update_auth_fields()

    def save(self):
        self.ini_path = Path(self.ini_var.get())

        for (section_name, field_name), var in self.vars.items():
            setattr(getattr(self.settings, section_name), field_name, var.get())

        self.settings.save(self.ini_path)
        self.messagebox.showinfo("Sheet Schema Builder", "Sheet-Schema-Builder.ini saved.")

    def run_builder(self, force):
        self.ini_path = Path(self.ini_var.get())

        if not self.ini_path.exists():
            self.messagebox.showerror("Sheet Schema Builder", f"INI file does not exist. Press Save INI before running.\n{self.ini_path}")
            return

        self.output.insert("end", "Running Sheet Schema Builder...\n")
        self.output.see("end")
        self.root.update_idletasks()

        try:
            completed = self.runner.run(self.ini_path, force)
            self.finish_run(completed.returncode, completed.stdout + completed.stderr)
        except Exception as exception:
            self.finish_run(1, str(exception))

    def finish_run(self, exit_code, output):
        self.output.insert("end", output + "\n")
        self.output.see("end")

        if exit_code == 0:
            self.messagebox.showinfo("Sheet Schema Builder", "Completed.")
        else:
            self.messagebox.showerror("Sheet Schema Builder", f"Failed. ExitCode: {exit_code}")


if __name__ == "__main__":
    open_window()
