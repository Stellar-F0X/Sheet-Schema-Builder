using SheetSchemaBuilder;

namespace DataBuilder.Configuration
{
	/// <summary>
	/// 단순 .ini 파서. [섹션] / key = value / ';' 또는 '#' 주석을 지원한다.
	/// 섹션명과 키는 대소문자를 구분하지 않는다.
	/// </summary>
	public sealed class IniFile
	{
		private readonly Dictionary<string, Dictionary<string, string>> _sections = new(StringComparer.OrdinalIgnoreCase);

		
		/// <summary>파일에서 ini 설정을 읽어온다.</summary>
		public static IniFile Load(string path)
		{
			if (File.Exists(path) == false)
			{
				throw new SheetSchemaBuilderException($".ini 파일을 찾을 수 없습니다: {path}");
			}

			IniFile ini = new IniFile();
			string currentSection = string.Empty;

			foreach (string rawLine in File.ReadAllLines(path))
			{
				string line = rawLine.Trim();

				if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
				{
					continue;
				}

				if (line.StartsWith('[') && line.EndsWith(']'))
				{
					currentSection = line[1..^1].Trim();
					continue;
				}

				int separator = line.IndexOf('=');

				if (separator < 0)
				{
					continue;
				}

				string key = line[..separator].Trim();
				string value = line[(separator + 1)..].Trim();

				if (ini._sections.TryGetValue(currentSection, out Dictionary<string, string>? section) == false)
				{
					section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
					ini._sections[currentSection] = section;
				}

				section[key] = value;
			}

			return ini;
		}

		
		/// <summary>지정한 섹션과 키의 값을 가져온다. 값이 없으면 기본값을 돌려준다.</summary>
		public string Get(string section, string key, string defaultValue = "")
		{
			if (_sections.TryGetValue(section, out Dictionary<string, string>? values) && values.TryGetValue(key, out string? value))
			{
				return value;
			}
			else
			{
				return defaultValue;
			}
		}

		
		/// <summary>지정한 섹션과 키의 필수 값을 가져온다.</summary>
		public string GetRequired(string section, string key)
		{
			string value = Get(section, key);

			if (string.IsNullOrWhiteSpace(value))
			{
				throw new SheetSchemaBuilderException($".ini에 [{section}] {key} 값이 필요합니다.");
			}
			else
			{
				return value;
			}
		}
	}
}