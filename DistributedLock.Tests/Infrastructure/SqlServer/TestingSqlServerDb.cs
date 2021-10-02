﻿using Medallion.Threading.Internal;
using Medallion.Threading.Tests.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading.Tasks;

namespace Medallion.Threading.Tests.SqlServer
{
    public interface ITestingSqlServerDb : ITestingDb { }

    public sealed class TestingSqlServerDb : ITestingSqlServerDb, ITestingPrimaryClientDb
    {
        internal static readonly string ConnectionString = SqlServerCredentials.ConnectionString;

        private readonly Microsoft.Data.SqlClient.SqlConnectionStringBuilder _connectionStringBuilder =
            new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(ConnectionString);

        public DbConnectionStringBuilder ConnectionStringBuilder => this._connectionStringBuilder;

        public int MaxPoolSize { get => this._connectionStringBuilder.MaxPoolSize; set => this._connectionStringBuilder.MaxPoolSize = value; }

        // https://stackoverflow.com/questions/5808332/sql-server-maximum-character-length-of-object-names/41502228
        public int MaxApplicationNameLength => 128;

        public TransactionSupport TransactionSupport => TransactionSupport.TransactionScoped;

        public void ClearPool(DbConnection connection) => Microsoft.Data.SqlClient.SqlConnection.ClearPool((Microsoft.Data.SqlClient.SqlConnection)connection);

        public int CountActiveSessions(string applicationName)
        {
            Invariant.Require(applicationName.Length <= this.MaxApplicationNameLength);

            using var connection = new Microsoft.Data.SqlClient.SqlConnection(ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $@"SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE program_name = @applicationName";
            command.Parameters.AddWithValue("applicationName", applicationName);
            return (int)command.ExecuteScalar();
        }

        public IsolationLevel GetIsolationLevel(DbConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT CASE transaction_isolation_level
                    WHEN 0 THEN 'Unspecified'
                    WHEN 1 THEN 'ReadUncommitted'
                    WHEN 2 THEN 'ReadCommitted'
                    WHEN 3 THEN 'RepeatableRead'
                    WHEN 4 THEN 'Serializable'
                    WHEN 5 THEN 'Snapshot'
                    ELSE 'Unknown' END AS isolationLevel
                FROM sys.dm_exec_sessions
                WHERE session_id = @@SPID";
            return (IsolationLevel)Enum.Parse(typeof(IsolationLevel), (string)command.ExecuteScalar());
        }

        public DbConnection CreateConnection() => new Microsoft.Data.SqlClient.SqlConnection(this.ConnectionStringBuilder.ConnectionString);

        public async Task KillSessionsAsync(string applicationName, DateTimeOffset? idleSince)
        {
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(ConnectionString);
            await connection.OpenAsync();

            var findIdleSessionsCommand = connection.CreateCommand();
            findIdleSessionsCommand.CommandText = @"
                SELECT session_id FROM sys.dm_exec_sessions
                WHERE session_id != @@SPID
                    AND program_name = @applicationName
                    AND (
                        @idleSince IS NULL
                        OR (    
                            (last_request_start_time IS NULL OR last_request_start_time <= @idleSince)
                            AND (last_request_end_time IS NULL OR last_request_end_time <= @idleSince)
                        )
                    )";
            findIdleSessionsCommand.Parameters.AddWithValue("applicationName", applicationName);
            findIdleSessionsCommand.Parameters.AddWithValue("idleSince", idleSince?.DateTime ?? DBNull.Value.As<object>()).SqlDbType = SqlDbType.DateTime;

            var spidsToKill = new List<short>();
            using (var idleSessionsReader = await findIdleSessionsCommand.ExecuteReaderAsync())
            {
                while (await idleSessionsReader.ReadAsync())
                {
                    spidsToKill.Add(idleSessionsReader.GetInt16(0));
                }
            }

            foreach (var spid in spidsToKill)
            {
                using var killCommand = connection.CreateCommand();
                killCommand.CommandText = "KILL " + spid;
                try { await killCommand.ExecuteNonQueryAsync(); }
                catch (Exception ex) { Console.WriteLine($"Failed to kill {spid}: {ex}"); }
            }
        }
    }

    public sealed class TestingSystemDataSqlServerDb : ITestingSqlServerDb
    {
        private readonly System.Data.SqlClient.SqlConnectionStringBuilder _connectionStringBuilder =
            new System.Data.SqlClient.SqlConnectionStringBuilder(TestingSqlServerDb.ConnectionString);

        public DbConnectionStringBuilder ConnectionStringBuilder => this._connectionStringBuilder;

        public int MaxPoolSize { get => this._connectionStringBuilder.MaxPoolSize; set => this._connectionStringBuilder.MaxPoolSize = value; }

        public int MaxApplicationNameLength => new TestingSqlServerDb().MaxApplicationNameLength;

        public TransactionSupport TransactionSupport => TransactionSupport.TransactionScoped;

        public void ClearPool(DbConnection connection) => System.Data.SqlClient.SqlConnection.ClearPool((System.Data.SqlClient.SqlConnection)connection);

        public int CountActiveSessions(string applicationName) => new TestingSqlServerDb().CountActiveSessions(applicationName);

        public IsolationLevel GetIsolationLevel(DbConnection connection) => new TestingSqlServerDb().GetIsolationLevel(connection);

        public DbConnection CreateConnection() => new System.Data.SqlClient.SqlConnection(this.ConnectionStringBuilder.ConnectionString);
    }
}
