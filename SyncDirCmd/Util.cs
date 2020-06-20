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
			double neg = 1.0;
			if (sizef < 0)
			{
				sizef *= -1.0;
				neg = -1.0;
			}

			if (sizef < 1024.0) return string.Format("{0:N0} B", sizef * neg);
			sizef /= 1024.0;
			if (sizef < 1024.0) return string.Format("{0:N1} KB", sizef * neg);
			sizef /= 1024.0;
			if (sizef < 1024.0) return string.Format("{0:N1} MB", sizef * neg);
			sizef /= 1024.0;
			if (sizef < 1024.0) return string.Format("{0:N1} GB", sizef * neg);
			sizef /= 1024.0;
			return string.Format("{0:N1} TB", sizef * neg);
		}
	}
}
