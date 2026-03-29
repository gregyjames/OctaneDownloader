using System;

namespace OctaneEngineCore.ShellProgressBar
{
	public interface IProgressBar : IDisposable
	{
		ChildProgressBar Spawn(long maxTicks, string message, ProgressBarOptions options = null);

		void Tick(string message = null);
		void Tick(int newTickCount, string message = null);

		long MaxTicks { get; set; }
		string Message { get; set; }

		double Percentage { get; }
		long CurrentTick { get; }

		ConsoleColor ForegroundColor { get; set; }

		/// <summary>
		/// This writes a new line above the progress bar to <see cref="Console.Out"/>.
		/// Use <see cref="Message"/> to update the message inside the progress bar
		/// </summary>
		void WriteLine(string message);

		/// <summary> This writes a new line above the progress bar to <see cref="Console.Error"/></summary>
		void WriteErrorLine(string message);

		IProgress<T> AsProgress<T>(Func<T, string> message = null, Func<T, double?> percentage = null);
	}
}
