using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SyncDirCmd
{
	class DirState
	{
		public FileDb FileDb { get; set; }
		public string RelPath { get; set; }
		public string LeftAbsPath { get; set; }
		public string RightAbsPath { get; set; }

		public DirState(FileDb fileDb, string relPath)
		{
			if (fileDb == null) throw new ArgumentNullException();

			FileDb = fileDb;
			RelPath = relPath;

			if (!string.IsNullOrEmpty(RelPath))
			{
				LeftAbsPath = Path.Combine(FileDb.LeftBasePath, RelPath);
				RightAbsPath = Path.Combine(FileDb.RightBasePath, RelPath);
			}
			else
			{
				LeftAbsPath = FileDb.LeftBasePath;
				RightAbsPath = FileDb.RightBasePath;
			}
		}

		public DirState(DirState parent, string subDir)
		{
			if (parent == null || string.IsNullOrEmpty(subDir)) throw new ArgumentNullException();

			FileDb = parent.FileDb;
			RelPath = !string.IsNullOrEmpty(parent.RelPath) ? Path.Combine(parent.RelPath, subDir) : subDir;

			LeftAbsPath = Path.Combine(FileDb.LeftBasePath, RelPath);
			RightAbsPath = Path.Combine(FileDb.RightBasePath, RelPath);
		}

		public string LeftBasePath
		{
			get { return FileDb.LeftBasePath; }
		}

		public string RightBasePath
		{
			get { return FileDb.RightBasePath; }
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
