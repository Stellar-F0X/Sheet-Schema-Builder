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
			init;
		}

		/// <summary>타입 뒤의 (pk)/(fk)/(dk) 표식으로 지정한 컬럼 역할.</summary>
		public EColumnRole Role
		{
			get;
			init;
		}

		/// <summary>Type == Enum일 때 enum 타입 이름.</summary>
		public string EnumName
		{
			get;
			init;
		} = string.Empty;

		/// <summary>Role이 ForeignKey/DataKey일 때 같은 필드명의 PK를 가진 대상 시트 이름.</summary>
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

		/// <summary>타입 문자열을 파싱한다. 기본 타입과 enum:이름, 선택적인 (pk)/(fk)/(dk) 접미사를 지원한다.</summary>
		public static ColumnSpec Parse(string sheetName, int columnIndex, string typeText, string fieldName)
		{
			string raw = typeText.Trim();
			string typePart = raw;
			EColumnRole role = ParseRoleSuffix(ref typePart);
			string lower = typePart.ToLowerInvariant();
			string field = Identifier.Sanitize(fieldName);

			if (string.IsNullOrWhiteSpace(field))
			{
				throw new SheetSchemaBuilderException($"시트 '{sheetName}' {columnIndex + 1}번째 열의 필드명(2행)이 비어 있습니다.");
			}

			EColumnType? primitive = ParsePrimitiveOrNull(lower);

			if (primitive.HasValue)
			{
				return new ColumnSpec { FieldName = field, Type = primitive.Value, Role = role, RawType = raw };
			}

			if (TryParseNamedType(typePart, "enum", out string enumName))
			{
				string name = Identifier.EnsurePrefix(Identifier.Sanitize(enumName), "E");
				return new ColumnSpec { FieldName = field, Type = EColumnType.Enum, Role = role, EnumName = name, RawType = raw };
			}

			throw new SheetSchemaBuilderException($"시트 '{sheetName}' {columnIndex + 1}번째 열의 타입(1행)을 해석할 수 없습니다: '{raw}' ");
		}


		/// <summary>타입 문자열 끝의 (pk)/(fk)/(dk)를 분리한다.</summary>
		private static EColumnRole ParseRoleSuffix(ref string typeText)
		{
			string trimmed = typeText.Trim();

			if (trimmed.EndsWith("(pk)", StringComparison.OrdinalIgnoreCase))
			{
				typeText = trimmed[..^4].Trim();
				return EColumnRole.PrimaryKey;
			}

			if (trimmed.EndsWith("(fk)", StringComparison.OrdinalIgnoreCase))
			{
				typeText = trimmed[..^4].Trim();
				return EColumnRole.ForeignKey;
			}

			if (trimmed.EndsWith("(dk)", StringComparison.OrdinalIgnoreCase))
			{
				typeText = trimmed[..^4].Trim();
				return EColumnRole.DataKey;
			}

			typeText = trimmed;
			return EColumnRole.None;
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
