using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SyncDirCmd
{
	class ArgumentException : Exception
	{
		public ArgumentException(string message) : base(message)
		{
		}
	}
}
