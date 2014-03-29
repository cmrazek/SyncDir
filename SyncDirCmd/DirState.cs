using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SyncDirCmd
{
	class DirState
	{
		public Sync Sync { get; set; }
		public FileDb LeftDb { get; set; }
		public FileDb RightDb { get; set; }
		public string RelPath { get; set; }
		public string LeftAbsPath { get; set; }
		public string RightAbsPath { get; set; }

		public DirState(Sync sync, FileDb leftDb, FileDb rightDb, string relPath)
		{
			if (sync == null || leftDb == null || rightDb == null) throw new ArgumentNullException();

			Sync = sync;
			LeftDb = leftDb;
			RightDb = rightDb;
			RelPath = relPath;

			if (!string.IsNullOrEmpty(RelPath))
			{
				LeftAbsPath = Path.Combine(LeftDb.BasePath, RelPath);
				RightAbsPath = Path.Combine(RightDb.BasePath, RelPath);
			}
			else
			{
				LeftAbsPath = LeftDb.BasePath;
				RightAbsPath = RightDb.BasePath;
			}
		}

		public DirState(DirState parent, string subDir)
		{
			if (parent == null || string.IsNullOrEmpty(subDir)) throw new ArgumentNullException();

			Sync = parent.Sync;
			LeftDb = parent.LeftDb;
			RightDb = parent.RightDb;
			RelPath = !string.IsNullOrEmpty(parent.RelPath) ? Path.Combine(parent.RelPath, subDir) : subDir;

			LeftAbsPath = Path.Combine(LeftDb.BasePath, RelPath);
			RightAbsPath = Path.Combine(RightDb.BasePath, RelPath);
		}

		public string LeftBasePath
		{
			get { return LeftDb.BasePath; }
		}

		public string RightBasePath
		{
			get { return RightDb.BasePath; }
		}

		public string GetLeftAbsFileName(string fileName)
		{
			return Path.Combine(LeftAbsPath, fileName);
		}

		public string GetRightAbsFileName(string fileName)
		{
			return Path.Combine(RightAbsPath, fileName);
		}

		public string GetRelFileName(string fileName)
		{
			return !string.IsNullOrEmpty(RelPath) ? Path.Combine(RelPath, fileName) : fileName;
		}
	}
}
