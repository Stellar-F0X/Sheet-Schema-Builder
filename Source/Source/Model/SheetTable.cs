using DataBuilder.CodeGen;
using SheetSchemaBuilder;

namespace DataBuilder.Model
{
	/// <summary>시트에서 그대로 읽어온 2차원 문자열 데이터.</summary>
	public sealed class RawSheet
	{
		/// <summary>시트 원본 데이터를 생성한다.</summary>
		public RawSheet(string name, IReadOnlyList<IReadOnlyList<string>> rows)
		{
			Name = name;
			Rows = rows;
		}

		public string Name
		{
			get;
		}

		public IReadOnlyList<IReadOnlyList<string>> Rows
		{
			get;
		}
	}


	/// <summary>타입(1행)·필드명(2행)·데이터(3행~)가 해석된 시트 한 장.</summary>
	public sealed class SheetTable
	{
		public required string Name
		{
			get;
			init;
		}

		/// <summary>생성될 구조체 이름 (예: Item → SItemRow).</summary>
		public string StructName
		{
			get { return Identifier.EnsurePrefix(Identifier.Sanitize(Name) + "Row", "S"); }
		}

		public required IReadOnlyList<ColumnSpec> Columns
		{
			get;
			init;
		}

		/// <summary>3행부터의 데이터. 각 행은 Columns 수만큼 패딩되어 있다.</summary>
		public required IReadOnlyList<IReadOnlyList<string>> Rows
		{
			get;
			init;
		}

		/// <summary>키 컬럼(첫 번째 컬럼). 관계형 조회의 Key로 사용된다.</summary>
		public ColumnSpec KeyColumn
		{
			get { return Columns[0]; }
		}

		/// <summary>시트 이름 + 2행 필드명들을 더한 문자열의 SHA-256 해시.</summary>
		public required string Hash
		{
			get;
			init;
		}


		/// <summary>원본 시트 데이터를 해석된 시트 모델로 변환한다.</summary>
		public static SheetTable Parse(RawSheet raw)
		{
			if (raw.Rows.Count < 2)
			{
				throw new SheetSchemaBuilderException($"시트 '{raw.Name}'에는 최소 2개 행(1행: 타입, 2행: 필드명)이 필요합니다.");
			}

			IReadOnlyList<string> typeRow = raw.Rows[0];
			IReadOnlyList<string> nameRow = raw.Rows[1];

			// 필드명이 있는 곳까지를 유효 컬럼으로 본다 (뒤쪽의 비어 있는 열은 무시).
			int columnCount = nameRow.Count;
			while (columnCount > 0 && string.IsNullOrWhiteSpace(nameRow[columnCount - 1]))
			{
				columnCount--;
			}

			if (columnCount == 0)
			{
				throw new SheetSchemaBuilderException($"시트 '{raw.Name}'의 2행(필드명)이 비어 있습니다.");
			}

			List<ColumnSpec> columns = new List<ColumnSpec>(columnCount);
			for (int i = 0; i < columnCount; i++)
			{
				string typeText = i < typeRow.Count ? typeRow[i] : string.Empty;
				
				if (string.IsNullOrWhiteSpace(typeText))
				{
					throw new SheetSchemaBuilderException($"시트 '{raw.Name}' {i + 1}번째 열의 타입(1행)이 비어 있습니다.");
				}

				columns.Add(ColumnSpec.Parse(raw.Name, i, typeText, nameRow[i]));
			}

			IGrouping<string, ColumnSpec>? duplicated = columns.GroupBy(c => c.FieldName)
			                                                   .FirstOrDefault(g => g.Count() > 1);

			if (duplicated != null)
			{
				throw new SheetSchemaBuilderException($"시트 '{raw.Name}'에 중복된 필드명이 있습니다: '{duplicated.Key}'");
			}

			EColumnKind keyKind = columns[0].Kind;
			
			if ((keyKind is EColumnKind.Int or EColumnKind.Long or EColumnKind.String) == false)
			{
				throw new SheetSchemaBuilderException($"시트 '{raw.Name}'의 첫 번째 컬럼은 Key로 사용되므로 int/long/string 타입이어야 합니다. (현재: {columns[0].RawType})");
			}

			List<IReadOnlyList<string>> rows = new List<IReadOnlyList<string>>();
			
			for (int r = 2; r < raw.Rows.Count; r++)
			{
				IReadOnlyList<string> source = raw.Rows[r];

				// 완전히 빈 행은 건너뛴다.
				if (source.All(string.IsNullOrWhiteSpace))
				{
					continue;
				}

				string[] padded = new string[columnCount];
				
				for (int c = 0; c < columnCount; c++)
				{
					padded[c] = c < source.Count ? source[c].Trim() : string.Empty;
				}

				rows.Add(padded);
			}

			// 시트 이름 + 타입 + 필드명들을 더한 문자열을 해시화한다.
			string hashSource = raw.Name + string.Concat(columns.Select(c => c.RawType + ":" + c.FieldName));

			return new SheetTable
			{
				Name = raw.Name,
				Columns = columns,
				Rows = rows,
				Hash = HashUtility.Sha256Hex(hashSource),
			};
		}

		/// <summary>ref 컬럼이 실제 존재하는 시트를 가리키는지, 자기 자신을 포함해 검증한다.</summary>
		public static void ResolveReferences(IReadOnlyList<SheetTable> tables)
		{
			Dictionary<string, SheetTable> byName = tables.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

			foreach (SheetTable table in tables)
			{
				foreach (ColumnSpec column in table.Columns)
				{
					if (column.Kind != EColumnKind.Ref)
					{
						continue;
					}

					if (byName.ContainsKey(column.RefSheetName))
					{
						continue;
					}

					throw new SheetSchemaBuilderException($"시트 '{table.Name}'의 컬럼 '{column.FieldName}'이 존재하지 않는 시트를 참조합니다: 'ref:{column.RefSheetName}'");
				}
			}
		}
	}
}