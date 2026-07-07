import configparser
import os
import subprocess
import sys
import threading
from pathlib import Path

try:
    import unreal
except ImportError:
    unreal = None


PLUGIN_ROOT = Path(__file__).resolve().parents[1]
INI_PATH = PLUGIN_ROOT / "Sheet-Schema-Builder.ini"
DLL_PATH = PLUGIN_ROOT / "Sheet-Schema-Builder.dll"


DEFAULTS = {
    "GoogleSheet": {
        "AuthMode": "ServiceAccount",
        "SpreadsheetId": "",
        "ServiceAccountJsonPath": "./credentials/service-account.json",
        "ApiKey": "",
        "LocalDirectory": "",
        "Sheets": "",
    },
    "CodeGen": {
        "Target": "Unreal",
        "Namespace": "BS.Data",
        "DatabaseClassName": "SheetDataBase",
        "DatabaseOutputDirectory": "./Generated/Database",
        "StructOutputDirectory": "./Generated/Database/Structs",
    },
    "Json": {
        "OutputPath": "./Generated/SheetDataBase.json",
    },
}


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

    entry = unreal.ToolMenuEntry(
        name="SheetSchemaBuilder.OpenSettings",
        type=unreal.MultiBlockType.MENU_ENTRY,
    )
    entry.set_label("Sheet Schema Builder")
    entry.set_tool_tip("Edit Sheet-Schema-Builder.ini and run the builder DLL.")
    entry.set_string_command(unreal.ToolMenuStringCommandType.PYTHON, "", command)
    menu.add_menu_entry("SheetSchemaBuilder", entry)
    menus.refresh_all_widgets()


def open_window():
    try:
        import tkinter as tk
        from tkinter import filedialog, messagebox, ttk
    except Exception as exception:
        show_unreal_message("Sheet Schema Builder", f"Tkinter is not available in this Unreal Python environment.\n\n{exception}")
        return

    SheetSchemaBuilderWindow(tk, ttk, filedialog, messagebox).show()


def show_unreal_message(title, message):
    if unreal is not None:
        unreal.EditorDialog.show_message(title, message, unreal.AppMsgType.OK)
    else:
        print(f"{title}: {message}")


def read_ini(path):
    config = configparser.ConfigParser()
    config.optionxform = str
    config.read_dict(DEFAULTS)

    if path.exists():
        config.read(path, encoding="utf-8-sig")

    config["CodeGen"]["Target"] = "Unreal"
    return config


def write_ini(path, config):
    path.parent.mkdir(parents=True, exist_ok=True)
    config["CodeGen"]["Target"] = "Unreal"

    lines = [
        "[GoogleSheet]",
        "# ServiceAccount | ApiKey | Local",
        f"AuthMode = {config['GoogleSheet'].get('AuthMode', 'ServiceAccount')}",
        f"SpreadsheetId = {config['GoogleSheet'].get('SpreadsheetId', '')}",
        f"ServiceAccountJsonPath = {config['GoogleSheet'].get('ServiceAccountJsonPath', './credentials/service-account.json')}",
        f"ApiKey = {config['GoogleSheet'].get('ApiKey', '')}",
        f"LocalDirectory = {config['GoogleSheet'].get('LocalDirectory', '')}",
        f"Sheets = {config['GoogleSheet'].get('Sheets', '')}",
        "",
        "[CodeGen]",
        "# Unity | Unreal",
        "Target = Unreal",
        f"Namespace = {config['CodeGen'].get('Namespace', 'BS.Data')}",
        f"DatabaseClassName = {config['CodeGen'].get('DatabaseClassName', 'SheetDataBase')}",
        f"DatabaseOutputDirectory = {config['CodeGen'].get('DatabaseOutputDirectory', './Generated/Database')}",
        f"StructOutputDirectory = {config['CodeGen'].get('StructOutputDirectory', './Generated/Database/Structs')}",
        "",
        "[Json]",
        f"OutputPath = {config['Json'].get('OutputPath', './Generated/SheetDataBase.json')}",
        "",
    ]

    path.write_text(os.linesep.join(lines), encoding="utf-8")


class SheetSchemaBuilderWindow:
    def __init__(self, tk, ttk, filedialog, messagebox):
        self.tk = tk
        self.ttk = ttk
        self.filedialog = filedialog
        self.messagebox = messagebox
        self.ini_path = INI_PATH
        self.dll_path = DLL_PATH
        self.config = read_ini(self.ini_path)
        self.vars = {}
        self.root = None
        self.output = None

    def show(self):
        if self.root is not None:
            self.root.lift()
            return

        self.root = self.tk.Tk()
        self.root.title("Sheet Schema Builder")
        self.root.geometry("720x680")
        self.root.protocol("WM_DELETE_WINDOW", self.root.destroy)
        self._build_ui()
        self.root.mainloop()

    def _build_ui(self):
        container = self.ttk.Frame(self.root, padding=12)
        container.pack(fill="both", expand=True)

        self._path_row(container)
        self._section(container, "Google Sheet")
        self._combo_row(container, "GoogleSheet", "AuthMode", ["ServiceAccount", "ApiKey", "Local"])
        self._entry_row(container, "GoogleSheet", "SpreadsheetId", "Spreadsheet ID")
        self._entry_row(container, "GoogleSheet", "ServiceAccountJsonPath", "Service Account JSON", browse="file")
        self._entry_row(container, "GoogleSheet", "ApiKey", "API Key")
        self._entry_row(container, "GoogleSheet", "LocalDirectory", "Local TSV Directory", browse="directory")
        self._entry_row(container, "GoogleSheet", "Sheets", "Sheets")

        self._section(container, "Code Generation")
        self._readonly_row(container, "Target", "Unreal")
        self._entry_row(container, "CodeGen", "Namespace", "Namespace")
        self._entry_row(container, "CodeGen", "DatabaseClassName", "Database Class Name")
        self._entry_row(container, "CodeGen", "DatabaseOutputDirectory", "Database Output Directory", browse="directory")
        self._entry_row(container, "CodeGen", "StructOutputDirectory", "Struct Output Directory", browse="directory")

        self._section(container, "Json")
        self._entry_row(container, "Json", "OutputPath", "Output Path", browse="file")

        button_frame = self.ttk.Frame(container)
        button_frame.pack(fill="x", pady=(12, 8))
        self.ttk.Button(button_frame, text="Reload", command=self.reload).pack(side="left")
        self.ttk.Button(button_frame, text="Save INI", command=self.save).pack(side="left", padx=6)
        self.ttk.Button(button_frame, text="Run", command=lambda: self.run_builder(False)).pack(side="right")
        self.ttk.Button(button_frame, text="Run Force", command=lambda: self.run_builder(True)).pack(side="right", padx=6)

        self.output = self.tk.Text(container, height=8)
        self.output.pack(fill="both", expand=True)

    def _path_row(self, parent):
        frame = self.ttk.Frame(parent)
        frame.pack(fill="x", pady=(0, 10))
        self.ttk.Label(frame, text="INI File", width=22).pack(side="left")
        self.ini_var = self.tk.StringVar(value=str(self.ini_path))
        self.ttk.Entry(frame, textvariable=self.ini_var).pack(side="left", fill="x", expand=True)
        self.ttk.Button(frame, text="...", width=3, command=self.browse_ini).pack(side="left", padx=(6, 0))

    def _section(self, parent, title):
        self.ttk.Label(parent, text=title, font=("TkDefaultFont", 10, "bold")).pack(anchor="w", pady=(10, 4))

    def _entry_row(self, parent, section, key, label, browse=None):
        frame = self.ttk.Frame(parent)
        frame.pack(fill="x", pady=2)
        self.ttk.Label(frame, text=label, width=22).pack(side="left")
        var = self.tk.StringVar(value=self.config[section].get(key, DEFAULTS[section].get(key, "")))
        self.vars[(section, key)] = var
        self.ttk.Entry(frame, textvariable=var).pack(side="left", fill="x", expand=True)

        if browse is not None:
            self.ttk.Button(frame, text="...", width=3, command=lambda: self.browse_path(var, browse)).pack(side="left", padx=(6, 0))

    def _combo_row(self, parent, section, key, options):
        frame = self.ttk.Frame(parent)
        frame.pack(fill="x", pady=2)
        self.ttk.Label(frame, text="Auth Mode", width=22).pack(side="left")
        var = self.tk.StringVar(value=self.config[section].get(key, DEFAULTS[section].get(key, options[0])))
        self.vars[(section, key)] = var
        self.ttk.Combobox(frame, textvariable=var, values=options, state="readonly").pack(side="left", fill="x", expand=True)

    def _readonly_row(self, parent, label, value):
        frame = self.ttk.Frame(parent)
        frame.pack(fill="x", pady=2)
        self.ttk.Label(frame, text=label, width=22).pack(side="left")
        readonly_var = self.tk.StringVar(value=value)
        self.ttk.Entry(frame, textvariable=readonly_var, state="readonly").pack(side="left", fill="x", expand=True)

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
            var.set(self._to_ini_relative_path(Path(selected)))

    def reload(self):
        self.ini_path = Path(self.ini_var.get())
        self.config = read_ini(self.ini_path)

        for (section, key), var in self.vars.items():
            var.set(self.config[section].get(key, DEFAULTS[section].get(key, "")))

    def save(self):
        self._collect()
        write_ini(self.ini_path, self.config)
        self.messagebox.showinfo("Sheet Schema Builder", "Sheet-Schema-Builder.ini saved.")

    def run_builder(self, force):
        self._collect()
        write_ini(self.ini_path, self.config)

        if not self.dll_path.exists():
            self.messagebox.showerror("Sheet Schema Builder", f"Sheet-Schema-Builder.dll was not found:\n{self.dll_path}")
            return

        self._append_output("Running Sheet Schema Builder...\n")
        thread = threading.Thread(target=self._run_builder_thread, args=(force,), daemon=True)
        thread.start()

    def _run_builder_thread(self, force):
        command = ["dotnet", str(self.dll_path), str(self.ini_path)]
        if force:
            command.append("--force")

        try:
            completed = subprocess.run(command, cwd=str(self.ini_path.parent), capture_output=True, text=True, encoding="utf-8")
            output = completed.stdout + completed.stderr
            self.root.after(0, lambda: self._builder_finished(completed.returncode, output))
        except Exception as exception:
            self.root.after(0, lambda: self._builder_finished(1, str(exception)))

    def _builder_finished(self, exit_code, output):
        self._append_output(output + "\n")
        if exit_code == 0:
            self.messagebox.showinfo("Sheet Schema Builder", "Completed.")
        else:
            self.messagebox.showerror("Sheet Schema Builder", f"Failed. ExitCode: {exit_code}")

    def _append_output(self, text):
        if self.output is not None:
            self.output.insert("end", text)
            self.output.see("end")

    def _collect(self):
        self.ini_path = Path(self.ini_var.get())
        self.config = read_ini(self.ini_path)

        for (section, key), var in self.vars.items():
            self.config[section][key] = var.get()

        self.config["CodeGen"]["Target"] = "Unreal"

    def _to_ini_relative_path(self, selected_path):
        try:
            relative = selected_path.relative_to(self.ini_path.parent)
            return "./" + relative.as_posix()
        except ValueError:
            return selected_path.as_posix()


if __name__ == "__main__":
    open_window()
