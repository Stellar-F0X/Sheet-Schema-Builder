using DataBuilder.Model;
using SheetSchemaBuilder;

namespace DataBuilder.Sheets
{
	/// <summary>
	/// 디렉터리 안의 .tsv 파일을 시트 대신 읽는다 (오프라인 테스트용).
	/// 파일 이름(확장자 제외)이 시트 이름이 된다.
	/// </summary>
	public sealed class LocalTsvSheetSource : ISheetSource
	{
		private readonly string _directory;

		/// <summary>로컬 TSV 디렉터리 경로로 시트 공급자를 생성한다.</summary>
		public LocalTsvSheetSource(string directory)
		{
			_directory = directory;
		}

		/// <summary>로컬 디렉터리에서 TSV 시트를 읽어온다.</summary>
		public Task<IReadOnlyList<RawSheet>> FetchAsync(IReadOnlyList<string> sheetFilter)
		{
			if (Directory.Exists(_directory) == false)
			{
				throw new SheetSchemaBuilderException($"로컬 시트 디렉터리를 찾을 수 없습니다: {_directory}");
			}

			IOrderedEnumerable<string> files = Directory
			                                   .GetFiles(_directory, "*.tsv")
			                                   .OrderBy(f => f, StringComparer.Ordinal);

			List<RawSheet> sheets = new List<RawSheet>();

			foreach (string file in files)
			{
				string name = Path.GetFileNameWithoutExtension(file);

				if (SheetNameRule.ShouldSkip(name))
				{
					continue;
				}

				if (sheetFilter.Count > 0 && sheetFilter.Contains(name, StringComparer.OrdinalIgnoreCase) == false)
				{
					continue;
				}

				List<IReadOnlyList<string>> rows = File.ReadAllLines(file)
				                                       .Select(IReadOnlyList<string> (line) => line.Split('\t'))
				                                       .ToList();

				sheets.Add(new RawSheet(name, rows));
			}

			if (sheets.Count == 0)
			{
				throw new SheetSchemaBuilderException($"'{_directory}'에서 읽을 .tsv 시트를 찾지 못했습니다.");
			}

			return Task.FromResult<IReadOnlyList<RawSheet>>(sheets);
		}
	}
}