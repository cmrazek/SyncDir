using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SyncDirCmd
{
	public partial class Sync
	{
		private List<Regex> _ignoreRx;

		public bool IgnorePath(string path)
		{
			if (_ignoreRx == null)
			{
				_ignoreRx = new List<Regex>();
				if (this.ignore != null)
				{
					foreach (var pattern in this.ignore)
					{
						try
						{
							_ignoreRx.Add(new Regex(pattern, RegexOptions.IgnoreCase));
						}
						catch (Exception ex)
						{
							Program.Instance.LogError(ex, string.Format("Ignore pattern '{0}' is not a valid regular expression.", pattern));
						}
					}
				}
			}

			foreach (var rx in _ignoreRx)
			{
				if (rx.IsMatch(path)) return true;
			}

			return false;
		}
	}
}
