using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using Debug = UnityEngine.Debug;

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

            LoadIni();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                DrawToolbar();

                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                DrawGoogleSheetSection();
                DrawCodeGenSection();
                DrawJsonSection();
                EditorGUILayout.EndScrollView();

                DrawFooter();
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("INI File", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(_iniPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

                if (GUILayout.Button("Browse", GUILayout.Width(76)))
                {
                    string selectedPath = EditorUtility.OpenFilePanel("Select Sheet-Schema-Builder.ini", Path.GetDirectoryName(_iniPath), "ini");
                    if (string.IsNullOrWhiteSpace(selectedPath) == false)
                    {
                        _iniPath = selectedPath;
                        LoadIni();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload"))
                {
                    LoadIni();
                }

                if (GUILayout.Button("Reveal"))
                {
                    EditorUtility.RevealInFinder(_iniPath);
                }
            }

            EditorGUILayout.Space(8);
        }

        private void DrawGoogleSheetSection()
        {
            DrawSectionHeader("Google Sheet");
            _settings.GoogleSheet.AuthMode = (EAuthMode)EditorGUILayout.EnumPopup("Auth Mode", _settings.GoogleSheet.AuthMode);
            _settings.GoogleSheet.SpreadsheetId = EditorGUILayout.TextField("Spreadsheet ID", _settings.GoogleSheet.SpreadsheetId);
            DrawPathField("Service Account JSON", ref _settings.GoogleSheet.ServiceAccountJsonPath, false, "json");
            _settings.GoogleSheet.ApiKey = EditorGUILayout.TextField("API Key", _settings.GoogleSheet.ApiKey);
            DrawPathField("Local TSV Directory", ref _settings.GoogleSheet.LocalDirectory, true, string.Empty);
            _settings.GoogleSheet.Sheets = EditorGUILayout.TextField("Sheets", _settings.GoogleSheet.Sheets);
            EditorGUILayout.HelpBox("Sheets is a comma-separated list. Leave it empty to fetch every sheet.", MessageType.None);
            EditorGUILayout.Space(8);
        }

        private void DrawCodeGenSection()
        {
            DrawSectionHeader("Code Generation");
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Target", TargetName);
            }

            _settings.CodeGen.Namespace = EditorGUILayout.TextField("Namespace", _settings.CodeGen.Namespace);
            _settings.CodeGen.DatabaseClassName = EditorGUILayout.TextField("Database Class Name", _settings.CodeGen.DatabaseClassName);
            DrawPathField("Database Output Directory", ref _settings.CodeGen.DatabaseOutputDirectory, true, string.Empty);
            DrawPathField("Struct Output Directory", ref _settings.CodeGen.StructOutputDirectory, true, string.Empty);
            EditorGUILayout.Space(8);
        }

        private void DrawJsonSection()
        {
            DrawSectionHeader("Json");
            DrawPathField("Output Path", ref _settings.Json.OutputPath, false, "json");
            EditorGUILayout.Space(8);
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Save INI", GUILayout.Width(120), GUILayout.Height(28)))
                {
                    SaveIni();
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

        private void DrawSectionHeader(string title)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void DrawPathField(string label, ref string path, bool folder, string extension)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                path = EditorGUILayout.TextField(label, path);

                if (GUILayout.Button("...", GUILayout.Width(32)))
                {
                    string baseDirectory = Directory.Exists(Path.GetDirectoryName(_iniPath) ?? string.Empty) ? Path.GetDirectoryName(_iniPath) : Application.dataPath;
                    string selectedPath = folder ? EditorUtility.OpenFolderPanel(label, baseDirectory, string.Empty) : EditorUtility.OpenFilePanel(label, baseDirectory, extension);

                    if (string.IsNullOrWhiteSpace(selectedPath) == false)
                    {
                        path = ToIniRelativePath(selectedPath);
                    }
                }
            }
        }

        private void LoadIni()
        {
            _settings = BuilderIniSettings.FromIni(_iniPath, TargetName);
            Repaint();
        }

        private void SaveIni()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_iniPath) ?? ".");
            File.WriteAllText(_iniPath, _settings.ToIniText(TargetName), Encoding.UTF8);
            AssetDatabase.Refresh();
            ShowNotification(new GUIContent("Saved Sheet-Schema-Builder.ini"));
        }

        private async void RunBuilder(bool force)
        {
            if (File.Exists(_iniPath) == false)
            {
                EditorUtility.DisplayDialog("Sheet Schema Builder", "INI file does not exist. Press Save INI before running.\n\n" + _iniPath, "OK");
                return;
            }

            string dllPath = FindBuilderDllPath();
            if (File.Exists(dllPath) == false)
            {
                EditorUtility.DisplayDialog("Sheet Schema Builder", "Sheet-Schema-Builder.dll was not found.\n\n" + dllPath, "OK");
                return;
            }

            _isRunning = true;
            try
            {
                EditorUtility.DisplayProgressBar("Sheet Schema Builder", "Running builder...", 0.5f);
                BuilderRunResult result = await Task.Run(() => ExecuteBuilder(dllPath, force));
                string log = result.StandardOutput + result.StandardError;

                if (string.IsNullOrWhiteSpace(result.StandardOutput) == false)
                {
                    Debug.Log(result.StandardOutput);
                }

                if (result.ExitCode == 0)
                {
                    AssetDatabase.Refresh();
                    ShowNotification(new GUIContent("Sheet Schema Builder completed"));
                    EditorUtility.DisplayDialog("Sheet Schema Builder", string.IsNullOrWhiteSpace(log) ? "Completed." : log, "OK");
                }
                else
                {
                    Debug.LogError(log);
                    EditorUtility.DisplayDialog("Sheet Schema Builder Failed", string.IsNullOrWhiteSpace(log) ? "Exit code: " + result.ExitCode : log, "OK");
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

        private string FindBuilderDllPath()
        {
            string iniDirectory = Path.GetDirectoryName(_iniPath) ?? string.Empty;
            string iniDirectoryDll = Path.Combine(iniDirectory, "Sheet-Schema-Builder.dll");
            if (File.Exists(iniDirectoryDll))
            {
                return iniDirectoryDll;
            }

            return Path.Combine(FindPackageRoot(), "Sheet-Schema-Builder.dll");
        }

        private BuilderRunResult ExecuteBuilder(string dllPath, bool force)
        {
            string arguments = Quote(dllPath) + " " + Quote(_iniPath) + (force ? " --force" : string.Empty);
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(_iniPath) ?? Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start dotnet process.");
                }

                string standardOutput = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new BuilderRunResult(process.ExitCode, standardOutput, standardError);
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string FindPackageRoot()
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(SheetSchemaBuilderSettingsWindow).Assembly);
            if (packageInfo != null && string.IsNullOrWhiteSpace(packageInfo.resolvedPath) == false)
            {
                return packageInfo.resolvedPath;
            }

            string packagePath = Path.Combine(Directory.GetCurrentDirectory(), "Packages", PackageName);
            return Directory.Exists(packagePath) ? packagePath : Path.Combine(Directory.GetCurrentDirectory(), "Package", TargetName);
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

            public static BuilderIniSettings CreateDefault(string target)
            {
                return new BuilderIniSettings
                {
                    GoogleSheet = GoogleSheetSettings.Default,
                    CodeGen = CodeGenSettings.CreateDefault(target),
                    Json = JsonSettings.Default
                };
            }

            public static BuilderIniSettings FromIni(string path, string target)
            {
                BuilderIniSettings settings = CreateDefault(target);
                if (File.Exists(path) == false)
                {
                    return settings;
                }

                Dictionary<string, Dictionary<string, string>> ini = ReadIni(path);
                settings.GoogleSheet.AuthMode = ParseEnum(Get(ini, "GoogleSheet", "AuthMode", settings.GoogleSheet.AuthMode.ToString()), settings.GoogleSheet.AuthMode);
                settings.GoogleSheet.SpreadsheetId = Get(ini, "GoogleSheet", "SpreadsheetId", settings.GoogleSheet.SpreadsheetId);
                settings.GoogleSheet.ServiceAccountJsonPath = Get(ini, "GoogleSheet", "ServiceAccountJsonPath", settings.GoogleSheet.ServiceAccountJsonPath);
                settings.GoogleSheet.ApiKey = Get(ini, "GoogleSheet", "ApiKey", settings.GoogleSheet.ApiKey);
                settings.GoogleSheet.LocalDirectory = Get(ini, "GoogleSheet", "LocalDirectory", settings.GoogleSheet.LocalDirectory);
                settings.GoogleSheet.Sheets = Get(ini, "GoogleSheet", "Sheets", settings.GoogleSheet.Sheets);
                settings.CodeGen.Target = target;
                settings.CodeGen.Namespace = Get(ini, "CodeGen", "Namespace", settings.CodeGen.Namespace);
                settings.CodeGen.DatabaseClassName = Get(ini, "CodeGen", "DatabaseClassName", settings.CodeGen.DatabaseClassName);
                settings.CodeGen.DatabaseOutputDirectory = Get(ini, "CodeGen", "DatabaseOutputDirectory", settings.CodeGen.DatabaseOutputDirectory);
                settings.CodeGen.StructOutputDirectory = Get(ini, "CodeGen", "StructOutputDirectory", settings.CodeGen.StructOutputDirectory);
                settings.Json.OutputPath = Get(ini, "Json", "OutputPath", settings.Json.OutputPath);
                return settings;
            }

            public string ToIniText(string target)
            {
                CodeGen.Target = target;
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

        private static Dictionary<string, Dictionary<string, string>> ReadIni(string path)
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

        private static string Get(Dictionary<string, Dictionary<string, string>> ini, string section, string key, string defaultValue)
        {
            return ini.TryGetValue(section, out Dictionary<string, string> values) && values.TryGetValue(key, out string value) ? value : defaultValue;
        }

        private static T ParseEnum<T>(string value, T defaultValue) where T : struct
        {
            return Enum.TryParse(value, true, out T parsedValue) ? parsedValue : defaultValue;
        }

        private readonly struct BuilderRunResult
        {
            public BuilderRunResult(int exitCode, string standardOutput, string standardError)
            {
                ExitCode = exitCode;
                StandardOutput = standardOutput;
                StandardError = standardError;
            }

            public int ExitCode
            {
                get;
            }

            public string StandardOutput
            {
                get;
            }

            public string StandardError
            {
                get;
            }
        }
    }
}
