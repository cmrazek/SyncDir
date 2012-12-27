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
		private string _leftBasePath = "";
		private string _rightBasePath = "";
		private string _dbFileName = "";
		private Dictionary<string, FileEntry> _files = new Dictionary<string, FileEntry>();

		class FileEntry
		{
			public string FileName { get; set; }
			public DateTime? Modified { get; set; }
			public long Size { get; set; }
			public bool DeleteMe { get; set; }
		}

		public FileDb(string leftBasePath, string rightBasePath)
		{
			if (string.IsNullOrWhiteSpace(leftBasePath) || string.IsNullOrWhiteSpace(rightBasePath)) throw new ArgumentNullException();
			_leftBasePath = leftBasePath;
			_rightBasePath = rightBasePath;
			FindDbFile();
		}

		private void FindDbFile()
		{
			var dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Res.AppNameIdent);
			if (!Directory.Exists(dataPath)) Directory.CreateDirectory(dataPath);

			var header = string.Concat(_leftBasePath, "|", _rightBasePath);
			var found = false;

			foreach (var fileName in Directory.GetFiles(dataPath, "*.dat"))
			{
				using (var sr = new StreamReader(fileName))
				{
					// First line of file contains the base path.
					var fileBasePath = sr.ReadLine();
					if (fileBasePath.Equals(header, StringComparison.OrdinalIgnoreCase))
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

		public string LeftBasePath
		{
			get { return _leftBasePath; }
		}

		public string RightBasePath
		{
			get { return _rightBasePath; }
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
					}
				}

				if (!string.IsNullOrEmpty(fileName))
				{
					_files[fileName.ToLower()] = new FileEntry
						{
							FileName = fileName,
							Modified = modified,
							Size = size
						};
				}
			}
		}

		public void SaveDbFile()
		{
			if (string.IsNullOrWhiteSpace(_dbFileName)) throw new InvalidOperationException("No database file name specified.");

			var sb = new StringBuilder();
			sb.Append(_leftBasePath);
			sb.Append("|");
			sb.AppendLine(_rightBasePath);

			foreach (var key in _files.Keys)
			{
				var entry = _files[key];
				if (entry.DeleteMe == false)
				{
					sb.Append("fn=");
					sb.Append(entry.FileName);

					if (entry.Modified.HasValue)
					{
						sb.Append("|mod=");
						sb.Append(entry.Modified.Value.ToString("yyyy-MM-dd HH:mm:ss.fff"));
					}

					if (entry.Size >= 0)
					{
						sb.Append("|size=");
						sb.Append(entry.Size.ToString());
					}

					sb.AppendLine();
				}
			}

			File.WriteAllText(_dbFileName, sb.ToString());
		}

		public void UpdateFile(string relPathName, FileInfo fi)
		{
			FileEntry entry;
			if (!_files.TryGetValue(relPathName.ToLower(), out entry))
			{
				_files[relPathName.ToLower()] = entry = new FileEntry();
				entry.FileName = relPathName;
			}

			entry.Modified = fi.LastWriteTime;
			entry.Size = fi.Length;
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
			return _files.ContainsKey(relPathName.ToLower());
		}

		public void DeleteFilesInDir(string relDirPath)
		{
			if (string.IsNullOrEmpty(relDirPath))
			{
				// Delete all files in the database.
				foreach (var entry in _files.Values) entry.DeleteMe = true;
			}
			else
			{
				var startsWith = string.Concat(relDirPath, Path.DirectorySeparatorChar);
				foreach (var key in _files.Keys)
				{
					if (key.StartsWith(relDirPath, StringComparison.OrdinalIgnoreCase))
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

		public bool FileExistsInDir(string relDirPath)
		{
			if (string.IsNullOrEmpty(relDirPath))
			{
				return _files.Count > 0;
			}
			else
			{
				var startsWith = string.Concat(relDirPath, Path.DirectorySeparatorChar);
				foreach (var key in _files.Keys)
				{
					if (key.StartsWith(startsWith, StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}

				return false;
			}
		}
		
	}
}
