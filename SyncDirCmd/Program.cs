using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace SyncDirCmd
{
	internal class Program
	{
		private static Program _instance;

		private string _configFile = null;
		private bool _test = false;
		private DateTime _startTime;
		private ReportWriter _rep = null;
		private bool _launchRep = false;
		private Config _config = null;	// Used when the config file has been specified through the command line.
		private Sync _argSync = null;	// Used when sync parameters defined through the command line.

		// Stats
		private int _numFilesAnalyzed = 0;
		private int _numDirsAnalyzed = 0;
		private int _numFilesCopied = 0;
		private int _numFilesDeleted = 0;
		private int _numDirsCreated = 0;
		private int _numDirsDeleted = 0;
		private int _numErrors = 0;
		private int _numWarnings = 0;
		private long _bytesCopied = 0;
		private long _bytesDeleted = 0;

		public static void Main(string[] args)
		{
			ConsoleColor origColor = Console.ForegroundColor;

			try
			{
				_instance = new Program();
				Environment.ExitCode = _instance.Run(args);
			}
			catch (Exception ex)
			{
				Cout.WriteLine(Cout.ErrorColor, ex);
				Environment.ExitCode = 1;
			}

			Console.ForegroundColor = origColor;
		}

		private int Run(string[] args)
		{
			if (!ProcessArguments(args)) return 1;

			_startTime = DateTime.Now;
			var repFileName = GetRepFileName();
			_rep = new ReportWriter(repFileName);
			_rep.StartReport("Synchronization Log");
			_rep.StartHeader();
			_rep.WriteHeader("Start Time", _startTime.ToString("g"));
			_rep.EndHeader();

			if (_argSync != null)
			{
				StartSync(_argSync);
			}
			else if (!string.IsNullOrEmpty(_configFile))
			{
				LoadConfigFile();
				if (_config.sync != null)
				{
					foreach (var sync in _config.sync)
					{
						StartSync(sync);
					}
				}
			}
			else
			{
				throw new InvalidOperationException("No sync operations defined?");
			}

			var endTime = DateTime.Now;
			var duration = endTime.Subtract(_startTime);
			_rep.StartSummary();
			LogSummary("End Time", endTime.ToString("g"));
			LogSummary("Duration", duration.ToString("g"));
			LogSummary("Directories analyzed", _numDirsAnalyzed.ToString());
			LogSummary("Directories created", _numDirsCreated.ToString());
			LogSummary("Directories deleted", _numDirsDeleted.ToString());
			LogSummary("Files analyzed", _numFilesAnalyzed.ToString());
			LogSummary("Files copied", _numFilesCopied.ToString());
			LogSummary("Files deleted", _numFilesDeleted.ToString());
			LogSummary("Size copied", FormatFileSize(_bytesCopied));
			LogSummary("Size deleted", FormatFileSize(_bytesDeleted));
			LogSummary("Errors", _numErrors.ToString());
			LogSummary("Warnings", _numWarnings.ToString());
			_rep.EndSummary();

			if (_rep != null) _rep.Close();

			if (_launchRep)
			{
				System.Diagnostics.Process.Start(repFileName);
			}

			return 0;
		}

		private bool ShowUsage(string message = "")
		{
			if (!string.IsNullOrEmpty(message))
			{
				Cout.WriteLine(Cout.WarningColor, message);
				Cout.WriteLine();
			}

			Cout.WriteLine("Usage:");
			Cout.WriteLine("  SyncDirCmd [switches] [<config_file>]|[<left_dir> <right_dir>]");
			Cout.WriteLine();
			Cout.WriteLine("Arguments:");
			Cout.WriteLine("  config_file - XML configuration file containing the directories to be sync'd");
			Cout.WriteLine("  left_dir    - Left directory");
			Cout.WriteLine("  right_dir   - Right directory");
			Cout.WriteLine();
			Cout.WriteLine("  You must specify either config_file, or left_dir and right_dir.");
			Cout.WriteLine();
			Cout.WriteLine("Switches:");
			Cout.WriteLine("  -left  - Left dir is the master (default).");
			Cout.WriteLine("  -right - Right dir is the master.");
			Cout.WriteLine("  -both  - Both dirs are the master.");
			Cout.WriteLine("  -test  - Test only (do not copy files).");

			return false;
		}

		private bool ProcessArguments(string[] args)
		{
			try
			{
				var rxArgs = new Regex(@"^(?:-|/)(\w+)$");
				Match match;
				var argList = new List<string>();
				var master = Master.left;

				for (int a = 0; a < args.Length; a++)
				{
					var arg = args[a];
					if ((match = rxArgs.Match(arg)).Success)
					{
						var nextArg = a + 1 < args.Length ? args[a + 1] : null;

						switch (match.Groups[1].Value.ToLower())
						{
							case "left":
								master = Master.left;
								break;
							case "right":
								master = Master.right;
								break;
							case "both":
								master = Master.both;
								break;
							case "launchrep":
								_launchRep = true;
								break;
							case "test":
								_test = true;
								break;
							default:
								throw new ArgumentException(string.Format("Invalid switch '{0}'.", match.Groups[1].Value));
						}
					}
					else
					{
						argList.Add(arg);
					}
				}

				if (argList.Count == 2)
				{
					var sync = new Sync
					{
						left = argList[0],
						right = argList[1],
						master = master,
						masterSpecified = true
					};

					if (!Directory.Exists(sync.left)) throw new ArgumentException("Left path does not exist.");
					if (!Directory.Exists(sync.right)) throw new ArgumentException("Right path does not exist.");

					_argSync = sync;
				}
				else if (argList.Count == 1)
				{
					_configFile = argList[0];

					if (!File.Exists(_configFile)) throw new ArgumentException("Config file does not exist.");
				}
				else
				{
					throw new ArgumentException("Wrong number of arguments.");
				}

				return true;
			}
			catch (ArgumentException ex)
			{
				return ShowUsage(ex.Message);
			}
			catch (Exception ex)
			{
				throw ex;
			}
		}

		public static Program Instance
		{
			get { return _instance; }
		}

		private void LoadConfigFile()
		{
			var schemaFileName = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Config.xsd");
			var xmlReaderSettings = new XmlReaderSettings();
			xmlReaderSettings.Schemas.Add("SyncDirCmd.Config", schemaFileName);
			xmlReaderSettings.ValidationType = ValidationType.Schema;

			var fileStream = new FileStream(_configFile, FileMode.Open, FileAccess.Read);
			try
			{
				using (var xmlReader = XmlReader.Create(fileStream, xmlReaderSettings))
				{
					fileStream = null;
					var serializer = new XmlSerializer(typeof(Config));
					_config = serializer.Deserialize(xmlReader) as Config;
				}
			}
			catch (Exception ex)
			{
				if (fileStream != null) fileStream.Close();
				throw ex;
			}
		}

		private string GetRepFileName()
		{
			var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Res.AppNameIdent);
			if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

			var repDir = Path.Combine(dataDir, Res.ReportDirName);
			if (!Directory.Exists(repDir)) Directory.CreateDirectory(repDir);

			var fileName = Path.Combine(repDir, string.Format("SyncDirCmd Report {0}.htm", _startTime.ToString("yyyy-MM-dd HH.mm.ss")));
			var index = 0;
			while (File.Exists(fileName))
			{
				fileName = Path.Combine(repDir, string.Format("SyncDirCmd Report {0} ({1}).htm", _startTime.ToString("yyyy-MM-dd HH.mm.ss"), ++index));
			}

			return fileName;
		}

		public void LogError(Exception ex, string message)
		{
			_numErrors++;
			Cout.WriteLine(Cout.ErrorColor, message);
			Cout.WriteLine(Cout.ErrorColor, ex);
			_rep.WriteError(ex, message);
		}

		public void LogWarning(string message)
		{
			_numWarnings++;
			Cout.WriteLine(Cout.WarningColor, message);
			_rep.WriteWarning(message);
		}

		private void LogSummary(string label, string value)
		{
			Cout.WriteLine(Cout.NormalColor, string.Format("{0}: {1}", label, value));
			_rep.WriteSummary(label, value);
		}

		private void StartSync(Sync sync)
		{
			try
			{
				string title;
				if (!string.IsNullOrWhiteSpace(sync.name)) title = string.Format("Synchronizing directories ({0})", sync.name);
				else title = "Synchronizing directories";

				switch (sync.master)
				{
					case Master.left:
						_rep.WriteSyncStart(title, "Master Directory", sync.left, "Mirror Directory", sync.right);
						Cout.WriteLine(string.Concat("Master Directory: ", sync.left));
						Cout.WriteLine(string.Concat("Mirror Directory: ", sync.right));
						OneMaster(sync, sync.left, sync.right);
						break;
					case Master.right:
						_rep.WriteSyncStart(title, "Master Directory", sync.right, "Mirror Directory", sync.left);
						Cout.WriteLine(string.Concat("Master Directory: ", sync.right));
						Cout.WriteLine(string.Concat("Mirror Directory: ", sync.left));
						OneMaster(sync, sync.right, sync.left);
						break;
					case Master.both:
						_rep.WriteSyncStart(title, "Left Directory", sync.left, "Right Directory", sync.right);
						Cout.WriteLine(string.Concat("Left Directory: ", sync.left));
						Cout.WriteLine(string.Concat("Right Directory: ", sync.right));
						BothMaster(sync, sync.left, sync.right);
						break;
				}
			}
			catch (Exception ex)
			{
				_rep.WriteError(ex, "Exception when running sync.");
			}
		}

		private void OneMaster(Sync sync, string masterPath, string mirrorPath)
		{
			FileDb masterDb = new FileDb(masterPath);
			FileDb mirrorDb = new FileDb(mirrorPath);
			OneDir(new DirState(sync, masterDb, mirrorDb, ""));

			if (!_test)
			{
				masterDb.SaveDbFile();
				mirrorDb.SaveDbFile();
			}
		}

		private void OneDir(DirState state)
		{
			try
			{
				Cout.WriteLine(Cout.UnimportantColor, state.RelPath);
				_numDirsAnalyzed++;

				var leftPath = state.LeftAbsPath;
				var rightPath = state.RightAbsPath;
				var sync = state.Sync;

				string[] leftFiles;
				string[] leftDirs;
				if (Directory.Exists(leftPath))
				{
					leftFiles = GetUnignoredFileNamesInDirectory(state, leftPath).ToArray();
					leftDirs = GetUnignoredDirectoryNamesInDirectory(state, leftPath).ToArray();
				}
				else
				{
					leftFiles = new string[] { };
					leftDirs = new string[] { };
				}

				string[] rightFiles;
				string[] rightDirs;
				if (Directory.Exists(rightPath))
				{
					rightFiles = GetUnignoredFileNamesInDirectory(state, rightPath).ToArray();
					rightDirs = GetUnignoredDirectoryNamesInDirectory(state, rightPath).ToArray();
				}
				else
				{
					rightFiles = new string[] { };
					rightDirs = new string[] { };
				}

				// Find new and changed files.
				foreach (var fileName in leftFiles)
				{
					_numFilesAnalyzed++;

					var relFileName = state.GetRelFileName(fileName);
					var leftAbsFileName = state.GetLeftAbsFileName(fileName);
					var rightAbsFileName = state.GetRightAbsFileName(fileName);

					if (!rightFiles.Any(f => f.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
					{
						try
						{
							// File does not exist on the right.
							if (CopyFile(relFileName, leftAbsFileName, rightAbsFileName, "does not exist in mirror"))
							{
								state.LeftDb.UpdateFile(relFileName);
								state.RightDb.UpdateFile(relFileName);
							}
						}
						catch (Exception ex)
						{
							LogError(ex, string.Format("Error when copying file '{0}'.", state.RelPath));
						}
					}
					else
					{
						// File exists in both locations.  Check to see if they're the same.
						var leftInfo = new FileInfo(leftAbsFileName);
						var rightInfo = new FileInfo(rightAbsFileName);

						var copy = false;
						var reason = "";
						if (state.LeftDb.FileChanged(relFileName, leftInfo, ref reason))
						{
							copy = true;
							reason = "master file: " + reason;
						}
						else if (leftInfo.Length != rightInfo.Length)
						{
							copy = true;
							reason = "size different";
						}
						else if (Math.Abs(leftInfo.LastWriteTime.Subtract(rightInfo.LastWriteTime).TotalMinutes) > 1.0)
						{
							reason = "modified dates different";
							copy = true;
						}

						if (copy)
						{
							CopyFile(relFileName, leftAbsFileName, rightAbsFileName, reason);
						}
					}

					state.LeftDb.UpdateFile(relFileName);
					state.RightDb.UpdateFile(relFileName);
				}

				// Find deleted files.
				foreach (var fileName in (from r in rightFiles where !leftFiles.Any(l => l.Equals(r, StringComparison.OrdinalIgnoreCase)) select r))
				{
					_numFilesAnalyzed++;

					var relFileName = state.GetRelFileName(fileName);

					var reason = "";
					if (state.LeftDb.FileExists(fileName)) reason = "file deleted in master";
					else reason = "file does not exist in master";
					DeleteFile(state.GetRelFileName(fileName), state.GetRightAbsFileName(fileName), reason);

					state.LeftDb.DeleteFile(relFileName);
					state.RightDb.DeleteFile(relFileName);
				}

				// Find new directories, and recurse into existing ones.
				foreach (var dirName in leftDirs)
				{
					if (!rightDirs.Any(f => f.Equals(dirName, StringComparison.OrdinalIgnoreCase)))
					{
						// Directory does not exist on the right.
						CreateDir(state.GetRelFileName(dirName), state.GetRightAbsFileName(dirName), "new directory");
					}
					else
					{
						// Directory already exists on the right.
					}

					OneDir(new DirState(state, dirName));
				}

				// Find deleted directories.
				foreach (var dirName in (from r in rightDirs where !leftDirs.Any(l => l.Equals(r, StringComparison.OrdinalIgnoreCase)) select r))
				{
					var relDirName = state.GetRelFileName(dirName);

					string reason;
					if (state.LeftDb.FileExistsInDir(relDirName)) reason = "directory deleted in master";
					else reason = "directory does not exist in master";

					DeleteDir(relDirName, state.GetRightAbsFileName(dirName), reason);

					state.LeftDb.DeleteFilesInDir(relDirName);
					state.RightDb.DeleteFilesInDir(relDirName);
				}
			}
			catch (Exception ex)
			{
				LogError(ex, string.Format("Error when processing directory '{0}'.", state.RelPath));
			}
		}

		private void BothMaster(Sync sync, string leftPath, string rightPath)
		{
			var leftDb = new FileDb(leftPath);
			var rightDb = new FileDb(rightPath);
			BothDir(new DirState(sync, leftDb, rightDb, ""));

			if (!_test)
			{
				leftDb.SaveDbFile();
				rightDb.SaveDbFile();
			}
		}

		private void BothDir(DirState state)
		{
			try
			{
				Cout.WriteLine(Cout.UnimportantColor, state.RelPath);
				_numDirsAnalyzed++;

				string[] leftFiles, leftDirs, rightFiles, rightDirs;

			    if (Directory.Exists(state.LeftAbsPath))
			    {
					leftFiles = GetUnignoredFileNamesInDirectory(state, state.LeftAbsPath).ToArray();
					leftDirs = GetUnignoredDirectoryNamesInDirectory(state, state.LeftAbsPath).ToArray();
			    }
			    else
			    {
			        leftFiles = new string[] { };
			        leftDirs = new string[] { };
			    }

			    if (Directory.Exists(state.RightAbsPath))
			    {
					rightFiles = GetUnignoredFileNamesInDirectory(state, state.RightAbsPath).ToArray();
					rightDirs = GetUnignoredDirectoryNamesInDirectory(state, state.RightAbsPath).ToArray();
			    }
			    else
			    {
			        rightFiles = new string[] { };
			        rightDirs = new string[] { };
			    }

			    // Files that don't exist on right.
				foreach (var fileName in leftFiles)
				{
					_numFilesAnalyzed++;

					var leftAbsFileName = state.GetLeftAbsFileName(fileName);
					var rightAbsFileName = state.GetRightAbsFileName(fileName);
					var relFileName = state.GetRelFileName(fileName);

					if (!rightFiles.Any(r => r.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
					{
						// File is on left only.
						if (state.RightDb.FileExists(relFileName))
						{
							DeleteFile(relFileName, leftAbsFileName, "file deleted on right");
						}
						else
						{
							CopyFile(relFileName, leftAbsFileName, rightAbsFileName, "file new on left");
						}
					}
					else
					{
						// File exists on both sides.
						var leftInfo = new FileInfo(leftAbsFileName);
						var rightInfo = new FileInfo(rightAbsFileName);
						var leftReason = "";
						var rightReason = "";
						var leftChanged = state.LeftDb.FileChanged(relFileName, leftInfo, ref leftReason);
						var rightChanged = state.RightDb.FileChanged(relFileName, rightInfo, ref rightReason);

						if (leftChanged && !rightChanged)
						{
							// User updated the left file.
							CopyFile(relFileName, leftAbsFileName, rightAbsFileName, string.Concat("left file: ", leftReason));
						}
						else if (!leftChanged && rightChanged)
						{
							// User updated the right file.
							CopyFile(relFileName, rightAbsFileName, leftAbsFileName, string.Concat("right file: ", rightReason));
						}
						else
						{
							// Either the user updated both files, or the file is not recorded in the database yet.
							if (leftInfo.Length != rightInfo.Length || !FileDb.FileModifiedClose(leftInfo.LastWriteTime, rightInfo.LastWriteTime))
							{
								// Pick the file with the most recent modification time.
								if (leftInfo.LastWriteTime > rightInfo.LastWriteTime)
								{
									// Left file is newer.
									CopyFile(relFileName, leftAbsFileName, rightAbsFileName, "left file modified date is newer");
								}
								else if (leftInfo.LastWriteTime < rightInfo.LastWriteTime)
								{
									// Right file is newer.
									CopyFile(relFileName, rightAbsFileName, leftAbsFileName, "right file modified date is newer");
								}
								else
								{
									// Dates match exactly, but file lengths are different.
									// This shouldn't happen; if it does, then don't do anything since we don't know which file the user wants to keep.
									LogWarning(string.Format("The file '{0}' has the same modification date on either side, but a different size.", relFileName));
								}
							}
						}
					}

					state.LeftDb.UpdateFile(relFileName);
					state.RightDb.UpdateFile(relFileName);
				}

				// Files on the right only.
				foreach (var fileName in rightFiles)
				{
					if (leftFiles.Any(f => f.Equals(fileName, StringComparison.OrdinalIgnoreCase))) continue;

					_numFilesAnalyzed++;

					var leftAbsFileName = state.GetLeftAbsFileName(fileName);
					var rightAbsFileName = state.GetRightAbsFileName(fileName);
					var relFileName = state.GetRelFileName(fileName);

					if (state.LeftDb.FileExists(relFileName))
					{
						if (DeleteFile(relFileName, rightAbsFileName, "file deleted on left"))
						{
							state.LeftDb.DeleteFile(relFileName);
							state.RightDb.DeleteFile(relFileName);
						}
					}
					else
					{
						CopyFile(relFileName, rightAbsFileName, leftAbsFileName, "file new on right");
					}

					state.LeftDb.UpdateFile(relFileName);
					state.RightDb.UpdateFile(relFileName);
				}

				// Directories on left or both.
				foreach (var dirName in leftDirs)
				{
					var relDirName = state.GetRelFileName(dirName);

					if (!rightDirs.Any(r => r.Equals(dirName, StringComparison.OrdinalIgnoreCase)))
					{
						// On left side only.
						if (state.RightDb.FileExistsInDir(relDirName))
						{
							if (DeleteDir(relDirName, state.GetLeftAbsFileName(dirName), "directory on left deleted by user"))
							{
								state.RightDb.DeleteFilesInDir(relDirName);
								state.LeftDb.DeleteFilesInDir(relDirName);
							}
						}
						else
						{
							if (CreateDir(relDirName, state.GetRightAbsFileName(dirName), "directory on right new"))
							{
								BothDir(new DirState(state, dirName));
							}
						}
					}
					else
					{
						// Directories exists on both sides.
						BothDir(new DirState(state, dirName));
					}
				}

				// Directories on right side only.
				foreach (var dirName in rightDirs)
				{
					if (leftDirs.Any(l => l.Equals(dirName, StringComparison.OrdinalIgnoreCase))) continue;

					var relDirName = state.GetRelFileName(dirName);

					if (state.LeftDb.FileExistsInDir(relDirName))
					{
						if (DeleteDir(relDirName, state.GetRightAbsFileName(dirName), "directory on right deleted by user"))
						{
							state.RightDb.DeleteFilesInDir(relDirName);
							state.LeftDb.DeleteFilesInDir(relDirName);
						}
					}
					else
					{
						if (CreateDir(relDirName, state.GetLeftAbsFileName(dirName), "directory on left new"))
						{
							BothDir(new DirState(state, dirName));
						}
					}
				}
			}
			catch (Exception ex)
			{
				LogError(ex, string.Format("Error when processing directory '{0}'.", state.RelPath));
			}
		}

		private bool CopyFile(string relFileName, string srcFileName, string dstFileName, string reason)
		{
			try 
			{
				var fi = new FileInfo(srcFileName);

				_rep.WriteOperation("Copy File: " + relFileName,
					new ReportDetail("From File Name", srcFileName),
					new ReportDetail("To File Name", dstFileName),
					new ReportDetail("Reason", reason),
					new ReportDetail("Size", FormatFileSize(fi.Length)));
				Cout.WriteLine(Cout.ImportantColor, string.Concat("Copy File: ", relFileName));

				if (!_test) File.Copy(srcFileName, dstFileName, true);
				_numFilesCopied++;
				_bytesCopied += fi.Length;
				return true;
			}
			catch (Exception ex)
			{
				LogError(ex, "Error when copying file.");
				return false;
			}
		}

		private bool CreateDir(string relPath, string absPath, string reason)
		{
			try
			{
				_rep.WriteOperation("Create Directory: " + relPath, new ReportDetail("Path", absPath), new ReportDetail("Reason", reason));
				Cout.WriteLine(Cout.ImportantColor, string.Concat("Create Dir: ", relPath));

				if (!_test) Directory.CreateDirectory(absPath);
				_numDirsCreated++;
				return true;
			}
			catch (Exception ex)
			{
				LogError(ex, "Error when creating directory.");
				return false;
			}
		}

		private bool DeleteFile(string relFileName, string absFileName, string reason)
		{
			try
			{
				var fi = new FileInfo(absFileName);
				var length = fi.Length;

				_rep.WriteOperation("Delete File: " + relFileName,
					new ReportDetail("File Name", absFileName),
					new ReportDetail("Reason", reason),
					new ReportDetail("Size", FormatFileSize(length)));
				Cout.WriteLine(Cout.ImportantColor, string.Concat("Delete File: ", relFileName));

				if (!_test)
				{
					if ((fi.Attributes & FileAttributes.ReadOnly) != 0) File.SetAttributes(absFileName, fi.Attributes & ~FileAttributes.ReadOnly);
					File.Delete(absFileName);
				}
				_numFilesDeleted++;
				_bytesDeleted += length;
				return true;
			}
			catch (Exception ex)
			{
				LogError(ex, "Error when deleting file.");
				return false;
			}
		}

		private bool DeleteDir(string relPath, string absPath, string reason)
		{
			try
			{
				
				Cout.WriteLine(Cout.ImportantColor, string.Concat("Delete Dir: ", relPath));

				long bytesDeleted = 0;

				if (!_test) DeleteDir_Sub(absPath, ref bytesDeleted);

				_rep.WriteOperation("Delete Directory: " + relPath,
					new ReportDetail("Path", absPath),
					new ReportDetail("Reason", reason),
					new ReportDetail("Size", FormatFileSize(bytesDeleted)));
				return true;
			}
			catch (Exception ex)
			{
				_rep.WriteOperation("Delete Directory: " + relPath, new ReportDetail("Path", absPath), new ReportDetail("Reason", reason));
				LogError(ex, "Error when deleting directory.");
				return false;
			}
		}

		private void DeleteDir_Sub(string path, ref long bytesDeleted)
		{
			foreach (var fileName in Directory.GetFiles(path))
			{
				var length = (new FileInfo(fileName)).Length;

				var fi = new FileInfo(fileName);
				if ((fi.Attributes & FileAttributes.ReadOnly) != 0) File.SetAttributes(fileName, fi.Attributes & ~FileAttributes.ReadOnly);
				File.Delete(fileName);

				_numFilesDeleted++;
				bytesDeleted += length;
				_bytesDeleted += length;
			}

			foreach (var dirName in Directory.GetDirectories(path))
			{
				DeleteDir_Sub(dirName, ref bytesDeleted);
			}

			var di = new DirectoryInfo(path);
			if ((di.Attributes & FileAttributes.ReadOnly) != 0) di.Attributes &= ~FileAttributes.ReadOnly;
			Directory.Delete(path);
			_numDirsDeleted++;
		}

		private string FormatFileSize(long size)
		{
			double sizef = size;

			if (sizef < 1024.0) return string.Format("{0:N0} B", sizef);
			sizef /= 1024.0;
			if (sizef < 1024.0) return string.Format("{0:N1} KB", sizef);
			sizef /= 1024.0;
			if (sizef < 1024.0) return string.Format("{0:N1} MB", sizef);
			sizef /= 1024.0;
			if (sizef < 1024.0) return string.Format("{0:N1} GB", sizef);
			sizef /= 1024.0;
			return string.Format("{0:N1} TB", sizef);
		}

		private IEnumerable<string> GetUnignoredFileNamesInDirectory(DirState state, string dirPath)
		{
			var sync = state.Sync;

			foreach (var fullPath in Directory.GetFiles(dirPath))
			{
				var fileName = Path.GetFileName(fullPath);
				var relPath = state.GetRelFileName(fileName);
				if (sync.IgnorePath(relPath)) continue;

				yield return fileName;
			}
		}

		private IEnumerable<string> GetUnignoredDirectoryNamesInDirectory(DirState state, string dirPath)
		{
			var sync = state.Sync;

			foreach (var fullPath in Directory.GetDirectories(dirPath))
			{
				var dirName = Path.GetFileName(fullPath);
				var relPath = state.GetRelFileName(dirName);
				if (sync.IgnorePath(relPath)) continue;

				yield return dirName;
			}
		}
	}
}
