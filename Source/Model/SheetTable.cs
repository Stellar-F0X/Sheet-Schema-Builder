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

		/// <summary>(pk)로 명시된 기본 키 컬럼. PK가 없는 시트에서는 null이다.</summary>
		public ColumnSpec? PrimaryKeyColumn
		{
			get { return Columns.FirstOrDefault(c => c.Role == EColumnRole.PrimaryKey); }
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

			List<ColumnSpec> primaryKeys = columns.Where(c => c.Role == EColumnRole.PrimaryKey).ToList();
			if (primaryKeys.Count > 1)
			{
				throw new SheetSchemaBuilderException($"시트 '{raw.Name}'에는 PK를 하나만 지정할 수 있습니다: {string.Join(", ", primaryKeys.Select(c => c.FieldName))}");
			}

			if (primaryKeys.Count == 1 && IsSupportedKeyKind(primaryKeys[0].Type) == false)
			{
				throw new SheetSchemaBuilderException($"시트 '{raw.Name}'의 PK '{primaryKeys[0].FieldName}'는 int/long/float/double/string/enum 타입이어야 합니다. (현재: {primaryKeys[0].RawType})");
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

		/// <summary>FK 컬럼을 같은 필드명의 PK 컬럼과 연결하고 관계를 검증한다.</summary>
		public static void ResolveReferences(IReadOnlyList<SheetTable> tables)
		{
			Dictionary<string, List<(SheetTable Table, ColumnSpec Column)>> primaryKeysByField = new(StringComparer.OrdinalIgnoreCase);

			foreach (SheetTable table in tables)
			{
				ColumnSpec? primaryKey = table.PrimaryKeyColumn;
				if (primaryKey == null)
				{
					continue;
				}

				if (primaryKeysByField.TryGetValue(primaryKey.FieldName, out List<(SheetTable Table, ColumnSpec Column)>? entries) == false)
				{
					entries = new List<(SheetTable Table, ColumnSpec Column)>();
					primaryKeysByField[primaryKey.FieldName] = entries;
				}

				entries.Add((table, primaryKey));
			}

			foreach (SheetTable table in tables)
			{
				foreach (ColumnSpec foreignKey in table.Columns.Where(c => c.Role == EColumnRole.ForeignKey))
				{
					if (primaryKeysByField.TryGetValue(foreignKey.FieldName, out List<(SheetTable Table, ColumnSpec Column)>? targets) == false)
					{
						throw new SheetSchemaBuilderException($"시트 '{table.Name}'의 FK '{foreignKey.FieldName}'와 같은 이름의 PK를 찾을 수 없습니다.");
					}

					if (targets.Count > 1)
					{
						throw new SheetSchemaBuilderException($"시트 '{table.Name}'의 FK '{foreignKey.FieldName}'가 여러 시트의 PK와 일치합니다: {string.Join(", ", targets.Select(t => t.Table.Name))}");
					}

					(SheetTable targetTable, ColumnSpec targetKey) = targets[0];
					if (AreReferenceTypesCompatible(foreignKey, targetKey) == false)
					{
						throw new SheetSchemaBuilderException($"시트 '{table.Name}'의 FK '{foreignKey.FieldName}' 타입({foreignKey.RawType})이 대상 시트 '{targetTable.Name}'의 PK 타입({targetKey.RawType})과 일치하지 않습니다.");
					}

					foreignKey.RefSheetName = targetTable.Name;
				}

				table.Hash = BuildHash(table.Name, table.Columns);
			}
		}

		private static string BuildHash(string sheetName, IReadOnlyList<ColumnSpec> columns)
		{
			string hashSource = sheetName + string.Concat(columns.Select(c => $"{c.RawType}:{c.FieldName}:{c.Type}:{c.Role}:{c.RefSheetName}:{c.EnumName}"));
			return HashUtility.Sha256Hex(hashSource);
		}

		private static bool AreReferenceTypesCompatible(ColumnSpec foreignKey, ColumnSpec primaryKey)
		{
			if (foreignKey.Type != primaryKey.Type)
			{
				return false;
			}

			return foreignKey.Type != EColumnType.Enum || string.Equals(foreignKey.EnumName, primaryKey.EnumName, StringComparison.Ordinal);
		}

		private static bool IsSupportedKeyKind(EColumnType type)
		{
			return type is EColumnType.Int or EColumnType.Long or EColumnType.Float or EColumnType.Double or EColumnType.String or EColumnType.Enum;
		}
	}
}
