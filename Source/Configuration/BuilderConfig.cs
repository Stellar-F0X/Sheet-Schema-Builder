using SheetSchemaBuilder;

namespace DataBuilder.Configuration
{
	public enum EAuthMode
	{
		/// <summary>서비스 계정 JSON 키로 인증 (비공개 시트 접근 가능).</summary>
		ServiceAccount,

		/// <summary>API 키 사용 (링크 공개된 시트만 접근 가능).</summary>
		ApiKey,

		/// <summary>로컬 .tsv 파일을 시트 대신 사용 (오프라인 테스트용).</summary>
		Local,
	}

	public enum ECodeGenTarget
	{
		Unity,
		Unreal,
	}

	/// <summary>.ini에서 읽어들인 빌드 설정. 상대 경로는 .ini 파일 위치 기준으로 해석된다.</summary>
	public sealed class BuilderConfig
	{
		public required EAuthMode AuthMode
		{
			get;
			init;
		}

		public string SpreadsheetId
		{
			get;
			init;
		} = string.Empty;

		public string ServiceAccountJsonPath
		{
			get;
			init;
		} = string.Empty;

		public string ApiKey
		{
			get;
			init;
		} = string.Empty;

		public string LocalDirectory
		{
			get;
			init;
		} = string.Empty;

		/// <summary>가져올 시트 이름 목록. 비어 있으면 전체 시트를 가져온다.</summary>
		public IReadOnlyList<string> SheetFilter
		{
			get;
			init;
		} = Array.Empty<string>();

		public required string Namespace
		{
			get;
			init;
		}

		public required string DatabaseClassName
		{
			get;
			init;
		}

		public required ECodeGenTarget CodeGenTarget
		{
			get;
			init;
		}

		/// <summary>SheetDataBase 클래스가 생성될 디렉터리.</summary>
		public required string DatabaseOutputDirectory
		{
			get;
			init;
		}

		/// <summary>시트별 구조체가 생성될 디렉터리.</summary>
		public required string StructOutputDirectory
		{
			get;
			init;
		}

		/// <summary>전체 데이터가 저장될 Json 파일 경로.</summary>
		public required string JsonOutputPath
		{
			get;
			init;
		}


		/// <summary>ini 파일에서 빌드 설정을 로드한다.</summary>
		public static BuilderConfig Load(string iniPath)
		{
			IniFile ini = IniFile.Load(iniPath);
			string baseDir = Path.GetDirectoryName(Path.GetFullPath(iniPath))!;
			string authModeText = ini.Get("GoogleSheet", "AuthMode", "ServiceAccount");

			if (Enum.TryParse(authModeText, ignoreCase: true, out EAuthMode authMode) == false)
			{
				throw new SheetSchemaBuilderException($"[GoogleSheet] AuthMode 값이 잘못되었습니다: '{authModeText}' (ServiceAccount | ApiKey | Local)");
			}

			string codeGenTargetText = ini.Get("CodeGen", "Target", "Unity");

			if (Enum.TryParse(codeGenTargetText, ignoreCase: true, out ECodeGenTarget codeGenTarget) == false)
			{
				throw new SheetSchemaBuilderException($"[CodeGen] Target 값이 잘못되었습니다: '{codeGenTargetText}' (Unity | Unreal)");
			}

			string[] sheetFilter = ini.Get("GoogleSheet", "Sheets").Split(',').Select(sheet => sheet.Trim()).Where(sheet => sheet.Length > 0).ToArray();

			string databaseDir = ResolvePath(baseDir, ini.GetRequired("CodeGen", "DatabaseOutputDirectory"));
			string structDir = ini.Get("CodeGen", "StructOutputDirectory");

			BuilderConfig config = new BuilderConfig
			{
				StructOutputDirectory = string.IsNullOrWhiteSpace(structDir) ? Path.Combine(databaseDir, "Structs") : ResolvePath(baseDir, structDir),
				ServiceAccountJsonPath = ResolvePath(baseDir, ini.Get("GoogleSheet", "ServiceAccountJsonPath")),
				DatabaseClassName = ini.Get("CodeGen", "DatabaseClassName", "SheetDataBase"),
				LocalDirectory = ResolvePath(baseDir, ini.Get("GoogleSheet", "LocalDirectory")),
				JsonOutputPath = ResolvePath(baseDir, ini.GetRequired("Json", "OutputPath")),
				Namespace = ini.Get("CodeGen", "Namespace", "SheetData"),
				SpreadsheetId = ini.Get("GoogleSheet", "SpreadsheetId"),
				ApiKey = ini.Get("GoogleSheet", "ApiKey"),
				DatabaseOutputDirectory = databaseDir,
				CodeGenTarget = codeGenTarget,
				SheetFilter = sheetFilter,
				AuthMode = authMode,
			};

			config.Validate();
			return config;
		}


		private static string ResolvePath(string baseDir, string path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return string.Empty;
			}
			else
			{
				return Path.GetFullPath(Path.Combine(baseDir, path));
			}
		}


		/// <summary>인증 방식별 필수 설정값이 채워졌는지 검증한다.</summary>
		private void Validate()
		{
			switch (AuthMode)
			{
				case EAuthMode.ServiceAccount:
				{
					if (string.IsNullOrWhiteSpace(SpreadsheetId))
					{
						throw new SheetSchemaBuilderException("[GoogleSheet] SpreadsheetId 값이 필요합니다.");
					}

					if (string.IsNullOrWhiteSpace(ServiceAccountJsonPath))
					{
						throw new SheetSchemaBuilderException("[GoogleSheet] ServiceAccountJsonPath 값이 필요합니다.");
					}

					break;
				}

				case EAuthMode.ApiKey:
				{
					if (string.IsNullOrWhiteSpace(SpreadsheetId))
					{
						throw new SheetSchemaBuilderException("[GoogleSheet] SpreadsheetId 값이 필요합니다.");
					}

					if (string.IsNullOrWhiteSpace(ApiKey))
					{
						throw new SheetSchemaBuilderException("[GoogleSheet] ApiKey 값이 필요합니다.");
					}

					break;
				}

				case EAuthMode.Local:
				{
					if (string.IsNullOrWhiteSpace(LocalDirectory))
					{
						throw new SheetSchemaBuilderException("[GoogleSheet] LocalDirectory 값이 필요합니다.");
					}

					break;
				}

				default:
				{
					throw new SheetSchemaBuilderException($"지원하지 않는 AuthMode입니다: {AuthMode}");
				}
			}
		}
	}
}
