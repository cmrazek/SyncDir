using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SyncDirCmd
{
	static class Util
	{
		public static string FormatSize(this long size)
		{
			double sizef = size;

			if (sizef < 1024.0) return string.Format("{0:N0} B", sizef);
			sizef /= 1024.0;
			if (sizef < 1024.0) return string.Format("{0:N1} KB", sizef);
			sizef /= 1024.0;
			if (sizef < 1024.0) return string.Format("{0:N1} MB", sizef);
			sizef /= 1024.0;
			if (sizef < 1024.0) return string.Format("{0:N1} GB", sizef);
			sizef /= 1024.0;
			return string.Format("{0:N1} TB", sizef);
		}
	}
}
