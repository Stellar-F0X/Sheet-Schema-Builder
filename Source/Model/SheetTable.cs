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
			get { return "S" + Identifier.Sanitize(Name) + "Row"; }
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
			set;
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

			EColumnType keyType = columns[0].Type;

			if (IsSupportedKeyKind(keyType) == false)
			{
				throw new SheetSchemaBuilderException($"시트 '{raw.Name}'의 첫 번째 컬럼은 Key로 사용되므로 int/long/float/double/string/enum 타입이어야 합니다. (현재: {columns[0].RawType})");
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

			return new SheetTable
			{
				Name = raw.Name,
				Columns = columns,
				Rows = rows,
				Hash = BuildHash(raw.Name, columns),
			};
		}

		/// <summary>명시적 ref 컬럼과 Key 컬럼명 기반의 암시적 ref 컬럼을 해석하고 검증한다.</summary>
		public static void ResolveReferences(IReadOnlyList<SheetTable> tables)
		{
			Dictionary<string, SheetTable> byName = tables.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
			Dictionary<string, List<SheetTable>> byKeyFieldName = tables
			                                                      .GroupBy(t => t.KeyColumn.FieldName, StringComparer.OrdinalIgnoreCase)
			                                                      .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

			foreach (SheetTable table in tables)
			{
				for (int i = 0; i < table.Columns.Count; i++)
				{
					ColumnSpec column = table.Columns[i];

					if (column.Type == EColumnType.Ref)
					{
						ResolveExplicitReference(table, column, byName, byKeyFieldName);
						continue;
					}

					if (i == 0)
					{
						continue;
					}

					if (byKeyFieldName.TryGetValue(column.FieldName, out List<SheetTable>? targets) == false)
					{
						continue;
					}

					List<SheetTable> otherTargets = targets.Where(t => string.Equals(t.Name, table.Name, StringComparison.OrdinalIgnoreCase) == false)
					                                       .ToList();

					if (otherTargets.Count == 0)
					{
						continue;
					}

					if (otherTargets.Count > 1)
					{
						throw new SheetSchemaBuilderException($"시트 '{table.Name}'의 컬럼 '{column.FieldName}'이 여러 시트의 Key와 일치해 참조 대상을 결정할 수 없습니다: {string.Join(", ", otherTargets.Select(t => t.Name))}");
					}

					SetReference(table, column, otherTargets[0]);
				}

				table.Hash = BuildHash(table.Name, table.Columns);
			}
		}

		private static string BuildHash(string sheetName, IReadOnlyList<ColumnSpec> columns)
		{
			string hashSource = sheetName + string.Concat(columns.Select(c => $"{c.RawType}:{c.FieldName}:{c.Type}:{c.RefSheetName}:{c.EnumName}"));
			return HashUtility.Sha256Hex(hashSource);
		}

		private static void ResolveExplicitReference(SheetTable table, ColumnSpec column, Dictionary<string, SheetTable> byName, Dictionary<string, List<SheetTable>> byKeyFieldName)
		{
			if (byName.TryGetValue(column.RefSheetName, out SheetTable? target))
			{
				SetReference(table, column, target);
				return;
			}

			if (byKeyFieldName.TryGetValue(Identifier.Sanitize(column.RefSheetName), out List<SheetTable>? targets))
			{
				if (targets.Count == 1)
				{
					SetReference(table, column, targets[0]);
					return;
				}

				throw new SheetSchemaBuilderException($"시트 '{table.Name}'의 컬럼 '{column.FieldName}'이 참조하는 Key '{column.RefSheetName}'가 여러 시트에 있습니다: {string.Join(", ", targets.Select(t => t.Name))}");
			}

			throw new SheetSchemaBuilderException($"시트 '{table.Name}'의 컬럼 '{column.FieldName}'이 존재하지 않는 시트 또는 Key를 참조합니다: 'ref:{column.RefSheetName}'");
		}

		private static void SetReference(SheetTable table, ColumnSpec column, SheetTable target)
		{
			if (AreReferenceTypesCompatible(column, target.KeyColumn) == false)
			{
				throw new SheetSchemaBuilderException($"시트 '{table.Name}'의 컬럼 '{column.FieldName}' 타입({column.RawType})이 참조 대상 시트 '{target.Name}'의 Key 타입({target.KeyColumn.RawType})과 일치하지 않습니다.");
			}

			column.Type = EColumnType.Ref;
			column.RefSheetName = target.Name;
		}

		private static bool AreReferenceTypesCompatible(ColumnSpec column, ColumnSpec targetKey)
		{
			if (column.Type == EColumnType.Ref)
			{
				return true;
			}

			if (column.Type != targetKey.Type)
			{
				return false;
			}

			if (column.Type == EColumnType.Enum)
			{
				return string.Equals(column.EnumName, targetKey.EnumName, StringComparison.Ordinal);
			}

			return true;
		}

		private static bool IsSupportedKeyKind(EColumnType type)
		{
			return type is EColumnType.Int or EColumnType.Long or EColumnType.Float or EColumnType.Double or EColumnType.String or EColumnType.Enum;
		}
	}
}
