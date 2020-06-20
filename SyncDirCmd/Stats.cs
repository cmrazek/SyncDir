using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncDirCmd
{
	class Stats
	{
		public int NumFilesAnalyzed = 0;
		public int NumDirsAnalyzed = 0;
		public int NumFilesCopied = 0;
		public int NumFilesDeleted = 0;
		public int NumDirsCreated = 0;
		public int NumDirsDeleted = 0;
		public long BytesCopied = 0;
		public long BytesDeleted = 0;
		public long BytesNet = 0;
	}
}
