using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using Debug = UnityEngine.Debug;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SheetSchemaBuilder.UnityEditorTools
{
    public sealed class SheetSchemaBuilderSettingsWindow : EditorWindow
    {
        private const string PackageName = "com.sheet-schema-builder.tool";
        private const string IniFileName = "Sheet-Schema-Builder.ini";
        private const string TargetName = "Unity";

        private Vector2 _scroll;
        private string _iniPath = string.Empty;
        private BuilderIniSettings _settings;
        private bool _isRunning;

        [MenuItem("Tools/Sheet Schema Builder/Settings")]
        public static void Open()
        {
            SheetSchemaBuilderSettingsWindow window = GetWindow<SheetSchemaBuilderSettingsWindow>("Sheet Schema Builder");
            window.minSize = new Vector2(520, 520);
            window.Show();
        }

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(_iniPath))
            {
                _iniPath = Path.Combine(FindPackageRoot(), IniFileName);
            }

            _settings = BuilderIniSettings.Load(_iniPath, TargetName);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("INI File", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(_iniPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

                if (GUILayout.Button("Browse", GUILayout.Width(76)))
                {
                    string selectedPath = EditorUtility.OpenFilePanel("Select Sheet-Schema-Builder.ini", GetIniBaseDirectory(), "ini");
                    if (string.IsNullOrWhiteSpace(selectedPath) == false)
                    {
                        _iniPath = selectedPath;
                        _settings = BuilderIniSettings.Load(_iniPath, TargetName);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload"))
                {
                    _settings = BuilderIniSettings.Load(_iniPath, TargetName);
                }

                if (GUILayout.Button("Reveal"))
                {
                    RevealIniPath();
                }
            }

            using (EditorGUILayout.ScrollViewScope scrollView = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scrollView.scrollPosition;

                EditorGUILayout.Space(12);
                EditorGUILayout.LabelField("Google Sheet", EditorStyles.boldLabel);
                _settings.GoogleSheet.AuthMode = (EAuthMode)EditorGUILayout.EnumPopup("Auth Mode", _settings.GoogleSheet.AuthMode);

                using (new EditorGUI.DisabledScope(_settings.GoogleSheet.AuthMode == EAuthMode.Local))
                {
                    _settings.GoogleSheet.SpreadsheetId = EditorGUILayout.TextField("Spreadsheet ID", _settings.GoogleSheet.SpreadsheetId);
                }

                using (new EditorGUI.DisabledScope(_settings.GoogleSheet.AuthMode != EAuthMode.ServiceAccount))
                {
                    DrawPathField("Service Account JSON", ref _settings.GoogleSheet.ServiceAccountJsonPath, false, "json");
                }

                using (new EditorGUI.DisabledScope(_settings.GoogleSheet.AuthMode != EAuthMode.ApiKey))
                {
                    _settings.GoogleSheet.ApiKey = EditorGUILayout.TextField("API Key", _settings.GoogleSheet.ApiKey);
                }

                using (new EditorGUI.DisabledScope(_settings.GoogleSheet.AuthMode != EAuthMode.Local))
                {
                    DrawPathField("Local TSV Directory", ref _settings.GoogleSheet.LocalDirectory, true, string.Empty);
                }

                _settings.GoogleSheet.Sheets = EditorGUILayout.TextField("Sheets", _settings.GoogleSheet.Sheets);
                EditorGUILayout.HelpBox("Sheets is a comma-separated list. Leave it empty to fetch every sheet.", MessageType.None);

                EditorGUILayout.Space(12);
                EditorGUILayout.LabelField("Code Generation", EditorStyles.boldLabel);
                _settings.CodeGen.Namespace = EditorGUILayout.TextField("Namespace", _settings.CodeGen.Namespace);
                _settings.CodeGen.DatabaseClassName = EditorGUILayout.TextField("Database Class Name", _settings.CodeGen.DatabaseClassName);
                DrawPathField("Database Output Directory", ref _settings.CodeGen.DatabaseOutputDirectory, true, string.Empty);
                DrawPathField("Struct Output Directory", ref _settings.CodeGen.StructOutputDirectory, true, string.Empty);

                EditorGUILayout.Space(12);
                EditorGUILayout.LabelField("Json", EditorStyles.boldLabel);
                DrawPathField("Output Path", ref _settings.Json.OutputPath, false, "json");
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Save INI", GUILayout.Width(120), GUILayout.Height(28)))
                {
                    _settings.Save(_iniPath, TargetName);
                    AssetDatabase.Refresh();
                    ShowNotification(new GUIContent("Saved Sheet-Schema-Builder.ini"));
                }

                using (new EditorGUI.DisabledScope(_isRunning))
                {
                    if (GUILayout.Button("Run", GUILayout.Width(96), GUILayout.Height(28)))
                    {
                        RunBuilder(false);
                    }

                    if (GUILayout.Button("Run Force", GUILayout.Width(96), GUILayout.Height(28)))
                    {
                        RunBuilder(true);
                    }
                }
            }

            EditorGUILayout.Space(8);
        }

        private void DrawPathField(string label, ref string path, bool folder, string extension)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                path = EditorGUILayout.TextField(label, path);

                if (GUILayout.Button("...", GUILayout.Width(32)))
                {
                    string iniDirectory = Path.GetDirectoryName(_iniPath);
                    string baseDirectory = Directory.Exists(iniDirectory) ? iniDirectory : Application.dataPath;
                    string selectedPath = folder ? EditorUtility.OpenFolderPanel(label, baseDirectory, string.Empty) : EditorUtility.OpenFilePanel(label, baseDirectory, extension);

                    if (string.IsNullOrWhiteSpace(selectedPath) == false)
                    {
                        path = ToIniRelativePath(selectedPath);
                    }
                }
            }
        }

        private void RevealIniPath()
        {
            string iniPath = _iniPath;
            EditorApplication.delayCall += () => RevealIniPathDelayed(iniPath);
        }

        private static void RevealIniPathDelayed(string iniPath)
        {
            if (string.IsNullOrWhiteSpace(iniPath))
            {
                EditorUtility.DisplayDialog("Sheet Schema Builder", "INI path is empty.", "OK");
                return;
            }

            try
            {
                string absolutePath = Path.GetFullPath(iniPath);
                if (File.Exists(absolutePath) == false)
                {
                    EditorUtility.DisplayDialog("Sheet Schema Builder", "INI file does not exist. Press Save INI before revealing.\n\n" + absolutePath, "OK");
                    return;
                }

                EditorUtility.RevealInFinder(absolutePath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Sheet Schema Builder", "Failed to reveal INI file.\n\n" + exception.Message, "OK");
            }
        }

        private async void RunBuilder(bool force)
        {
            if (File.Exists(_iniPath) == false)
            {
                EditorUtility.DisplayDialog("Sheet Schema Builder", "INI file does not exist. Press Save INI before running.\n\n" + _iniPath, "OK");
                return;
            }

            _isRunning = true;
            try
            {
                EditorUtility.DisplayProgressBar("Sheet Schema Builder", "Running builder...", 0.5f);
                StringBuilder logBuilder = new StringBuilder();
                TextWriter originalOutput = Console.Out;
                TextWriter originalError = Console.Error;
                int exitCode;

                using (StringWriter output = new StringWriter(logBuilder))
                using (StringWriter error = new StringWriter(logBuilder))
                {
                    Console.SetOut(output);
                    Console.SetError(error);
                    try
                    {
                        string[] args = force ? new[] { _iniPath, "--target", TargetName, "--force" } : new[] { _iniPath, "--target", TargetName };
                        exitCode = await Task.Run(() => DataBuilder.SheetSchemaBuilder.Process(args));
                    }
                    finally
                    {
                        output.Flush();
                        error.Flush();
                        Console.SetOut(originalOutput);
                        Console.SetError(originalError);
                    }
                }

                string log = logBuilder.ToString();
                if (string.IsNullOrWhiteSpace(log) == false)
                {
                    Debug.Log(log);
                }

                if (exitCode == 0)
                {
                    AssetDatabase.Refresh();
                    ShowNotification(new GUIContent("Sheet Schema Builder completed"));
                    EditorUtility.DisplayDialog("Sheet Schema Builder", string.IsNullOrWhiteSpace(log) ? "Completed." : log, "OK");
                }
                else
                {
                    Debug.LogError(log);
                    EditorUtility.DisplayDialog("Sheet Schema Builder Failed", string.IsNullOrWhiteSpace(log) ? "Exit code: " + exitCode : log, "OK");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Sheet Schema Builder Failed", exception.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isRunning = false;
                Repaint();
            }
        }

        private string ToIniRelativePath(string selectedPath)
        {
            string iniDirectory = Path.GetDirectoryName(_iniPath);
            if (string.IsNullOrWhiteSpace(iniDirectory))
            {
                return selectedPath.Replace('\\', '/');
            }

            string relativePath = Path.GetRelativePath(iniDirectory, selectedPath).Replace('\\', '/');
            return relativePath.StartsWith("..", StringComparison.Ordinal) ? selectedPath.Replace('\\', '/') : "./" + relativePath;
        }

        private string GetIniBaseDirectory()
        {
            string iniDirectory = string.IsNullOrWhiteSpace(_iniPath) ? string.Empty : Path.GetDirectoryName(_iniPath);
            return Directory.Exists(iniDirectory) ? iniDirectory : Application.dataPath;
        }

        private static string FindPackageRoot()
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(SheetSchemaBuilderSettingsWindow).Assembly);

            if (packageInfo == null || string.IsNullOrWhiteSpace(packageInfo.resolvedPath))
            {
                string packagePath = Path.Combine(Directory.GetCurrentDirectory(), "Packages", PackageName);
                return Directory.Exists(packagePath) ? packagePath : Path.Combine(Directory.GetCurrentDirectory(), "Package", TargetName);
            }
            else
            {
                return packageInfo.resolvedPath;
            }
        }

        private enum EAuthMode
        {
            ServiceAccount,
            ApiKey,
            Local
        }

        [Serializable]
        private struct BuilderIniSettings
        {
            public GoogleSheetSettings GoogleSheet;
            public CodeGenSettings CodeGen;
            public JsonSettings Json;

            public static BuilderIniSettings Load(string path, string target)
            {
                BuilderIniSettings settings = CreateDefault(target);
                if (File.Exists(path) == false)
                {
                    return settings;
                }

                Dictionary<string, Dictionary<string, string>> ini = IniFile.Read(path);
                settings.GoogleSheet.AuthMode = IniFile.GetEnum(ini, "GoogleSheet", "AuthMode", settings.GoogleSheet.AuthMode);
                settings.GoogleSheet.SpreadsheetId = IniFile.Get(ini, "GoogleSheet", "SpreadsheetId", settings.GoogleSheet.SpreadsheetId);
                settings.GoogleSheet.ServiceAccountJsonPath = IniFile.Get(ini, "GoogleSheet", "ServiceAccountJsonPath", settings.GoogleSheet.ServiceAccountJsonPath);
                settings.GoogleSheet.ApiKey = IniFile.Get(ini, "GoogleSheet", "ApiKey", settings.GoogleSheet.ApiKey);
                settings.GoogleSheet.LocalDirectory = IniFile.Get(ini, "GoogleSheet", "LocalDirectory", settings.GoogleSheet.LocalDirectory);
                settings.GoogleSheet.Sheets = IniFile.Get(ini, "GoogleSheet", "Sheets", settings.GoogleSheet.Sheets);
                settings.CodeGen.Target = target;
                settings.CodeGen.Namespace = IniFile.Get(ini, "CodeGen", "Namespace", settings.CodeGen.Namespace);
                settings.CodeGen.DatabaseClassName = IniFile.Get(ini, "CodeGen", "DatabaseClassName", settings.CodeGen.DatabaseClassName);
                settings.CodeGen.DatabaseOutputDirectory = IniFile.Get(ini, "CodeGen", "DatabaseOutputDirectory", settings.CodeGen.DatabaseOutputDirectory);
                settings.CodeGen.StructOutputDirectory = IniFile.Get(ini, "CodeGen", "StructOutputDirectory", settings.CodeGen.StructOutputDirectory);
                settings.Json.OutputPath = IniFile.Get(ini, "Json", "OutputPath", settings.Json.OutputPath);
                return settings;
            }

            public void Save(string path, string target)
            {
                CodeGen.Target = target;
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                File.WriteAllText(path, ToIniText(), Encoding.UTF8);
            }

            private static BuilderIniSettings CreateDefault(string target)
            {
                return new BuilderIniSettings
                {
                    GoogleSheet = GoogleSheetSettings.Default,
                    CodeGen = CodeGenSettings.CreateDefault(target),
                    Json = JsonSettings.Default
                };
            }

            private string ToIniText()
            {
                return string.Join(Environment.NewLine, new[]
                {
                    "[GoogleSheet]",
                    "# ServiceAccount | ApiKey | Local",
                    "AuthMode = " + GoogleSheet.AuthMode,
                    "SpreadsheetId = " + GoogleSheet.SpreadsheetId,
                    "ServiceAccountJsonPath = " + GoogleSheet.ServiceAccountJsonPath,
                    "ApiKey = " + GoogleSheet.ApiKey,
                    "LocalDirectory = " + GoogleSheet.LocalDirectory,
                    "Sheets = " + GoogleSheet.Sheets,
                    string.Empty,
                    "[CodeGen]",
                    "# Unity | Unreal",
                    "Target = " + CodeGen.Target,
                    "Namespace = " + CodeGen.Namespace,
                    "DatabaseClassName = " + CodeGen.DatabaseClassName,
                    "DatabaseOutputDirectory = " + CodeGen.DatabaseOutputDirectory,
                    "StructOutputDirectory = " + CodeGen.StructOutputDirectory,
                    string.Empty,
                    "[Json]",
                    "OutputPath = " + Json.OutputPath,
                    string.Empty
                });
            }
        }

        [Serializable]
        private struct GoogleSheetSettings
        {
            public EAuthMode AuthMode;
            public string SpreadsheetId;
            public string ServiceAccountJsonPath;
            public string ApiKey;
            public string LocalDirectory;
            public string Sheets;

            public static GoogleSheetSettings Default
            {
                get
                {
                    return new GoogleSheetSettings
                    {
                        AuthMode = EAuthMode.ServiceAccount,
                        SpreadsheetId = string.Empty,
                        ServiceAccountJsonPath = "./credentials/service-account.json",
                        ApiKey = string.Empty,
                        LocalDirectory = string.Empty,
                        Sheets = string.Empty
                    };
                }
            }
        }

        [Serializable]
        private struct CodeGenSettings
        {
            public string Target;
            public string Namespace;
            public string DatabaseClassName;
            public string DatabaseOutputDirectory;
            public string StructOutputDirectory;

            public static CodeGenSettings CreateDefault(string target)
            {
                return new CodeGenSettings
                {
                    Target = target,
                    Namespace = "BS.Data",
                    DatabaseClassName = "SheetDataBase",
                    DatabaseOutputDirectory = "./Generated/Database",
                    StructOutputDirectory = "./Generated/Database/Structs"
                };
            }
        }

        [Serializable]
        private struct JsonSettings
        {
            public string OutputPath;

            public static JsonSettings Default
            {
                get { return new JsonSettings { OutputPath = "./Generated/SheetDataBase.json" }; }
            }
        }

        private static class IniFile
        {
            public static Dictionary<string, Dictionary<string, string>> Read(string path)
            {
                Dictionary<string, Dictionary<string, string>> sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                string currentSection = string.Empty;

                foreach (string rawLine in File.ReadAllLines(path))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                    {
                        currentSection = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }

                    int separator = line.IndexOf('=');
                    if (separator < 0)
                    {
                        continue;
                    }

                    if (sections.TryGetValue(currentSection, out Dictionary<string, string> values) == false)
                    {
                        values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        sections[currentSection] = values;
                    }

                    values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
                }

                return sections;
            }

            public static string Get(Dictionary<string, Dictionary<string, string>> ini, string section, string key, string defaultValue)
            {
                return ini.TryGetValue(section, out Dictionary<string, string> values) && values.TryGetValue(key, out string value) ? value : defaultValue;
            }

            public static T GetEnum<T>(Dictionary<string, Dictionary<string, string>> ini, string section, string key, T defaultValue) where T : struct
            {
                string value = Get(ini, section, key, defaultValue.ToString());
                return Enum.TryParse(value, true, out T parsedValue) ? parsedValue : defaultValue;
            }
        }

    }
}
