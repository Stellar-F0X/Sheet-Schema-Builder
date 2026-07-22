using System.Text;
using DataBuilder.CodeGen;
using DataBuilder.Configuration;
using DataBuilder.Export;
using DataBuilder.Model;
using DataBuilder.Sheets;
using SheetSchemaBuilder;

namespace DataBuilder
{
	/// <summary>
	/// Data Builder — Google Sheet를 읽어 C# 구조체 / 데이터베이스 클래스를 코드 제네레이션하고,
	/// 전체 데이터를 다시 읽을 수 있는 Json으로 저장하는 도구.
	///
	/// 사용법: DataBuilder [ini경로] [--force]
	/// ini경로  : 설정 파일 경로 (기본값: 실행 디렉터리의 Sheet-Schema-Builder.ini)
	/// --base-directory 경로 : ini에 적힌 상대 경로의 기준 디렉터리를 재정의한다.
	/// --force  : 해시가 같아도 모든 코드를 다시 생성한다.
	/// </summary> 
	public static class SheetSchemaBuilder
	{
		/// <summary>CLI 호환 진입점. 로그는 현재 프로세스의 표준 출력과 오류 출력으로 보낸다.</summary>
		public static async Task<int> Process(string[] args)
		{
			Console.OutputEncoding = Encoding.UTF8;
			return await ProcessCore(args, Console.Out, Console.Error);
		}

		/// <summary>Unity 등 내장 호출자가 표시할 수 있도록 실행 결과와 로그 문자열을 함께 반환한다.</summary>
		public static async Task<BuilderProcessResult> ProcessWithResult(string[] args)
		{
			StringBuilder outputBuilder = new StringBuilder();
			StringBuilder errorBuilder = new StringBuilder();

			using StringWriter output = new StringWriter(outputBuilder);
			using StringWriter error = new StringWriter(errorBuilder);
			int exitCode;

			try
			{
				exitCode = await ProcessCore(args, output, error);
			}
			catch (Exception exception)
			{
				error.WriteLine("[예외] " + exception);
				exitCode = 1;
			}

			return new BuilderProcessResult(exitCode, outputBuilder.ToString(), errorBuilder.ToString());
		}

		private static async Task<int> ProcessCore(string[] args, TextWriter output, TextWriter error)
		{
			using IDisposable logScope = BuilderLog.Push(output, error);

			try
			{
				await RunAsync(args);
				return 0;
			}
			catch (SheetSchemaBuilderException exception)
			{
				BuilderLog.Error.WriteLine($"[오류] {exception.Message}");
				return 1;
			}
			finally
			{
				CollectGarbage();
			}
		}

		private static void CollectGarbage()
		{
			try
			{
				System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
				GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
			}
			catch (NotSupportedException)
			{
				GC.Collect();
			}

			GC.WaitForPendingFinalizers();
		}


		/// <summary>시트 로드, 코드 생성, Json 내보내기 작업을 순서대로 실행한다.</summary>
		private static async Task RunAsync(string[] args)
		{
			bool force = false;
			string iniPath = "Sheet-Schema-Builder.ini";
			string? baseDirectoryOverride = null;
			ECodeGenTarget? targetOverride = null;
			for (int i = 0; i < args.Length; i++)
			{
				string arg = args[i];
				if (arg.Equals("--force", StringComparison.OrdinalIgnoreCase))
				{
					force = true;
				}
				else if (arg.Equals("--target", StringComparison.OrdinalIgnoreCase))
				{
					if (++i >= args.Length || Enum.TryParse(args[i], ignoreCase: true, out ECodeGenTarget parsedTarget) == false)
					{
						throw new SheetSchemaBuilderException("--target 값이 잘못되었습니다. (Unity | Unreal)");
					}

					targetOverride = parsedTarget;
				}
				else if (arg.Equals("--base-directory", StringComparison.OrdinalIgnoreCase))
				{
					if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i]))
					{
						throw new SheetSchemaBuilderException("--base-directory 경로가 필요합니다.");
					}

					baseDirectoryOverride = args[i];
				}
				else if (arg.StartsWith("--", StringComparison.Ordinal))
				{
					throw new SheetSchemaBuilderException($"지원하지 않는 옵션입니다: {arg}");
				}
				else
				{
					iniPath = arg;
				}
			}

			BuilderConfig config = BuilderConfig.Load(iniPath, targetOverride, baseDirectoryOverride);
			BuilderLog.Output.WriteLine($"설정 로드: {Path.GetFullPath(iniPath)} (AuthMode: {config.AuthMode}, CodeGenTarget: {config.CodeGenTarget})");

			// 1. 시트 데이터를 가져온다.
			ISheetSource source;

			if (config.AuthMode == EAuthMode.Local)
			{
				source = new LocalTsvSheetSource(config.LocalDirectory);
			}
			else
			{
				source = new GoogleSheetSource(config);
			}

			IReadOnlyList<RawSheet> rawSheets;

			try
			{
				rawSheets = await source.FetchAsync(config.SheetFilter);
			}
			finally
			{
				(source as IDisposable)?.Dispose();
			}

			BuilderLog.Output.WriteLine($"시트 {rawSheets.Count}개 로드: {string.Join(", ", rawSheets.Select(s => s.Name))}");

			// 시트 구조(1행: 타입, 2행: 필드명, 3행~: 데이터)를 해석하고 참조 관계를 검증한다.
			List<SheetTable> tables = rawSheets.Select(SheetTable.Parse).ToList();
			SheetTable.ResolveReferences(tables);
			EnumRegistry enums = EnumRegistry.Build(tables);

			// 2~4, 8-1. 구조체 / enum / 데이터베이스 클래스를 코드 제네레이션한다.
			BuilderLog.Output.WriteLine();
			BuilderLog.Output.WriteLine("코드 생성:");
			CreateCodeGenerator(config, tables, enums, force).GenerateAll();

			// 7~9. 데이터 무결성(키 중복, ref 대상 존재)을 검증하며 전체 데이터를 Json으로 저장한다.
			new JsonExporter(tables, enums).Export(config.JsonOutputPath);

			BuilderLog.Output.WriteLine();
			BuilderLog.Output.WriteLine($"Json 저장: {config.JsonOutputPath}");
			BuilderLog.Output.WriteLine($"완료 — 시트 {tables.Count}개, 총 {tables.Sum(t => t.Rows.Count)}개 행.");
		}


		/// <summary>설정된 타겟에 맞는 코드 제너레이터를 만든다.</summary>
		private static ICodeGenerator CreateCodeGenerator(BuilderConfig config, IReadOnlyList<SheetTable> tables, EnumRegistry enums, bool force)
		{
			switch (config.CodeGenTarget)
			{
				case ECodeGenTarget.Unity: return new UnityCodeGenerator(config, tables, enums, force);

				case ECodeGenTarget.Unreal: return new UnrealCodeGenerator(config, tables, enums, force);

				default: throw new SheetSchemaBuilderException($"지원하지 않는 CodeGen Target입니다: {config.CodeGenTarget}");
			}
		}
	}
}
