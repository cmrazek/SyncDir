using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncDirCmd
{
	internal class Database : IDisposable
	{
		private SQLiteConnection _conn;

		public Database()
		{
			var dbFileName = Path.Combine(Program.AppDataDir, Res.DatabaseFileName);
			var newFile = false;
			if (!File.Exists(dbFileName))
			{
				SQLiteConnection.CreateFile(dbFileName);
				newFile = true;
			}

			_conn = new SQLiteConnection($"Data Source={dbFileName}; Version=3;");
			_conn.Open();

			if (newFile)
			{
				var cmd = _conn.CreateCommand();
				cmd.CommandType = CommandType.Text;
				cmd.CommandText = @"
create table base_path (
	path	varchar(1000) not null collate nocase
);
create unique index base_path_ix_path on base_path (path);
create table rel_file (
	base_path_id	bigint			not null collate nocase,
	rel_path		varchar(1000)	not null collate nocase,
	modified		datetime		not null,
	file_size		bigint			not null,
	dir				tinyint			not null
);
create unique index file_ix_rel_path on rel_file (base_path_id, rel_path);
";
				cmd.ExecuteNonQuery();
			}
		}

		public void Dispose()
		{
			if (_conn != null)
			{
				try
				{
					_conn.Close();
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine(ex.ToString());
				}
				finally
				{
					_conn = null;
				}
			}
		}

		private SQLiteCommand CreateCommand(string sql, params object[] args)
		{
			if (_conn == null) throw new InvalidOperationException("Not connected to database.");
			if (args.Length % 2 != 0) throw new ArgumentException("Arguments must be a multiple of 2, for key/value pairs");

			var cmd = _conn.CreateCommand();
			cmd.CommandType = CommandType.Text;
			cmd.CommandText = sql;

			for (int i = 0; i < args.Length; i += 2)
			{
				if (args[i] == null) throw new ArgumentNullException($"args[{i}]");
				cmd.Parameters.AddWithValue(args[i].ToString(), args[i + 1]);
			}

			return cmd;
		}

		public long GetBasePathId(string path)
		{
			using (var cmd = CreateCommand("select rowid from base_path where path = @path", "@path", path))
			{
				var rdr = cmd.ExecuteReader(CommandBehavior.SingleRow);
				if (rdr.Read())
				{
					return rdr.GetInt64(0);
				}
			}

			using (var cmd = CreateCommand("insert into base_path (path) values (@path); select last_insert_rowid();", "@path", path))
			{
				return (long)cmd.ExecuteScalar();
			}
		}

		public void UpdateFile(long basePathId, string relPathName, DateTime modified, long size, bool dir)
		{
			int numResults;
			using (var cmd = CreateCommand(@"
update rel_file
set modified = @modified, file_size = @file_size, dir = @dir
where base_path_id = @base_path_id
and rel_path = @rel_path",
					"@base_path_id", basePathId,
					"@rel_path", relPathName,
					"@modified", modified,
					"@file_size", size,
					"@dir", dir ? 1 : 0))
			{
				numResults = cmd.ExecuteNonQuery();
			}

			if (numResults == 0)
			{
				using (var cmd = CreateCommand(@"
insert into rel_file (base_path_id, rel_path, modified, file_size, dir)
values (@base_path_id, @rel_path, @modified, @file_size, @dir)",
						"@base_path_id", basePathId,
						"@rel_path", relPathName,
						"@modified", modified,
						"@file_size", size,
						"@dir", dir ? 1 : 0))
				{
					cmd.ExecuteNonQuery();
				}
			}
		}

		public void RemoveFile(long basePathId, string relPathName)
		{
			using (var cmd = CreateCommand(@"delete from rel_file where base_path_id = @base_path_id and rel_path = @rel_path",
				"@base_path_id", basePathId,
				"@rel_path", relPathName))
			{
				cmd.ExecuteNonQuery();
			}
		}

		public FileEntry GetFile(long basePathId, string relPathName)
		{
			using (var cmd = CreateCommand(@"
select rowid, base_path_id, rel_path, modified, file_size, dir from rel_file
where base_path_id = @base_path_id and rel_path = @rel_path",
				"@base_path_id", basePathId,
				"@rel_path", relPathName))
			using (var rdr = cmd.ExecuteReader(CommandBehavior.SingleRow))
			{
				if (rdr.Read())
				{
					return new FileEntry(rdr.GetInt64(0), rdr.GetInt64(1), rdr.GetString(2), rdr.GetDateTime(3),
						rdr.GetInt64(4), rdr.GetInt32(5) != 0);
				}
			}

			return null;
		}

		public void RemoveAllFiles(long basePathId)
		{
			using (var cmd = CreateCommand("delete from rel_file where base_path_id = @base_path_id", "@base_path_id", basePathId))
			{
				cmd.ExecuteNonQuery();
			}
		}

		public void RemoveAllFilesInDir(long basePathId, string relDirName)
		{
			var filesToRemove = new List<long>();

			using (var cmd = CreateCommand(@"
select rowid, rel_path from rel_file
where base_path_id = @base_path_id and rel_path like '" + relDirName + "%'",
				"@base_path_id", basePathId))
			using (var rdr = cmd.ExecuteReader())
			{
				while (rdr.Read())
				{
					if (FileDb.PathIsInDir(rdr.GetString(1), relDirName))
					{
						filesToRemove.Add(rdr.GetInt64(0));
					}
				}
			}

			using (var txn = _conn.BeginTransaction())
			using (var cmd = _conn.CreateCommand())
			{
				cmd.CommandType = CommandType.Text;
				cmd.CommandText = "delete from rel_file where rowid = @rowid";

				foreach (var rowid in filesToRemove)
				{
					cmd.Parameters.Clear();
					cmd.Parameters.AddWithValue("@rowid", rowid);
					cmd.ExecuteNonQuery();
				}

				txn.Commit();
			}
		}

		public SQLiteTransaction BeginTransaction()
		{
			return _conn.BeginTransaction();
		}
	}
}
