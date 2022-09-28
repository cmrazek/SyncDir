using Newtonsoft.Json;

namespace SyncDirCmd
{
	public class ConfigFile
	{
		[JsonProperty("directories")]
		public SyncDir[] Directories { get; set; }
	}
}
