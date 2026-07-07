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
	/// --force  : 해시가 같아도 모든 코드를 다시 생성한다.
	/// </summary> 
	public static class SheetSchemaBuilder
	{
		public static async Task<int> Process(string[] args)
		{
			Console.OutputEncoding = Encoding.UTF8;

			try
			{
				await RunAsync(args);
				return 0;
			}
			catch (SheetSchemaBuilderException exception)
			{
				Console.Error.WriteLine($"[오류] {exception.Message}");
				return 1;
			}
			finally
			{
				System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
				GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
				GC.WaitForPendingFinalizers();
				GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
			}
		}


		/// <summary>시트 로드, 코드 생성, Json 내보내기 작업을 순서대로 실행한다.</summary>
		private static async Task RunAsync(string[] args)
		{
			bool force = args.Contains("--force", StringComparer.OrdinalIgnoreCase);
			string iniPath = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "Sheet-Schema-Builder.ini";

			BuilderConfig config = BuilderConfig.Load(iniPath);
			Console.WriteLine($"설정 로드: {Path.GetFullPath(iniPath)} (AuthMode: {config.AuthMode}, CodeGenTarget: {config.CodeGenTarget})");

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

			Console.WriteLine($"시트 {rawSheets.Count}개 로드: {string.Join(", ", rawSheets.Select(s => s.Name))}");

			// 시트 구조(1행: 타입, 2행: 필드명, 3행~: 데이터)를 해석하고 참조 관계를 검증한다.
			List<SheetTable> tables = rawSheets.Select(SheetTable.Parse).ToList();
			SheetTable.ResolveReferences(tables);
			EnumRegistry enums = EnumRegistry.Build(tables);

			// 2~4, 8-1. 구조체 / enum / 데이터베이스 클래스를 코드 제네레이션한다.
			Console.WriteLine();
			Console.WriteLine("코드 생성:");
			CreateCodeGenerator(config, tables, enums, force).GenerateAll();

			// 7~9. 데이터 무결성(키 중복, ref 대상 존재)을 검증하며 전체 데이터를 Json으로 저장한다.
			new JsonExporter(tables, enums).Export(config.JsonOutputPath);

			Console.WriteLine();
			Console.WriteLine($"Json 저장: {config.JsonOutputPath}");
			Console.WriteLine($"완료 — 시트 {tables.Count}개, 총 {tables.Sum(t => t.Rows.Count)}개 행.");
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
