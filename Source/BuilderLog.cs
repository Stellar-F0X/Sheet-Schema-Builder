namespace DataBuilder
{
	/// <summary>비동기 실행 흐름별 로그 출력 대상을 보관한다.</summary>
	internal static class BuilderLog
	{
		private static readonly AsyncLocal<TextWriter?> _output = new AsyncLocal<TextWriter?>();
		private static readonly AsyncLocal<TextWriter?> _error = new AsyncLocal<TextWriter?>();

		public static TextWriter Output
		{
			get { return _output.Value ?? Console.Out; }
		}

		public static TextWriter Error
		{
			get { return _error.Value ?? Console.Error; }
		}

		public static IDisposable Push(TextWriter output, TextWriter error)
		{
			return new Scope(output, error);
		}

		private sealed class Scope : IDisposable
		{
			private readonly TextWriter? _previousOutput;
			private readonly TextWriter? _previousError;

			public Scope(TextWriter output, TextWriter error)
			{
				_previousOutput = BuilderLog._output.Value;
				_previousError = BuilderLog._error.Value;
				BuilderLog._output.Value = output;
				BuilderLog._error.Value = error;
			}

			public void Dispose()
			{
				BuilderLog._output.Value = _previousOutput;
				BuilderLog._error.Value = _previousError;
			}
		}
	}
}
