using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SyncDirCmd
{
	public class Cout
	{
		private static object _lock = new object();
		private static ConsoleColor _foreColor = NormalColor;

		public const ConsoleColor ErrorColor = ConsoleColor.Red;
		public const ConsoleColor WarningColor = ConsoleColor.Yellow;
		public const ConsoleColor NormalColor = ConsoleColor.Gray;
		public const ConsoleColor ImportantColor = ConsoleColor.Cyan;
		public const ConsoleColor UnimportantColor = ConsoleColor.DarkGray;

		public static ConsoleColor ForeColor
		{
			get { lock (_lock) { return _foreColor; } }
			set { lock (_lock) { _foreColor = value; } }
		}

		public static void Write(object obj)
		{
			lock (_lock)
			{
				Console.ForegroundColor = _foreColor;
				Console.Write(obj);
			}
		}

		public static void WriteLine(object obj)
		{
			lock (_lock)
			{
				Console.ForegroundColor = _foreColor;
				Console.WriteLine(obj);
			}
		}

		public static void Write(ConsoleColor color, object obj)
		{
			lock (_lock)
			{
				Console.ForegroundColor = color;
				Console.Write(obj);
			}
		}

		public static void WriteLine(ConsoleColor color, object obj)
		{
			lock (_lock)
			{
				Console.ForegroundColor = color;
				Console.WriteLine(obj);
			}
		}

		public static void WriteLine()
		{
			lock (_lock)
			{
				Console.ForegroundColor = _foreColor;
				Console.WriteLine();
			}
		}
		
	}
}
