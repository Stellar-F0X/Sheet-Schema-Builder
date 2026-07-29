using System.Globalization;
using System.Text.Json;
using DataBuilder.Model;
using SheetSchemaBuilder;

namespace DataBuilder.Export
{
	/// <summary>
	/// 모든 시트 데이터를 하나의 Json으로 저장한다.
	/// 출력 형식은 생성된 SheetDataBase 클래스(FromJson)로 그대로 읽을 수 있다:
	/// { "시트이름": [ { "필드": 값, ... }, ... ], ... }
	/// (enum은 선언 순서 인덱스의 정수로 기록되어 Unity JsonUtility와도 호환된다)
	/// </summary>
	public sealed class JsonExporter
	{
		/// <summary>Json 내보내기에 필요한 시트 모델과 enum 정보를 보관한다.</summary>
		public JsonExporter(IReadOnlyList<SheetTable> tables, EnumRegistry enums)
		{
			_tables = tables;
			_enums = enums;
			_tablesByName = tables.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
		}


		private readonly IReadOnlyList<SheetTable> _tables;

		private readonly EnumRegistry _enums;

		private readonly Dictionary<string, SheetTable> _tablesByName;

		private readonly Dictionary<string, HashSet<string>> _primaryKeySets = new(StringComparer.OrdinalIgnoreCase);


		/// <summary>전체 시트 데이터를 Json 파일로 저장한다.</summary>
		public void Export(string outputPath)
		{
			ValidateSchemaData();
			Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

			using FileStream stream = File.Create(outputPath);
			using Utf8JsonWriter writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

			writer.WriteStartObject();

			foreach (SheetTable table in _tables)
			{
				writer.WritePropertyName(Identifier.Sanitize(table.Name));
				writer.WriteStartArray();

				for (int r = 0; r < table.Rows.Count; r++)
				{
					writer.WriteStartObject();

					for (int c = 0; c < table.Columns.Count; c++)
					{
						WriteCell(writer, table, r, c);
					}

					writer.WriteEndObject();
				}

				writer.WriteEndArray();
			}

			writer.WriteEndObject();
		}


		/// <summary>PK의 비어 있음/중복과 FK 대상 값의 존재 여부를 검증한다.</summary>
		private void ValidateSchemaData()
		{
			CollectPrimaryKeys();
			ValidateForeignKeys();
		}


		/// <summary>시트별 PK를 실제 직렬화 타입 기준으로 정규화하여 수집한다.</summary>
		private void CollectPrimaryKeys()
		{
			foreach (SheetTable table in _tables)
			{
				ColumnSpec? primaryKey = table.PrimaryKeyColumn;
				if (primaryKey == null)
				{
					continue;
				}

				int columnIndex = IndexOfColumn(table, primaryKey);
				HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
				_primaryKeySets[table.Name] = keys;

				for (int r = 0; r < table.Rows.Count; r++)
				{
					string cell = table.Rows[r][columnIndex];
					string key = NormalizeKey(primaryKey, cell, table.Name, r);

					if (keys.Add(key) == false)
					{
						throw new SheetSchemaBuilderException($"시트 '{table.Name}' {r + 3}행: PK '{primaryKey.FieldName}' 값 '{cell}'이 중복되었습니다.");
					}
				}
			}
		}


		/// <summary>모든 FK 값이 연결된 대상 시트의 PK에 존재하는지 검증한다.</summary>
		private void ValidateForeignKeys()
		{
			foreach (SheetTable table in _tables)
			{
				for (int c = 0; c < table.Columns.Count; c++)
				{
					ColumnSpec foreignKey = table.Columns[c];
					if (foreignKey.Role != EColumnRole.ForeignKey)
					{
						continue;
					}

					SheetTable target = _tablesByName[foreignKey.RefSheetName];
					HashSet<string> targetKeys = _primaryKeySets[target.Name];

					for (int r = 0; r < table.Rows.Count; r++)
					{
						string cell = table.Rows[r][c];
						string key = NormalizeKey(foreignKey, cell, table.Name, r);
						if (targetKeys.Contains(key) == false)
						{
							throw new SheetSchemaBuilderException($"시트 '{table.Name}' {r + 3}행의 FK '{foreignKey.FieldName}' 값 '{cell}'이 대상 시트 '{target.Name}'의 PK에 없습니다.");
						}
					}
				}
			}
		}


		private static int IndexOfColumn(SheetTable table, ColumnSpec column)
		{
			for (int i = 0; i < table.Columns.Count; i++)
			{
				if (ReferenceEquals(table.Columns[i], column))
				{
					return i;
				}
			}

			throw new InvalidOperationException($"시트 '{table.Name}'에서 컬럼 '{column.FieldName}'을 찾지 못했습니다.");
		}


		/// <summary>PK/FK 비교가 생성 코드의 키 비교와 같도록 셀 값을 선언 타입으로 정규화한다.</summary>
		private string NormalizeKey(ColumnSpec column, string cell, string sheetName, int rowIndex)
		{
			if (string.IsNullOrWhiteSpace(cell))
			{
				throw new SheetSchemaBuilderException($"시트 '{sheetName}' {rowIndex + 3}행: '{column.FieldName}' 키 값이 비어 있습니다.");
			}

			switch (column.Type)
			{
				case EColumnType.Int:
					if (int.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
					{
						return intValue.ToString(CultureInfo.InvariantCulture);
					}
					break;

				case EColumnType.Long:
					if (long.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue))
					{
						return longValue.ToString(CultureInfo.InvariantCulture);
					}
					break;

				case EColumnType.Float:
					if (float.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue) &&
					    float.IsNaN(floatValue) == false && float.IsInfinity(floatValue) == false)
					{
						return floatValue.ToString("R", CultureInfo.InvariantCulture);
					}
					break;

				case EColumnType.Double:
					if (double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue) &&
					    double.IsNaN(doubleValue) == false && double.IsInfinity(doubleValue) == false)
					{
						return doubleValue.ToString("R", CultureInfo.InvariantCulture);
					}
					break;

				case EColumnType.String:
					return cell;

				case EColumnType.Enum:
					return _enums.GetValue(column.EnumName, cell).ToString(CultureInfo.InvariantCulture);
			}

			throw new SheetSchemaBuilderException($"시트 '{sheetName}' {rowIndex + 3}행의 키 '{column.FieldName}' 값 '{cell}'을 타입 '{column.RawType}'(으)로 해석할 수 없습니다.");
		}


		/// <summary>시트 셀 하나를 Json 속성으로 기록한다.</summary>
		private void WriteCell(Utf8JsonWriter writer, SheetTable table, int rowIndex, int columnIndex)
		{
			ColumnSpec column = table.Columns[columnIndex];
			string cell = table.Rows[rowIndex][columnIndex];
			writer.WritePropertyName(column.FieldName);

			switch (column.Type)
			{
				case EColumnType.Int:
				{
					if (string.IsNullOrWhiteSpace(cell) == false && int.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
					{
						writer.WriteNumberValue(value);
					}
					else
					{
						throw new SheetSchemaBuilderException($"시트 '{table.Name}' {rowIndex + 3}행 '{column.FieldName}' 컬럼: '{cell}' 값을 {typeof(Int32)}(으)로 해석할 수 없습니다.");
					}

					break;
				}

				case EColumnType.Long:
				{
					if (string.IsNullOrWhiteSpace(cell) == false && long.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
					{
						writer.WriteNumberValue(value);
					}
					else
					{
						throw new SheetSchemaBuilderException($"시트 '{table.Name}' {rowIndex + 3}행 '{column.FieldName}' 컬럼: '{cell}' 값을 {typeof(Int64)}(으)로 해석할 수 없습니다.");
					}

					break;
				}

				case EColumnType.Float:
				{
					if (string.IsNullOrWhiteSpace(cell) == false && float.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
					{
						writer.WriteNumberValue(value);
					}
					else
					{
						throw new SheetSchemaBuilderException($"시트 '{table.Name}' {rowIndex + 3}행 '{column.FieldName}' 컬럼: '{cell}' 값을 {typeof(Single)}(으)로 해석할 수 없습니다.");
					}

					break;
				}

				case EColumnType.Double:
				{
					if (string.IsNullOrWhiteSpace(cell) == false && double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
					{
						writer.WriteNumberValue(value);
					}
					else
					{
						throw new SheetSchemaBuilderException($"시트 '{table.Name}' {rowIndex + 3}행 '{column.FieldName}' 컬럼: '{cell}' 값을 {typeof(Double)}(으)로 해석할 수 없습니다.");
					}

					break;
				}

				case EColumnType.Bool:
				{
					writer.WriteBooleanValue(ParseBool(cell));
					break;
				}

				case EColumnType.String:
				{
					writer.WriteStringValue(cell);
					break;
				}

				case EColumnType.Enum:
				{
					writer.WriteNumberValue(_enums.GetValue(column.EnumName, cell));
					break;
				}

				default:
				{
					throw new SheetSchemaBuilderException($"지원하지 않는 컬럼 타입입니다: {column.Type}");
				}
			}
		}


		/// <summary>문자열 셀을 bool 값으로 변환한다.</summary>
		private static bool ParseBool(string cell)
		{
			if (string.IsNullOrWhiteSpace(cell))
			{
				return false;
			}
			
			string trimCell = cell.Trim();
			
			switch (trimCell.ToLowerInvariant())
			{
				case "true":
				case "yes":
				case "o":
				{
					return true;
				}

				case "false":
				case "no":
				case "x":
				case "":
				{
					return false;
				}
			}

			throw new SheetSchemaBuilderException($"시트의 값 '{cell}'을 {typeof(bool)}로 변환할 수 없습니다. ");
		}
	}
}
