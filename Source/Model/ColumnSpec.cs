using SheetSchemaBuilder;

namespace DataBuilder.Model
{
	/// <summary>시트의 한 열(컬럼) 정의. 1행의 타입 문자열과 2행의 필드명으로 구성된다.</summary>
	public sealed class ColumnSpec
	{
		private readonly static Dictionary<string, EColumnType> _PrimitiveTypeMap = new()
		{
			["int"] = EColumnType.Int,
			["int32"] = EColumnType.Int,
			["long"] = EColumnType.Long,
			["int64"] = EColumnType.Long,
			["float"] = EColumnType.Float,
			["single"] = EColumnType.Float,
			["double"] = EColumnType.Double,
			["bool"] = EColumnType.Bool,
			["boolean"] = EColumnType.Bool,
			["string"] = EColumnType.String,
			["text"] = EColumnType.String,
		};

		public required string FieldName
		{
			get;
			init;
		}

		public required EColumnType Type
		{
			get;
			set;
		}

		/// <summary>Type == Enum일 때 enum 타입 이름.</summary>
		public string EnumName
		{
			get;
			init;
		} = string.Empty;

		/// <summary>Type == Ref일 때 참조 대상 시트 이름.</summary>
		public string RefSheetName
		{
			get;
			set;
		} = string.Empty;

		/// <summary>원본 타입 문자열 (예: "enum:ItemType").</summary>
		public required string RawType
		{
			get;
			init;
		}

		/// <summary>타입 문자열을 파싱한다. int/long/float/double/bool/string, enum:이름, ref:시트명을 지원한다.</summary>
		public static ColumnSpec Parse(string sheetName, int columnIndex, string typeText, string fieldName)
		{
			string raw = typeText.Trim();
			string lower = raw.ToLowerInvariant();
			string field = Identifier.Sanitize(fieldName);

			if (string.IsNullOrWhiteSpace(field))
			{
				throw new SheetSchemaBuilderException($"시트 '{sheetName}' {columnIndex + 1}번째 열의 필드명(2행)이 비어 있습니다.");
			}

			EColumnType? primitive = ParsePrimitiveOrNull(lower);

			if (primitive.HasValue)
			{
				return new ColumnSpec { FieldName = field, Type = primitive.Value, RawType = raw };
			}

			if (TryParseNamedType(raw, "enum", out string enumName))
			{
				string name = Identifier.EnsurePrefix(Identifier.Sanitize(enumName), "E");
				return new ColumnSpec { FieldName = field, Type = EColumnType.Enum, EnumName = name, RawType = raw };
			}

			if (TryParseNamedType(raw, "ref", out string refSheet))
			{
				return new ColumnSpec { FieldName = field, Type = EColumnType.Ref, RefSheetName = refSheet, RawType = raw };
			}
			else
			{
				throw new SheetSchemaBuilderException($"시트 '{sheetName}' {columnIndex + 1}번째 열의 타입(1행)을 해석할 수 없습니다: '{raw}' ");
			}
		}
		

		/// <summary>기본 타입 문자열을 컬럼 타입으로 변환한다.</summary>
		private static EColumnType? ParsePrimitiveOrNull(string lower)
		{
			if (_PrimitiveTypeMap.TryGetValue(lower, out EColumnType kind))
			{
				return kind;
			}
			else
			{
				return null;
			}
		}

		
		/// <summary>"enum:Name" 또는 "enum(Name)" 형태를 파싱한다.</summary>
		private static bool TryParseNamedType(string raw, string keyword, out string name)
		{
			name = string.Empty;

			if (raw.StartsWith(keyword + ":", StringComparison.OrdinalIgnoreCase))
			{
				name = raw[(keyword.Length + 1)..].Trim();
				return name.Length > 0;
			}

			if (raw.StartsWith(keyword + "(", StringComparison.OrdinalIgnoreCase) && raw.EndsWith(')'))
			{
				name = raw[(keyword.Length + 1)..^1].Trim();
				return name.Length > 0;
			}

			return name.Length > 0;
		}
	}
}
