using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SyncDirCmd
{
	public class FileDb
	{
		private string _basePath = "";
		private string _dbFileName = "";
		private Dictionary<string, FileEntry> _files = new Dictionary<string, FileEntry>();

		class FileEntry
		{
			public string PathName { get; set; }
			public DateTime? Modified { get; set; }
			public long Size { get; set; }
			public bool DeleteMe { get; set; }
            public bool Directory { get; set; }
		}

		public FileDb(string basePath)
		{
			if (string.IsNullOrWhiteSpace(basePath)) throw new ArgumentNullException();
			_basePath = basePath;
			FindDbFile();
		}

		private void FindDbFile()
		{
			var dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Res.AppNameIdent);
			if (!Directory.Exists(dataPath)) Directory.CreateDirectory(dataPath);

			var found = false;

			foreach (var fileName in Directory.GetFiles(dataPath, "*.dat"))
			{
				using (var sr = new StreamReader(fileName))
				{
					// First line of file contains the base path.
					var fileBasePath = sr.ReadLine();
					if (fileBasePath.Equals(_basePath, StringComparison.OrdinalIgnoreCase))
					{
						_dbFileName = fileName;
						LoadDbFile(sr);
						found = true;
						break;
					}
				}
			}

			if (!found)
			{
				_dbFileName = Path.Combine(dataPath, string.Format("{0}.dat", Guid.NewGuid().ToString()));
			}
		}

		public string BasePath
		{
			get { return _basePath; }
		}

		private void LoadDbFile(StreamReader sr)
		{
			while (!sr.EndOfStream)
			{
				var line = sr.ReadLine();
				if (line == null) continue;

				string fileName = null;
				DateTime? modified = null;
				long size = -1;
                bool dir = false;

				foreach (var rec in line.Split('|'))
				{
					var eqIndex = rec.IndexOf("=");
					if (eqIndex < 0) continue;

					var key = rec.Substring(0, eqIndex);
					var value = rec.Substring(eqIndex + 1);
					
					switch (key)
					{
						case "fn":
							fileName = value;
							break;
						case "mod":
							{
								DateTime dt;
								if (DateTime.TryParse(value, out dt)) modified = dt;
							}
							break;
						case "size":
							{
								long sz;
								if (long.TryParse(value, out sz))
								{
									size = sz;
								}
							}
							break;
                        case "dir":
                            bool.TryParse(value, out dir);
                            break;
					}
				}

				if (!string.IsNullOrEmpty(fileName))
				{
					_files[fileName.ToLower()] = new FileEntry
						{
							PathName = fileName,
							Modified = modified,
							Size = size,
                            Directory = dir
						};
				}
			}
		}

		public void SaveDbFile()
		{
			if (string.IsNullOrWhiteSpace(_dbFileName)) throw new InvalidOperationException("No database file name specified.");

			var sb = new StringBuilder();
			sb.AppendLine(_basePath);

			foreach (var key in _files.Keys)
			{
				var entry = _files[key];
				if (entry.DeleteMe == false)
				{
					sb.Append("fn=");
					sb.Append(entry.PathName);

                    if (entry.Directory)
                    {
                        sb.Append("|dir=true");
                    }

					if (entry.Modified.HasValue)
					{
						sb.Append("|mod=");
						sb.Append(entry.Modified.Value.ToString("yyyy-MM-dd HH:mm:ss.fff"));
					}

					if (entry.Size >= 0 && !entry.Directory)
					{
						sb.Append("|size=");
						sb.Append(entry.Size.ToString());
					}

					sb.AppendLine();
				}
			}

			File.WriteAllText(_dbFileName, sb.ToString());
		}

		public void UpdateFile(string relPathName)
		{
			var absFileName = Path.Combine(_basePath, relPathName);
			if (File.Exists(absFileName))
			{
				var fi = new FileInfo(absFileName);

				FileEntry entry;
				if (!_files.TryGetValue(relPathName.ToLower(), out entry))
				{
					_files[relPathName.ToLower()] = entry = new FileEntry();
					entry.PathName = relPathName;
				}

				entry.Modified = fi.LastWriteTime;
				entry.Size = fi.Length;
			}
			else
			{
				_files.Remove(relPathName.ToLower());
			}
		}

        public void UpdateDirectory(string relPathName)
        {
            var absPath = Path.Combine(_basePath, relPathName);
            if (Directory.Exists(absPath))
            {
                FileEntry entry;
                if (!_files.TryGetValue(relPathName.ToLower(), out entry))
                {
                    _files[relPathName.ToLower()] = entry = new FileEntry();
                    entry.PathName = relPathName;
                }

                entry.Directory = true;
            }
            else
            {
                _files.Remove(relPathName.ToLower());
            }
        }

		public bool FileChanged(string relPathName, FileInfo fi, ref string reason)
		{
			FileEntry entry;
			if (_files.TryGetValue(relPathName.ToLower(), out entry))
			{
				if (entry.Size != fi.Length)
				{
					reason = "size changed in db";
					return true;
				}

				if (entry.Modified.HasValue)
				{
					if (!FileModifiedClose(entry.Modified.Value, fi.LastWriteTime))
					{
						reason = "modified date changed in db";
						return true;
					}
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
			FileEntry entry;
			if (_files.TryGetValue(relPathName.ToLower(), out entry))
			{
				entry.DeleteMe = true;
			}
		}

		public bool FileExists(string relPathName)
		{
            FileEntry entry;
            if (_files.TryGetValue(relPathName.ToLower(), out entry))
            {
                return entry.Directory == false;
            }
            return false;
		}

        public bool DirectoryExists(string relPathName)
        {
            FileEntry entry;
            if (_files.TryGetValue(relPathName.ToLower(), out entry))
            {
                return entry.Directory;
            }
            return false;
        }

		public void DeleteDirectory(string relDirPath)
		{
			if (string.IsNullOrEmpty(relDirPath))
			{
				// Delete all files in the database.
				foreach (var entry in _files.Values) entry.DeleteMe = true;
			}
			else
			{
				foreach (var key in _files.Keys)
				{
                    if (PathIsInDir(key, relDirPath))
					{
						_files[key].DeleteMe = true;
					}
				}
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
