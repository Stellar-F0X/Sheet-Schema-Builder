using System.Text;
using System.Reflection;
using SheetSchemaBuilder;

namespace DataBuilder.CodeGen
{
    /// <summary>CodeGen 템플릿 파일을 읽고 문자열 치환을 적용한다.</summary>
    public sealed class CodeTemplate
    {
        private const string _PACKAGE_RELATIVE_DIRECTORY = "Package";
        private const string _UNREAL_PLUGIN_DIRECTORY = "SheetSchemaBuilder";
        private const string _LEGACY_TEMPLATE_RELATIVE_DIRECTORY = "Templates";
        private const string _DEFAULT_TEMPLATE_FILE_NAME = "UnityCodeTemplates.txt";
        private const string _TEMPLATE_START_PREFIX = "# TEMPLATE ";
        private const string _TEMPLATE_END = "# END_TEMPLATE";

        private readonly string _templateFileName;
        private readonly string _templateEngineDirectory;
        private Dictionary<string, string>? _templates;

        /// <summary>지정한 CodeGen 템플릿 파일을 읽을 준비를 한다.</summary>
        public CodeTemplate(string templateFileName = _DEFAULT_TEMPLATE_FILE_NAME)
        {
            _templateFileName = templateFileName;
            _templateEngineDirectory = ResolveEngineDirectory(templateFileName);
        }

        /// <summary>템플릿 파일을 읽고 {{KEY}} 형태의 자리표시자를 치환한다.</summary>
        public string Render(string templateName, params (string Key, string Value)[] replacements)
        {
            string source = Load(templateName);

            foreach ((string key, string value) in replacements)
            {
                source = source.Replace("{{" + key + "}}", value);
            }

            string normalized = NormalizeLineEndings(source, Environment.NewLine);
            return normalized.TrimEnd('\r', '\n');
        }

        /// <summary>템플릿 섹션 내용을 캐시해서 돌려준다.</summary>
        private string Load(string templateName)
        {
            _templates ??= LoadTemplates(_templateFileName, _templateEngineDirectory);

            if (_templates.TryGetValue(templateName, out string? templateText))
            {
                return templateText;
            }

            throw new SheetSchemaBuilderException($"CodeGen 템플릿 섹션을 찾을 수 없습니다: {templateName}");
        }

        /// <summary>하나의 템플릿 파일에서 모든 템플릿 섹션을 읽는다.</summary>
        private static Dictionary<string, string> LoadTemplates(string templateFileName, string templateEngineDirectory)
        {
            string[] lines = LoadTemplateLines(templateFileName, templateEngineDirectory);
            Dictionary<string, string> templates = new Dictionary<string, string>(StringComparer.Ordinal);
            string? currentName = null;
            List<string> currentLines = new List<string>();

            foreach (string line in lines)
            {
                if (line.StartsWith(_TEMPLATE_START_PREFIX, StringComparison.Ordinal))
                {
                    if (currentName != null)
                    {
                        throw new SheetSchemaBuilderException($"CodeGen 템플릿 섹션 종료가 누락되었습니다: {currentName}");
                    }

                    currentName = line[_TEMPLATE_START_PREFIX.Length..].Trim();
                    currentLines.Clear();
                    continue;
                }

                if (line == _TEMPLATE_END)
                {
                    if (currentName == null)
                    {
                        throw new SheetSchemaBuilderException("CodeGen 템플릿 종료 태그가 시작 태그 없이 등장했습니다.");
                    }

                    templates[currentName] = string.Join(Environment.NewLine, currentLines);
                    currentName = null;
                    currentLines.Clear();
                    continue;
                }

                if (currentName != null)
                {
                    currentLines.Add(line);
                }
            }

            if (currentName != null)
            {
                throw new SheetSchemaBuilderException($"CodeGen 템플릿 섹션 종료가 누락되었습니다: {currentName}");
            }

            return templates;
        }

        private static string[] LoadTemplateLines(string templateFileName, string templateEngineDirectory)
        {
            string? templatePath = FindTemplatePath(templateFileName, templateEngineDirectory);

            if (templatePath != null)
            {
                return File.ReadAllLines(templatePath, Encoding.UTF8);
            }

            foreach (string resourceName in GetResourceNames(templateFileName, templateEngineDirectory))
            {
                using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);

                if (stream == null)
                {
                    continue;
                }

                using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                return NormalizeLineEndings(reader.ReadToEnd(), "\n").Split('\n');
            }

            throw new SheetSchemaBuilderException($"CodeGen 템플릿 파일을 찾을 수 없습니다: {templateFileName}");
        }

        /// <summary>실행 출력 폴더와 소스 폴더에서 엔진별 템플릿 파일을 찾는다.</summary>
        private static string? FindTemplatePath(string templateName, string templateEngineDirectory)
        {
#if !NATIVE_AOT
            string? assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrWhiteSpace(assemblyDirectory) == false)
            {
                string? assemblyPath = FindTemplatePathUnder(assemblyDirectory, templateEngineDirectory, templateName);
                if (assemblyPath != null)
                {
                    return assemblyPath;
                }
            }
#endif

            string? appBasePath = FindTemplatePathUnder(AppContext.BaseDirectory, templateEngineDirectory, templateName);
            if (appBasePath != null)
            {
                return appBasePath;
            }

            string? currentDirectoryPath = FindTemplatePathUnder(Environment.CurrentDirectory, templateEngineDirectory, templateName);
            if (currentDirectoryPath != null)
            {
                return currentDirectoryPath;
            }

            return null;
        }

        /// <summary>시작 디렉터리부터 상위 디렉터리까지 올라가며 템플릿 파일을 찾는다.</summary>
        private static string? FindTemplatePathUnder(string startDirectory, string templateEngineDirectory, string templateName)
        {
            DirectoryInfo? directory = new DirectoryInfo(startDirectory);
            while (directory != null)
            {
                foreach (string candidatePath in GetTemplatePathCandidates(directory.FullName, templateEngineDirectory, templateName))
                {
                    if (File.Exists(candidatePath))
                    {
                        return candidatePath;
                    }
                }

                directory = directory.Parent;
            }

            return null;
        }

        /// <summary>템플릿 파일 이름에서 현재 엔진별 패키지 폴더 이름을 추론한다.</summary>
        private static string ResolveEngineDirectory(string templateFileName)
        {
            if (templateFileName.StartsWith("Unity", StringComparison.OrdinalIgnoreCase))
            {
                return "Unity";
            }

            if (templateFileName.StartsWith("Unreal", StringComparison.OrdinalIgnoreCase))
            {
                return "Unreal";
            }

            return string.Empty;
        }

        /// <summary>현재 폴더 구조와 이전 폴더 구조를 모두 고려한 템플릿 후보 경로를 만든다.</summary>
        private static IEnumerable<string> GetTemplatePathCandidates(string baseDirectory, string templateEngineDirectory, string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateEngineDirectory) == false)
            {
                if (templateEngineDirectory.Equals("Unreal", StringComparison.OrdinalIgnoreCase))
                {
                    yield return Path.Combine(baseDirectory, _PACKAGE_RELATIVE_DIRECTORY, templateEngineDirectory, _UNREAL_PLUGIN_DIRECTORY, templateName);
                    yield return Path.Combine(baseDirectory, templateEngineDirectory, _UNREAL_PLUGIN_DIRECTORY, templateName);
                }

                yield return Path.Combine(baseDirectory, _PACKAGE_RELATIVE_DIRECTORY, templateEngineDirectory, templateName);
                yield return Path.Combine(baseDirectory, templateEngineDirectory, templateName);
            }

            yield return Path.Combine(baseDirectory, templateName);
            yield return Path.Combine(baseDirectory, _LEGACY_TEMPLATE_RELATIVE_DIRECTORY, templateName);
        }

        /// <summary>현재 embedded resource 이름과 이전 resource 이름 후보를 만든다.</summary>
        private static IEnumerable<string> GetResourceNames(string templateFileName, string templateEngineDirectory)
        {
            if (string.IsNullOrWhiteSpace(templateEngineDirectory) == false)
            {
                yield return templateEngineDirectory + "." + templateFileName;
                yield return _PACKAGE_RELATIVE_DIRECTORY + "." + templateEngineDirectory + "." + templateFileName;
            }

            yield return _LEGACY_TEMPLATE_RELATIVE_DIRECTORY + "." + templateFileName;
            yield return templateFileName;
        }

        private static string NormalizeLineEndings(string text, string replacement)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", replacement);
        }
    }
}
