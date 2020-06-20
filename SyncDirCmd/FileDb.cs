using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SyncDirCmd
{
	class FileDb : IDisposable
	{
		private string _basePath = "";
		private long _basePathId;
		private Database _db;

		public FileDb(Database db, string basePath)
		{
			_db = db ?? throw new ArgumentNullException(nameof(db));
			_basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
			_basePathId = _db.GetBasePathId(basePath);
		}

		public void Dispose()
		{
			_db = null;
		}

		public string BasePath
		{
			get { return _basePath; }
		}

		public void UpdateFile(string relPathName)
		{
			if (string.IsNullOrEmpty(relPathName)) throw new ArgumentNullException(nameof(relPathName));

			var absFileName = Path.Combine(_basePath, relPathName);
			if (File.Exists(absFileName))
			{
				var fi = new FileInfo(absFileName);
				var modified = fi.LastWriteTime;
				var size = fi.Length;

				_db.UpdateFile(_basePathId, relPathName, modified, size, false);
			}
			else
			{
				_db.RemoveFile(_basePathId, relPathName);
			}
		}

		public void UpdateDirectory(string relPathName)
		{
			var absPath = Path.Combine(_basePath, relPathName);
			if (Directory.Exists(absPath))
			{
				var fi = new FileInfo(absPath);
				var modified = fi.LastWriteTime;

				_db.UpdateFile(_basePathId, relPathName, modified, 0, true);
			}
			else
			{
				_db.RemoveFile(_basePathId, relPathName);
			}
		}

		public bool HasFileChanged(string relPathName, FileInfo fi, ref string reason)
		{
			if (string.IsNullOrEmpty(relPathName)) throw new ArgumentNullException(nameof(relPathName));

			var entry = _db.GetFile(_basePathId, relPathName);
			if (entry != null)
			{
				if (entry.Size != fi.Length)
				{
					reason = "size changed in db";
					return true;
				}

				if (!FileModifiedClose(entry.Modified, fi.LastWriteTime))
				{
					reason = "modified date changed in db";
					return true;
				}

				return false;
			}
			else
			{
				return false;
			}
		}

		public void DeleteFile(string relPathName)
		{
			if (string.IsNullOrEmpty(relPathName)) throw new ArgumentNullException(nameof(relPathName));

			_db.RemoveFile(_basePathId, relPathName);
		}

		public bool FileExists(string relPathName)
		{
			if (string.IsNullOrEmpty(relPathName)) throw new ArgumentNullException(nameof(relPathName));

			var entry = _db.GetFile(_basePathId, relPathName);
			return entry != null && entry.Directory == false;
		}

		public bool DirectoryExists(string relPathName)
		{
			var entry = _db.GetFile(_basePathId, relPathName);
			return entry != null && entry.Directory == true;
		}

		public void DeleteDirectory(string relDirPath)
		{
			if (string.IsNullOrEmpty(relDirPath)) throw new ArgumentNullException(nameof(relDirPath));

			if (string.IsNullOrEmpty(relDirPath))
			{
				// Delete all files in the database.
				_db.RemoveAllFiles(_basePathId);
			}
			else
			{
				_db.RemoveAllFilesInDir(_basePathId, relDirPath);
			}
		}

		public static bool FileModifiedClose(DateTime a, DateTime b)
		{
			var span = a.Subtract(b);
			if (Math.Abs(span.TotalMinutes) > 1.0) return false;
			return true;
		}

		public static bool PathIsInDir(string testPathName, string dirPathName)
		{
			if (!testPathName.StartsWith(dirPathName, StringComparison.OrdinalIgnoreCase)) return false;

			if (testPathName.Length > dirPathName.Length)
			{
				var ch = testPathName[dirPathName.Length];
				if (ch == Path.DirectorySeparatorChar || ch == Path.AltDirectorySeparatorChar) return true;
				else return false;
			}
			else return true;
		}
	}
}
