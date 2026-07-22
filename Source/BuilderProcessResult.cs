namespace DataBuilder
{
	/// <summary>빌더 실행 결과와 호출자가 표시할 로그를 함께 반환한다.</summary>
	public sealed class BuilderProcessResult
	{
		public BuilderProcessResult(int exitCode, string output, string error)
		{
			ExitCode = exitCode;
			Output = output;
			Error = error;
		}

		public int ExitCode
		{
			get;
		}

		public string Output
		{
			get;
		}

		public string Error
		{
			get;
		}

		public string CombinedLog
		{
			get
			{
				if (string.IsNullOrWhiteSpace(Output))
				{
					return Error;
				}

				if (string.IsNullOrWhiteSpace(Error))
				{
					return Output;
				}

				return Output.TrimEnd() + Environment.NewLine + Error;
			}
		}
	}
}
