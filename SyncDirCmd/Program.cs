using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SyncDirCmd
{
    internal class Program
    {
        private static Program _instance;

        private string _configFile;
        private bool _test;
        private DateTime _startTime;
        private ReportWriter _rep;
        private bool _launchRep;
        private string _repDir;
        private ConfigFile _config;	// Used when the config file has been specified through the command line.
        private SyncDir _argSync;  // Used when sync parameters defined through the command line.
        private Stats _leftStats = new Stats();
        private Stats _rightStats = new Stats();
        private int _numErrors = 0;
        private int _numWarnings = 0;

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

            using (var db = new Database())
            {
                if (_argSync != null)
                {
                    StartSync(db, _argSync);
                }
                else if (!string.IsNullOrEmpty(_configFile))
                {
                    LoadConfigFile();
                    if (_config.Directories != null)
                    {
                        foreach (var sync in _config.Directories)
                        {
                            StartSync(db, sync);
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException("No sync operations defined?");
                }
            }

            var endTime = DateTime.Now;
            var duration = endTime.Subtract(_startTime);
            _rep.StartSummary();
            LogSummary("End Time", endTime.ToString("g"));
            LogSummary("Duration", duration.ToString("g"));
            LogSummary("Directories analyzed", _leftStats.NumDirsAnalyzed.ToString(), _rightStats.NumDirsAnalyzed.ToString());
            LogSummary("Directories created", _leftStats.NumDirsCreated.ToString(), _rightStats.NumDirsCreated.ToString());
            LogSummary("Directories deleted", _leftStats.NumDirsDeleted.ToString(), _rightStats.NumDirsDeleted.ToString());
            LogSummary("Files analyzed", _leftStats.NumFilesAnalyzed.ToString(), _rightStats.NumFilesAnalyzed.ToString());
            LogSummary("Files copied", _leftStats.NumFilesCopied.ToString(), _rightStats.NumFilesCopied.ToString());
            LogSummary("Files deleted", _leftStats.NumFilesDeleted.ToString(), _rightStats.NumFilesDeleted.ToString());
            LogSummary("Size copied", _leftStats.BytesCopied.FormatSize(), _rightStats.BytesCopied.FormatSize());
            LogSummary("Size deleted", _leftStats.BytesDeleted.FormatSize(), _rightStats.BytesDeleted.FormatSize());
            LogSummary("Size net", _leftStats.BytesNet.FormatSize(), _rightStats.BytesNet.FormatSize());
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
            ////////////////00000000011111111112222222222333333333344444444445555555555666666666677777777778
            ////////////////12345678901234567890123456789012345678901234567890123456789012345678901234567890
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
            Cout.WriteLine("  -left           - Left dir is the master (default).");
            Cout.WriteLine("  -right          - Right dir is the master.");
            Cout.WriteLine("  -both           - Both dirs are the master.");
            Cout.WriteLine("  -test           - Test only (do not copy files).");
            Cout.WriteLine("  -repdir <path>  - Specifies the report output directory.");
            Cout.WriteLine("  -launchrep      - Launch the report when complete.");

            return false;
        }

        private bool ProcessArguments(string[] args)
        {
            try
            {
                var rxArgs = new Regex(@"^(?:-|/)(\w+)$");
                Match match;
                var argList = new List<string>();
                var master = Master.Left;

                for (int a = 0; a < args.Length; a++)
                {
                    var arg = args[a];
                    if ((match = rxArgs.Match(arg)).Success)
                    {
                        var nextArg = a + 1 < args.Length ? args[a + 1] : null;

                        switch (match.Groups[1].Value.ToLower())
                        {
                            case "left":
                                master = Master.Left;
                                break;
                            case "right":
                                master = Master.Right;
                                break;
                            case "both":
                                master = Master.Both;
                                break;
                            case "launchrep":
                                _launchRep = true;
                                break;
                            case "repdir":
                                a++;
                                if (a >= args.Length) throw new ArgumentException(string.Format("Expected file name to follow '{0}'.", arg));
                                _repDir = Path.GetFullPath(args[a]);
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
                    var sync = new SyncDir
                    {
                        Left = argList[0],
                        Right = argList[1],
                        Master = master
                    };

                    if (!Directory.Exists(sync.Left)) throw new ArgumentException("Left path does not exist.");
                    if (!Directory.Exists(sync.Right)) throw new ArgumentException("Right path does not exist.");

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
            var fileContent = File.ReadAllText(_configFile);
            _config = JsonConvert.DeserializeObject<ConfigFile>(fileContent);
        }

        private static string _appDataDir;
        public static string AppDataDir
        {
            get
            {
                if (_appDataDir == null)
                {
                    _appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Res.AppNameIdent);
                    if (!Directory.Exists(_appDataDir)) Directory.CreateDirectory(_appDataDir);
                }
                return _appDataDir;
            }
        }

        private string GetRepFileName()
        {
            if (string.IsNullOrEmpty(_repDir))
            {
                _repDir = Path.Combine(AppDataDir, Res.ReportDirName);
                if (!Directory.Exists(_repDir)) Directory.CreateDirectory(_repDir);
            }

            var fileName = Path.Combine(_repDir, string.Format("SyncDirCmd Report {0}.htm", _startTime.ToString("yyyy-MM-dd HH.mm.ss")));
            var index = 0;
            while (File.Exists(fileName))
            {
                fileName = Path.Combine(_repDir, string.Format("SyncDirCmd Report {0} ({1}).htm", _startTime.ToString("yyyy-MM-dd HH.mm.ss"), ++index));
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

        public void LogError(Exception ex, string format, params object[] args)
        {
            var message = format;
            try
            {
                message = string.Format(format, args);
            }
            catch (Exception) { }

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
            Cout.WriteLine(Cout.NormalColor, $"{label}: {value}");
            _rep.WriteSummary(label, value);
        }

        private void LogSummary(string label, string leftValue, string rightValue)
        {
            Cout.WriteLine(Cout.NormalColor, $"{label}: {leftValue} - {rightValue}");
            _rep.WriteSummary(label, leftValue, rightValue);
        }

        private void StartSync(Database db, SyncDir sync)
        {
            try
            {
                switch (sync.Master)
                {
                    case Master.Left:
                        _rep.StartSection(string.Format("{0} -> {1}", sync.Left, sync.Right));

                        if (!Directory.Exists(sync.Left))
                        {
                            _rep.WriteError(string.Format("Folder on left does not exist: {0}", sync.Left));
                        }
                        else if (!Directory.Exists(sync.Right))
                        {
                            _rep.WriteError(string.Format("Folder on right does not exist: {0}", sync.Right));
                        }
                        else
                        {
                            OneMaster(db, sync, sync.Left, sync.Right);
                        }
                        _rep.EndSection();
                        break;
                    case Master.Right:
                        _rep.StartSection(string.Format("{0} <- {1}", sync.Left, sync.Right));

                        if (!Directory.Exists(sync.Left))
                        {
                            _rep.WriteError(string.Format("Folder on left does not exist: {0}", sync.Left));
                        }
                        else if (!Directory.Exists(sync.Right))
                        {
                            _rep.WriteError(string.Format("Folder on right does not exist: {0}", sync.Right));
                        }
                        else
                        {
                            OneMaster(db, sync, sync.Right, sync.Left);
                        }
                        _rep.EndSection();
                        break;
                    case Master.Both:
                        _rep.StartSection(string.Format("{0} <-> {1}", sync.Left, sync.Right));

                        if (!Directory.Exists(sync.Left))
                        {
                            _rep.WriteError(string.Format("Folder on left does not exist: {0}", sync.Left));
                        }
                        else if (!Directory.Exists(sync.Right))
                        {
                            _rep.WriteError(string.Format("Folder on right does not exist: {0}", sync.Right));
                        }
                        else
                        {
                            BothMaster(db, sync, sync.Left, sync.Right);
                        }
                        _rep.EndSection();
                        break;
                }
            }
            catch (Exception ex)
            {
                _rep.WriteError(ex, "Exception when running sync.");
            }
        }

        private void OneMaster(Database db, SyncDir sync, string masterPath, string mirrorPath)
        {
            using (var masterDb = new FileDb(db, masterPath))
            using (var mirrorDb = new FileDb(db, mirrorPath))
            using (var txn = db.BeginTransaction())
            {
                Cout.WriteLine($"{masterDb.BasePath} --> {mirrorDb.BasePath}");

                OneDir(new DirState(sync, masterDb, mirrorDb, ""));
                txn.Commit();
            }
        }

        private void OneDir(DirState state)
        {
            try
            {
                if (Directory.Exists(state.LeftAbsPath)) _leftStats.NumDirsAnalyzed++;

                if (Directory.Exists(state.RightAbsPath)) _rightStats.NumDirsAnalyzed++;
                else CreateDir(state.RelPath, state.RightAbsPath, Direction.LeftToRight, "Right directory does not exist.");

                state.LeftDb.UpdateDirectory(state.RelPath);
                state.RightDb.UpdateDirectory(state.RelPath);

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
                    _leftStats.NumFilesAnalyzed++;

                    var relFileName = state.GetRelFileName(fileName);
                    var leftAbsFileName = state.GetLeftAbsFileName(fileName);
                    var rightAbsFileName = state.GetRightAbsFileName(fileName);

                    if (!rightFiles.Any(f => f.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                    {
                        try
                        {
                            // File does not exist on the right.
                            if (CopyFile(relFileName, leftAbsFileName, rightAbsFileName, Direction.LeftToRight, "does not exist in mirror"))
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
                        _rightStats.NumFilesAnalyzed++;

                        var leftInfo = new FileInfo(leftAbsFileName);
                        var rightInfo = new FileInfo(rightAbsFileName);

                        var copy = false;
                        var reason = "";
                        if (state.LeftDb.HasFileChanged(relFileName, leftInfo, ref reason))
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
                            CopyFile(relFileName, leftAbsFileName, rightAbsFileName, Direction.LeftToRight, reason);
                        }
                    }

                    state.LeftDb.UpdateFile(relFileName);
                    state.RightDb.UpdateFile(relFileName);
                }

                // Find deleted files.
                foreach (var fileName in (from r in rightFiles where !leftFiles.Any(l => l.Equals(r, StringComparison.OrdinalIgnoreCase)) select r))
                {
                    _rightStats.NumFilesAnalyzed++;

                    var relFileName = state.GetRelFileName(fileName);

                    var reason = "";
                    if (state.LeftDb.FileExists(fileName)) reason = "file deleted in master";
                    else reason = "file does not exist in master";
                    DeleteFile(state.GetRelFileName(fileName), state.GetRightAbsFileName(fileName), Direction.LeftToRight, reason);

                    state.LeftDb.DeleteFile(relFileName);
                    state.RightDb.DeleteFile(relFileName);
                }

                // Find new directories, and recurse into existing ones.
                foreach (var dirName in leftDirs)
                {
                    if (!rightDirs.Any(f => f.Equals(dirName, StringComparison.OrdinalIgnoreCase)))
                    {
                        // Directory does not exist on the right.
                        CreateDir(state.GetRelFileName(dirName), state.GetRightAbsFileName(dirName), Direction.LeftToRight, "new directory");
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
                    if (state.LeftDb.DirectoryExists(relDirName)) reason = "directory deleted in master";
                    else reason = "directory does not exist in master";

                    DeleteDir(relDirName, state.GetRightAbsFileName(dirName), Direction.LeftToRight, reason);

                    state.LeftDb.DeleteDirectory(relDirName);
                    state.RightDb.DeleteDirectory(relDirName);
                }
            }
            catch (Exception ex)
            {
                LogError(ex, string.Format("Error when processing directory '{0}'.", state.RelPath));
            }
        }

        private void BothMaster(Database db, SyncDir sync, string leftPath, string rightPath)
        {
            using (var leftDb = new FileDb(db, leftPath))
            using (var rightDb = new FileDb(db, rightPath))
            using (var txn = db.BeginTransaction())
            {
                Cout.WriteLine($"{leftDb.BasePath} <--> {rightDb.BasePath}");

                BothDir(new DirState(sync, leftDb, rightDb, ""));
                txn.Commit();
            }
        }

        private void BothDir(DirState state)
        {
            try
            {
                if (Directory.Exists(state.LeftAbsPath)) _leftStats.NumDirsAnalyzed++;
                else CreateDir(state.RelPath, state.LeftAbsPath, Direction.RightToLeft, "Left directory does not exist.");

                if (Directory.Exists(state.RightAbsPath)) _rightStats.NumDirsAnalyzed++;
                else CreateDir(state.RelPath, state.RightAbsPath, Direction.LeftToRight, "Right directory does not exist.");

                state.LeftDb.UpdateDirectory(state.RelPath);
                state.RightDb.UpdateDirectory(state.RelPath);

                string[] leftFiles, leftDirs, rightFiles, rightDirs;

                if (Directory.Exists(state.LeftAbsPath))
                {
                    leftFiles = GetUnignoredFileNamesInDirectory(state, state.LeftAbsPath).ToArray();
                    leftDirs = GetUnignoredDirectoryNamesInDirectory(state, state.LeftAbsPath).ToArray();
                }
                else throw new DirectoryNotFoundException($"Directory '{state.LeftAbsPath}' no longer exists.");

                if (Directory.Exists(state.RightAbsPath))
                {
                    rightFiles = GetUnignoredFileNamesInDirectory(state, state.RightAbsPath).ToArray();
                    rightDirs = GetUnignoredDirectoryNamesInDirectory(state, state.RightAbsPath).ToArray();
                }
                else throw new DirectoryNotFoundException($"Directory '{state.RightAbsPath}' no longer exists.");

                // Files that don't exist on right.
                foreach (var fileName in leftFiles)
                {
                    _leftStats.NumFilesAnalyzed++;

                    var leftAbsFileName = state.GetLeftAbsFileName(fileName);
                    var rightAbsFileName = state.GetRightAbsFileName(fileName);
                    var relFileName = state.GetRelFileName(fileName);

                    if (!rightFiles.Any(r => r.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                    {
                        // File is on left only.
                        if (state.RightDb.FileExists(relFileName))
                        {
                            DeleteFile(relFileName, leftAbsFileName, Direction.RightToLeft, "file deleted on right");
                        }
                        else
                        {
                            CopyFile(relFileName, leftAbsFileName, rightAbsFileName, Direction.LeftToRight, "file new on left");
                        }
                    }
                    else
                    {
                        // File exists on both sides.
                        _rightStats.NumFilesAnalyzed++;

                        var leftInfo = new FileInfo(leftAbsFileName);
                        var rightInfo = new FileInfo(rightAbsFileName);
                        var leftReason = "";
                        var rightReason = "";
                        var leftChanged = state.LeftDb.HasFileChanged(relFileName, leftInfo, ref leftReason);
                        var rightChanged = state.RightDb.HasFileChanged(relFileName, rightInfo, ref rightReason);

                        if (leftChanged && !rightChanged)
                        {
                            // User updated the left file.
                            CopyFile(relFileName, leftAbsFileName, rightAbsFileName, Direction.LeftToRight, string.Concat("left file: ", leftReason));
                        }
                        else if (!leftChanged && rightChanged)
                        {
                            // User updated the right file.
                            CopyFile(relFileName, rightAbsFileName, leftAbsFileName, Direction.RightToLeft, string.Concat("right file: ", rightReason));
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
                                    CopyFile(relFileName, leftAbsFileName, rightAbsFileName, Direction.LeftToRight, "left file modified date is newer");
                                }
                                else if (leftInfo.LastWriteTime < rightInfo.LastWriteTime)
                                {
                                    // Right file is newer.
                                    CopyFile(relFileName, rightAbsFileName, leftAbsFileName, Direction.RightToLeft, "right file modified date is newer");
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

                    _rightStats.NumFilesAnalyzed++;

                    var leftAbsFileName = state.GetLeftAbsFileName(fileName);
                    var rightAbsFileName = state.GetRightAbsFileName(fileName);
                    var relFileName = state.GetRelFileName(fileName);

                    if (state.LeftDb.FileExists(relFileName))
                    {
                        if (DeleteFile(relFileName, rightAbsFileName, Direction.LeftToRight, "file deleted on left"))
                        {
                            state.LeftDb.DeleteFile(relFileName);
                            state.RightDb.DeleteFile(relFileName);
                        }
                    }
                    else
                    {
                        CopyFile(relFileName, rightAbsFileName, leftAbsFileName, Direction.RightToLeft, "file new on right");
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
                        if (state.RightDb.DirectoryExists(relDirName))
                        {
                            if (DeleteDir(relDirName, state.GetLeftAbsFileName(dirName), Direction.RightToLeft, "directory on right deleted by user"))
                            {
                                state.RightDb.DeleteDirectory(relDirName);
                                state.LeftDb.DeleteDirectory(relDirName);
                            }
                        }
                        else
                        {
                            if (CreateDir(relDirName, state.GetRightAbsFileName(dirName), Direction.LeftToRight, "directory on left new"))
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

                    if (state.LeftDb.DirectoryExists(relDirName))
                    {
                        if (DeleteDir(relDirName, state.GetRightAbsFileName(dirName), Direction.LeftToRight, "directory on left deleted by user"))
                        {
                            state.RightDb.DeleteDirectory(relDirName);
                            state.LeftDb.DeleteDirectory(relDirName);
                        }
                    }
                    else
                    {
                        if (CreateDir(relDirName, state.GetLeftAbsFileName(dirName), Direction.RightToLeft, "directory on right new"))
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

        private bool CopyFile(string relFileName, string srcFileName, string dstFileName, Direction direction, string reason)
        {
            try 
            {
                var fi = new FileInfo(srcFileName);

                var oldSize = 0L;
                if (File.Exists(dstFileName))
                {
                    oldSize = new FileInfo(dstFileName).Length;
                }

                _rep.WriteFileOperation("Copy File", relFileName, fi.Length, reason);
                Cout.WriteLine(Cout.ImportantColor, $"Copy File: {(direction == Direction.LeftToRight ? "-->" : "<--")} {relFileName}");

                if (!_test)
                {
                    CheckFileAttributes(dstFileName);
                    File.Copy(srcFileName, dstFileName, true);
                }

                switch (direction)
                {
                    case Direction.LeftToRight:
                        _rightStats.NumFilesCopied++;
                        _rightStats.BytesCopied += fi.Length;
                        _rightStats.BytesNet += fi.Length - oldSize;
                        break;
                    case Direction.RightToLeft:
                        _leftStats.NumFilesCopied++;
                        _leftStats.BytesCopied += fi.Length;
                        _leftStats.BytesNet += fi.Length - oldSize;
                        break;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogError(ex, "Error when copying file '{0}' to '{0}'", srcFileName, dstFileName);
                return false;
            }
        }

        private bool CreateDir(string relPath, string absPath, Direction direction, string reason)
        {
            try
            {
                _rep.WriteFileOperation("Create Dir", relPath, null, reason);
                Cout.WriteLine(Cout.ImportantColor, $"Create Dir: {(direction == Direction.LeftToRight ? "-->" : "<--")} {relPath}");

                if (!_test) Directory.CreateDirectory(absPath);

                switch (direction)
                {
                    case Direction.LeftToRight:
                        _rightStats.NumDirsCreated++;
                        break;
                    case Direction.RightToLeft:
                        _leftStats.NumDirsCreated++;
                        break;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogError(ex, "Error when creating directory.");
                return false;
            }
        }

        private bool DeleteFile(string relFileName, string absFileName, Direction direction, string reason)
        {
            try
            {
                var fi = new FileInfo(absFileName);
                var length = fi.Length;

                _rep.WriteFileOperation("Delete File", relFileName, fi.Length, reason);
                Cout.WriteLine(Cout.ImportantColor, $"Delete File: {(direction == Direction.LeftToRight ? "-->" : "<--")} {relFileName}");

                if (!_test)
                {
                    CheckFileAttributes(absFileName);
                    File.Delete(absFileName);
                }

                switch (direction)
                {
                    case Direction.LeftToRight:
                        _rightStats.NumFilesDeleted++;
                        _rightStats.BytesDeleted += length;
                        _rightStats.BytesNet -= length;
                        break;
                    case Direction.RightToLeft:
                        _leftStats.NumFilesDeleted++;
                        _leftStats.BytesDeleted += length;
                        _leftStats.BytesNet -= length;
                        break;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogError(ex, "Error when deleting file.");
                return false;
            }
        }

        private bool DeleteDir(string relPath, string absPath, Direction direction, string reason)
        {
            try
            {
                
                Cout.WriteLine(Cout.ImportantColor, $"Delete Dir: {(direction == Direction.LeftToRight ? "-->" : "<--")} {relPath}");

                long bytesDeleted = 0;

                if (!_test) DeleteDir_Sub(absPath, direction, ref bytesDeleted);

                _rep.WriteFileOperation("Delete Dir", relPath, bytesDeleted, reason);
                return true;
            }
            catch (Exception ex)
            {
                _rep.WriteFileOperation("Delete Dir", relPath, null, reason);
                LogError(ex, "Error when deleting directory.");
                return false;
            }
        }

        private void DeleteDir_Sub(string path, Direction direction, ref long bytesDeleted)
        {
            foreach (var fileName in Directory.GetFiles(path))
            {
                var length = (new FileInfo(fileName)).Length;

                CheckFileAttributes(fileName);
                File.Delete(fileName);

                switch (direction)
                {
                    case Direction.LeftToRight:
                        _rightStats.NumFilesDeleted++;
                        _rightStats.BytesDeleted += length;
                        _rightStats.BytesNet -= length;
                        break;
                    case Direction.RightToLeft:
                        _leftStats.NumFilesDeleted++;
                        _leftStats.BytesDeleted += length;
                        _leftStats.BytesNet -= length;
                        break;
                }

                bytesDeleted += length;
            }

            foreach (var dirName in Directory.GetDirectories(path))
            {
                DeleteDir_Sub(dirName, direction, ref bytesDeleted);
            }

            var di = new DirectoryInfo(path);
            if ((di.Attributes & FileAttributes.ReadOnly) != 0) di.Attributes &= ~FileAttributes.ReadOnly;
            Directory.Delete(path);

            switch (direction)
            {
                case Direction.LeftToRight:
                    _rightStats.NumDirsDeleted++;
                    break;
                case Direction.RightToLeft:
                    _leftStats.NumDirsDeleted++;
                    break;
            }
        }

        private void CheckFileAttributes(string fileName)
        {
            if (File.Exists(fileName))
            {
                var attribs = File.GetAttributes(fileName);
                if ((attribs & FileAttributes.ReadOnly) != 0)
                {
                    attribs &= ~FileAttributes.ReadOnly;
                    File.SetAttributes(fileName, attribs);
                }
            }
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
