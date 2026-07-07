namespace SheetSchemaBuilder
{
	/// <summary>사용자에게 그대로 보여줄 수 있는, 원인이 명확한 오류.</summary>
	public sealed class SheetSchemaBuilderException : Exception
	{
		/// <summary>오류 메시지로 예외를 생성한다.</summary>
		public SheetSchemaBuilderException(string message) : base(message) {}

		/// <summary>오류 메시지와 내부 예외로 예외를 생성한다.</summary>
		public SheetSchemaBuilderException(string message, Exception inner) : base(message, inner) {}
	}
}