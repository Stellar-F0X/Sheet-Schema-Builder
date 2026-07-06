using System.Text;
using System.Reflection;
using SheetSchemaBuilder;

namespace DataBuilder.CodeGen
{
    /// <summary>CodeGen 템플릿 파일을 읽고 문자열 치환을 적용한다.</summary>
    public sealed class CodeTemplate
    {
        private const string _TEMPLATE_RELATIVE_DIRECTORY = "Templates";
        private const string _DEFAULT_TEMPLATE_FILE_NAME = "UnityCodeTemplates.txt";
        private const string _TEMPLATE_START_PREFIX = "# TEMPLATE ";
        private const string _TEMPLATE_END = "# END_TEMPLATE";

        private readonly string _templateFileName;
        private Dictionary<string, string>? _templates;

        /// <summary>지정한 CodeGen 템플릿 파일을 읽을 준비를 한다.</summary>
        public CodeTemplate(string templateFileName = _DEFAULT_TEMPLATE_FILE_NAME)
        {
            _templateFileName = templateFileName;
        }

        /// <summary>템플릿 파일을 읽고 {{KEY}} 형태의 자리표시자를 치환한다.</summary>
        public string Render(string templateName, params (string Key, string Value)[] replacements)
        {
            string source = Load(templateName);

            foreach ((string key, string value) in replacements)
            {
                source = source.Replace("{{" + key + "}}", value);
            }

            string normalized = source.ReplaceLineEndings(Environment.NewLine);
            return normalized.TrimEnd('\r', '\n');
        }

        /// <summary>템플릿 섹션 내용을 캐시해서 돌려준다.</summary>
        private string Load(string templateName)
        {
            _templates ??= LoadTemplates(_templateFileName);

            if (_templates.TryGetValue(templateName, out string? templateText))
            {
                return templateText;
            }

            throw new SheetSchemaBuilderException($"CodeGen 템플릿 섹션을 찾을 수 없습니다: {templateName}");
        }

        /// <summary>하나의 템플릿 파일에서 모든 템플릿 섹션을 읽는다.</summary>
        private static Dictionary<string, string> LoadTemplates(string templateFileName)
        {
            string[] lines = LoadTemplateLines(templateFileName);
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

        private static string[] LoadTemplateLines(string templateFileName)
        {
            string? templatePath = FindTemplatePath(templateFileName);

            if (templatePath != null)
            {
                return File.ReadAllLines(templatePath, Encoding.UTF8);
            }

            string resourceName = "Templates." + templateFileName;
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                throw new SheetSchemaBuilderException($"CodeGen 템플릿 파일을 찾을 수 없습니다: {templateFileName}");
            }

            using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd().ReplaceLineEndings("\n").Split('\n');
        }

        /// <summary>실행 출력 폴더와 소스 폴더에서 템플릿 파일을 찾는다.</summary>
        private static string? FindTemplatePath(string templateName)
        {
            string? assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrWhiteSpace(assemblyDirectory) == false)
            {
                string? assemblyPath = FindTemplatePathUnder(assemblyDirectory, templateName);
                if (assemblyPath != null)
                {
                    return assemblyPath;
                }
            }

            string? appBasePath = FindTemplatePathUnder(AppContext.BaseDirectory, templateName);
            if (appBasePath != null)
            {
                return appBasePath;
            }

            string? currentDirectoryPath = FindTemplatePathUnder(Environment.CurrentDirectory, templateName);
            if (currentDirectoryPath != null)
            {
                return currentDirectoryPath;
            }

            return null;
        }

        /// <summary>시작 디렉터리부터 상위 디렉터리까지 올라가며 템플릿 파일을 찾는다.</summary>
        private static string? FindTemplatePathUnder(string startDirectory, string templateName)
        {
            DirectoryInfo? directory = new DirectoryInfo(startDirectory);
            while (directory != null)
            {
                string directPath = Path.Combine(directory.FullName, _TEMPLATE_RELATIVE_DIRECTORY, templateName);
                if (File.Exists(directPath))
                {
                    return directPath;
                }

                string workspacePath = Path.Combine
                (
                    directory.FullName,
                    "DataBuilder",
                    _TEMPLATE_RELATIVE_DIRECTORY,
                    templateName
                );

                if (File.Exists(workspacePath))
                {
                    return workspacePath;
                }

                directory = directory.Parent;
            }

            return null;
        }
    }
}
