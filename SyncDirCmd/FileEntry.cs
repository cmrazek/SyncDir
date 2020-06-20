using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncDirCmd
{
	class FileEntry
	{
		public long FileEntryId { get; private set; }
		public long BasePathId { get; private set; }
		public string RelativePathName { get; set; }
		public DateTime Modified { get; set; }
		public long Size { get; set; }
		public bool Directory { get; set; }

		public FileEntry(long fileEntryId, long basePathId, string relPathName, DateTime modified, long size, bool directory)
		{
			FileEntryId = fileEntryId;
			BasePathId = basePathId;
			RelativePathName = relPathName;
			Modified = modified;
			Size = size;
			Directory = directory;
		}
	}
}
