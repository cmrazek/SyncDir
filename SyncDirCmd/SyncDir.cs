using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SyncDirCmd
{
	public class SyncDir
	{
		private List<Regex> _ignoreRx;

		[JsonProperty("left")]
		public string Left { get; set; }

		[JsonProperty("right")]
		public string Right { get; set; }

		[JsonProperty("master")]
		[JsonConverter(typeof(MasterJsonConverter))]
		public Master Master { get; set; }

		[JsonProperty("ignore")]
		public string[] Ignore { get; set; }

		public bool IgnorePath(string path)
		{
			if (_ignoreRx == null)
			{
				_ignoreRx = new List<Regex>();
				if (Ignore != null)
				{
					foreach (var pattern in Ignore)
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
