using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncDirCmd
{
	public class ConfigFile
	{
		[JsonProperty("directories")]
		public SyncDir[] Directories { get; set; }
	}
}
