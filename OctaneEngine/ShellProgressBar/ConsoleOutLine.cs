using System.Diagnostics.CodeAnalysis;

namespace OctaneEngineCore.ShellProgressBar
{
	[ExcludeFromCodeCoverage]
	public struct ConsoleOutLine
	{
		public bool Error { get; }
		public string Line { get; }

		public ConsoleOutLine(string line, bool error = false)
		{
			Error = error;
			Line = line;
		}
	}
}
