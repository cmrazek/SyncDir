using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SyncDirCmd
{
	internal class Program
	{
		private string _leftPath = null;
		private string _rightPath = null;
		private MasterDir _master = MasterDir.Left;
		private string _reportFileName = null;
		private string _errorFileName = null;
		private bool _test = false;
		private DateTime _startTime;
		private ReportWriter _rep = null;
		private bool _launchRep = false;

		// Stats
		private int _numFilesAnalyzed = 0;
		private int _numDirsAnalyzed = 0;
		private int _numFilesCopied = 0;
		private int _numFilesDeleted = 0;
		private int _numDirsCreated = 0;
		private int _numDirsDeleted = 0;
		private int _numErrors = 0;
		private int _numWarnings = 0;

		private enum MasterDir
		{
			Left,
			Right,
			Both
		}

		public static void Main(string[] args)
		{
			ConsoleColor origColor = Console.ForegroundColor;

			try
			{
				var prog = new Program();
				Environment.ExitCode = prog.Run(args);
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

			switch (_master)
			{
				case MasterDir.Left:
					_rep.WriteHeader("Master Directory", _leftPath);
					_rep.WriteHeader("Mirror Directory", _rightPath);
					Cout.WriteLine(string.Concat("Master Directory: ", _leftPath));
					Cout.WriteLine(string.Concat("Mirror Directory: ", _rightPath));
					break;
				case MasterDir.Right:
					_rep.WriteHeader("Master Directory", _rightPath);
					_rep.WriteHeader("Mirror Directory", _leftPath);
					Cout.WriteLine(string.Concat("Master Directory: ", _rightPath));
					Cout.WriteLine(string.Concat("Mirror Directory: ", _leftPath));
					break;
				case MasterDir.Both:
					_rep.WriteHeader("Left Directory", _leftPath);
					_rep.WriteHeader("Right Directory", _rightPath);
					Cout.WriteLine(string.Concat("Left Directory: ", _leftPath));
					Cout.WriteLine(string.Concat("Right Directory: ", _rightPath));
					break;
			}

			_rep.EndHeader();

			switch (_master)
			{
				case MasterDir.Left:
					OneMaster(_leftPath, _rightPath);
					break;
				case MasterDir.Right:
					OneMaster(_rightPath, _leftPath);
					break;
				case MasterDir.Both:
					BothMaster();
					break;
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
			Cout.WriteLine("  SyncDirCmd <switches> <left_dir> <right_dir>");
			Cout.WriteLine();
			Cout.WriteLine("Switches:");
			Cout.WriteLine("  -left              - Left dir is the master (default).");
			Cout.WriteLine("  -right             - Right dir is the master.");
			Cout.WriteLine("  -both              - Both dirs are the master.");
			Cout.WriteLine("  -report <filename> - Generate a report.");
			Cout.WriteLine("  -error <filename>  - Generate a report for errors only.");
			Cout.WriteLine("  -test              - Test only (do not copy files).");

			return false;
		}

		private bool ProcessArguments(string[] args)
		{
			try
			{
				var rxArgs = new Regex(@"^(?:-|/)(\w+)$");
				Match match;
				var argIndex = 0;

				for (int a = 0; a < args.Length; a++)
				{
					var arg = args[a];
					if ((match = rxArgs.Match(arg)).Success)
					{
						var nextArg = a + 1 < args.Length ? args[a + 1] : null;
						if (ProcessSwitch(match.Groups[1].Value, nextArg) && nextArg != null) a++;
					}
					else
					{
						switch (argIndex++)
						{
							case 0:
								_leftPath = arg;
								break;
							case 1:
								_rightPath = arg;
								break;
							default:
								throw new ArgumentException(string.Format("Invalid argument '{0}'.", arg));
						}
					}
				}

				if (string.IsNullOrWhiteSpace(_leftPath)) throw new ArgumentException("Left path not specified.");
				if (string.IsNullOrWhiteSpace(_rightPath)) throw new ArgumentException("Right path not specified.");
				if (!Directory.Exists(_leftPath)) throw new ArgumentException("Left path does not exist.");
				if (!Directory.Exists(_rightPath)) throw new ArgumentException("Right path does not exist.");

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

		private bool ProcessSwitch(string switchName, string nextArg)
		{
			switch (switchName.ToLower())
			{
				case "left":
					_master = MasterDir.Left;
					return false;
				case "right":
					_master = MasterDir.Right;
					return false;
				case "both":
					_master = MasterDir.Both;
					return false;
				case "launchrep":
					_launchRep = true;
					return false;
				case "test":
					_test = true;
					return false;
				default:
					throw new ArgumentException(string.Format("Invalid switch '{0}'.", switchName));
			}
		}

		private string GetRepFileName()
		{
			var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Res.AppNameIdent);
			if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

			var fileName = Path.Combine(dataDir, string.Format("SyncDirCmd Report {0}.htm", _startTime.ToString("yyyy-MM-dd HH.mm.ss")));
			var index = 0;
			while (File.Exists(fileName))
			{
				fileName = Path.Combine(dataDir, string.Format("SyncDirCmd Report {0} ({1}).htm", _startTime.ToString("yyyy-MM-dd HH.mm.ss"), ++index));
			}

			return fileName;
		}

		private void LogError(Exception ex, string message)
		{
			_numErrors++;
			Cout.WriteLine(Cout.ErrorColor, message);
			Cout.WriteLine(Cout.ErrorColor, ex);
			_rep.WriteError(ex, message);
		}

		private void LogWarning(string message)
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

		private void OneMaster(string masterPath, string mirrorPath)
		{
			FileDb fileDb = new FileDb(masterPath, mirrorPath);
			OneDir(new DirState(fileDb, ""));

			if (!_test)
			{
				fileDb.SaveDbFile();
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

				string[] leftFiles;
				string[] leftDirs;
				if (Directory.Exists(leftPath))
				{
					leftFiles = (from f in Directory.GetFiles(leftPath) select Path.GetFileName(f)).ToArray();
					leftDirs = (from f in Directory.GetDirectories(leftPath) select Path.GetFileName(f)).ToArray();
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
					rightFiles = (from f in Directory.GetFiles(rightPath) select Path.GetFileName(f)).ToArray();
					rightDirs = (from f in Directory.GetDirectories(rightPath) select Path.GetFileName(f)).ToArray();
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
							var leftInfo = new FileInfo(leftAbsFileName);
							if (CopyFile(relFileName, leftAbsFileName, rightAbsFileName, "does not exist in mirror"))
							{
								state.FileDb.UpdateFile(state.GetRelFileName(fileName), leftInfo);
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
						if (state.FileDb.FileChanged(relFileName, leftInfo, ref reason))
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
							if (CopyFile(relFileName, leftAbsFileName, rightAbsFileName, reason))
							{
								state.FileDb.UpdateFile(relFileName, leftInfo);
							}
						}
						else
						{
							state.FileDb.UpdateFile(relFileName, leftInfo);
						}
					}
				}

				// Find deleted files.
				foreach (var fileName in (from r in rightFiles where !leftFiles.Any(l => l.Equals(r, StringComparison.OrdinalIgnoreCase)) select r))
				{
					_numFilesAnalyzed++;

					var reason = "";
					if (state.FileDb.FileExists(fileName))
					{
						reason = "file deleted in master";
					}
					else
					{
						reason = "file does not exist in master";
					}

					if (DeleteFile(state.GetRelFileName(fileName), state.GetRightAbsFileName(fileName), reason))
					{
						state.FileDb.DeleteFile(state.GetRelFileName(fileName));
					}
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
					if (DeleteDir(state.GetRelFileName(dirName), state.GetRightAbsFileName(dirName), "directory does not exist in master"))
					{
						state.FileDb.DeleteFilesInDir(state.GetRelFileName(dirName));
					}
				}
			}
			catch (Exception ex)
			{
				LogError(ex, string.Format("Error when processing directory '{0}'.", state.RelPath));
			}
		}

		private void BothMaster()
		{
			var fileDb = new FileDb(_leftPath, _rightPath);
			BothDir(new DirState(fileDb, ""));

			if (!_test)
			{
				fileDb.SaveDbFile();
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
			        leftFiles = (from f in Directory.GetFiles(state.LeftAbsPath) select Path.GetFileName(f)).ToArray();
			        leftDirs = (from d in Directory.GetDirectories(state.LeftAbsPath) select Path.GetFileName(d)).ToArray();
			    }
			    else
			    {
			        leftFiles = new string[] { };
			        leftDirs = new string[] { };
			    }

			    if (Directory.Exists(state.RightAbsPath))
			    {
			        rightFiles = (from f in Directory.GetFiles(state.RightAbsPath) select Path.GetFileName(f)).ToArray();
			        rightDirs = (from d in Directory.GetDirectories(state.RightAbsPath) select Path.GetFileName(d)).ToArray();
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
						if (state.FileDb.FileExists(relFileName))
						{
							if (DeleteFile(relFileName, leftAbsFileName, "file deleted on right"))
							{
								state.FileDb.DeleteFile(relFileName);
							}
						}
						else
						{
							if (CopyFile(relFileName, leftAbsFileName, rightAbsFileName, "file new on left"))
							{
								state.FileDb.UpdateFile(relFileName, new FileInfo(leftAbsFileName));
							}
						}
					}
					else
					{
						// File exists on both sides.
						var leftInfo = new FileInfo(leftAbsFileName);
						var rightInfo = new FileInfo(rightAbsFileName);
						var leftReason = "";
						var rightReason = "";
						var leftChanged = state.FileDb.FileChanged(relFileName, leftInfo, ref leftReason);
						var rightChanged = state.FileDb.FileChanged(relFileName, rightInfo, ref rightReason);

						if (leftChanged && !rightChanged)
						{
							// User updated the left file.
							if (CopyFile(relFileName, leftAbsFileName, rightAbsFileName, string.Concat("left file: ", leftReason)))
							{
								state.FileDb.UpdateFile(relFileName, leftInfo);
							}
						}
						else if (!leftChanged && rightChanged)
						{
							// User updated the right file.
							if (CopyFile(relFileName, rightAbsFileName, leftAbsFileName, string.Concat("right file: ", rightReason)))
							{
								state.FileDb.UpdateFile(relFileName, rightInfo);
							}
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
									if (CopyFile(relFileName, leftAbsFileName, rightAbsFileName, "left file modified date is newer"))
									{
										state.FileDb.UpdateFile(relFileName, leftInfo);
									}
								}
								else if (leftInfo.LastWriteTime < rightInfo.LastWriteTime)
								{
									// Right file is newer.
									if (CopyFile(relFileName, rightAbsFileName, leftAbsFileName, "right file modified date is newer"))
									{
										state.FileDb.UpdateFile(relFileName, rightInfo);
									}
								}
								else
								{
									// Dates match exactly, but file lengths are different.
									// This shouldn't happen; if it does, then don't do anything since we don't know which file the user wants to keep.
									LogWarning(string.Format("The file '{0}' has the same modification date on either side, but a different size.", relFileName));
								}
							}
							else
							{
								// File is the same.
								state.FileDb.UpdateFile(relFileName, leftInfo);
							}
						}
					}
				}

				// Files on the right only.
				foreach (var fileName in rightFiles)
				{
					if (leftFiles.Any(f => f.Equals(fileName, StringComparison.OrdinalIgnoreCase))) continue;

					_numFilesAnalyzed++;

					var leftAbsFileName = state.GetLeftAbsFileName(fileName);
					var rightAbsFileName = state.GetRightAbsFileName(fileName);
					var relFileName = state.GetRelFileName(fileName);

					if (state.FileDb.FileExists(relFileName))
					{
						if (DeleteFile(relFileName, rightAbsFileName, "file deleted on left"))
						{
							state.FileDb.DeleteFile(relFileName);
						}
					}
					else
					{
						if (CopyFile(relFileName, rightAbsFileName, leftAbsFileName, "file new on right"))
						{
							state.FileDb.UpdateFile(relFileName, new FileInfo(rightAbsFileName));
						}
					}
				}

				// Directories on left or both.
				foreach (var dirName in leftDirs)
				{
					var relDirName = state.GetRelFileName(dirName);

					if (!rightDirs.Any(r => r.Equals(dirName, StringComparison.OrdinalIgnoreCase)))
					{
						// On left side only.
						if (state.FileDb.FileExistsInDir(relDirName))
						{
							if (DeleteDir(relDirName, state.GetLeftAbsFileName(dirName), "directory on left deleted by user"))
							{
								state.FileDb.DeleteFilesInDir(relDirName);
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

					if (state.FileDb.FileExistsInDir(relDirName))
					{
						if (DeleteDir(relDirName, state.GetRightAbsFileName(dirName), "directory on right deleted by user"))
						{
							state.FileDb.DeleteFilesInDir(relDirName);
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
				_rep.WriteOperation("Copy File: " + relFileName, new ReportDetail("From File Name", srcFileName), new ReportDetail("To File Name", dstFileName), new ReportDetail("Reason", reason));
				Cout.WriteLine(Cout.ImportantColor, string.Concat("Copy File: ", relFileName));

				if (!_test) File.Copy(srcFileName, dstFileName, true);
				_numFilesCopied++;
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
				_rep.WriteOperation("Delete File: " + relFileName, new ReportDetail("File Name", absFileName), new ReportDetail("Reason", reason));
				Cout.WriteLine(Cout.ImportantColor, string.Concat("Delete File: ", relFileName));

				if (!_test) File.Delete(absFileName);
				_numFilesDeleted++;
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
				_rep.WriteOperation("Delete Directory: " + relPath, new ReportDetail("Path", absPath), new ReportDetail("Reason", reason));
				Cout.WriteLine(Cout.ImportantColor, string.Concat("Delete Dir: ", relPath));

				if (!_test) DeleteDir_Sub(absPath);
				return true;
			}
			catch (Exception ex)
			{
				LogError(ex, "Error when deleting directory.");
				return false;
			}
		}

		private void DeleteDir_Sub(string path)
		{
			foreach (var fileName in Directory.GetFiles(path))
			{
				File.Delete(fileName);
				_numFilesDeleted++;
			}

			foreach (var dirName in Directory.GetDirectories(path))
			{
				DeleteDir_Sub(dirName);
			}

			Directory.Delete(path);
			_numDirsDeleted++;
		}
	}
}
