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
			_tablesByName = tables.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
			_tables = tables;
			_enums = enums;
		}

		
		/// <summary>시트별 Key 컬럼 값 집합. ref 무결성 검증에 사용한다.</summary>
		private readonly Dictionary<string, HashSet<string>> _keySets = new(StringComparer.OrdinalIgnoreCase); 

		private readonly Dictionary<string, SheetTable> _tablesByName;

		private readonly IReadOnlyList<SheetTable> _tables;

		private readonly EnumRegistry _enums;


		/// <summary>전체 시트 데이터를 Json 파일로 저장한다.</summary>
		public void Export(string outputPath)
		{
			CollectKeys();
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


		/// <summary>키 중복을 검증하며 시트별 키 집합을 만든다.</summary>
		private void CollectKeys()
		{
			foreach (SheetTable table in _tables)
			{
				_keySets[table.Name] = new HashSet<string>(StringComparer.Ordinal); 
				HashSet<string> keys = _keySets[table.Name]; 

				for (int r = 0; r < table.Rows.Count; r++)
				{
					string key = table.Rows[r][0];

					if (string.IsNullOrWhiteSpace(key))
					{
						throw new SheetSchemaBuilderException($"시트 '{table.Name}' {r + 3}행: 키가 설정되어있지 않습니다.");
					}

					if (keys.Add(key) == false)
					{
						throw new SheetSchemaBuilderException($"시트 '{table.Name}' {r + 3}행: 키 '{key}'가 중복되었습니다.");
					}
				}
			}
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

				case EColumnType.Ref:
				{
					if (WriteRefCell(writer, column, cell))
					{
						return;
					}
					else
					{
						throw new SheetSchemaBuilderException($"시트 '{table.Name}' {rowIndex + 3}행 '{column.FieldName}' 컬럼: '{cell}' 값을 {nameof(EColumnType.Ref)}(으)로 해석할 수 없습니다.");
					}
				}

				default:
				{
					throw new SheetSchemaBuilderException($"지원하지 않는 컬럼 타입입니다: {column.Type}");
				}
			}
		}


		/// <summary> ref 컬럼: 대상 시트 Key 타입으로 기록하고, 대상 시트에 실제 존재하는 키인지 검증한다. </summary>
		private bool WriteRefCell(Utf8JsonWriter writer, ColumnSpec column, string cell)
		{
			SheetTable target = _tablesByName[column.RefSheetName];

			if (_keySets[target.Name].Contains(cell) == false) 
			{
				return false; 
			}

			switch (target.KeyColumn.Type)
			{
				case EColumnType.Int:
				{
					if (string.IsNullOrWhiteSpace(cell) == false && int.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intKey))
					{
						writer.WriteNumberValue(intKey);
						return true;
					}

					break;
				}

				case EColumnType.Long:
				{
					if (string.IsNullOrWhiteSpace(cell) == false && long.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longKey))
					{
						writer.WriteNumberValue(longKey);
						return true;
					}

					break;
				}

				case EColumnType.Float:
				{
					if (string.IsNullOrWhiteSpace(cell) == false && float.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatKey))
					{
						writer.WriteNumberValue(floatKey);
						return true;
					}

					break;
				}

				case EColumnType.Double:
				{
					if (string.IsNullOrWhiteSpace(cell) == false && double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleKey))
					{
						writer.WriteNumberValue(doubleKey);
						return true;
					}

					break;
				}

				case EColumnType.String:
				{
					writer.WriteStringValue(cell);
					return true;
				}

				case EColumnType.Enum:
				{
					writer.WriteNumberValue(_enums.GetValue(target.KeyColumn.EnumName, cell));
					return true;
				}

				default:
				{
					throw new SheetSchemaBuilderException($"ref 대상 시트 '{target.Name}'의 Key 타입을 지원하지 않습니다: {target.KeyColumn.Type}");
				}
			}

			return false;
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
