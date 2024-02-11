using Medallion.Threading.Internal;
using Medallion.Threading.Internal.Data;
using System.Data;

namespace Medallion.Threading.SqlServer;

/// <summary>
/// Implements a distributed lock using a SQL server application lock
/// (see https://msdn.microsoft.com/en-us/library/ms189823.aspx)
/// </summary>
public sealed partial class SqlDistributedLock : IInternalDistributedLock<SqlDistributedLockHandle>
{
    private readonly IDbDistributedLock _internalLock;

    /// <summary>
    /// Constructs a new lock using the provided <paramref name="name"/>. 
    /// 
    /// The provided <paramref name="connectionString"/> will be used to connect to the database.
    /// 
    /// Unless <paramref name="exactName"/> is specified, <paramref name="name"/> will be escaped/hashed to ensure name validity.
    /// </summary>
    public SqlDistributedLock(string name, string connectionString, Action<SqlConnectionOptionsBuilder>? options = null, bool exactName = false)
        : this(name, exactName, n => CreateInternalLock(n, connectionString, options))
    {
    }

    /// <summary>
    /// Constructs a new lock using the provided <paramref name="name"/>.
    /// 
    /// The provided <paramref name="connection"/> will be used to connect to the database and will provide lock scope. It is assumed to be externally managed and
    /// will not be opened or closed.
    /// 
    /// Unless <paramref name="exactName"/> is specified, <paramref name="name"/> will be escaped/hashed to ensure name validity.
    /// </summary>
    public SqlDistributedLock(string name, IDbConnection connection, bool exactName = false)
        : this(name, exactName, n => CreateInternalLock(n, connection))
    {
    }

    /// <summary>
    /// Constructs a new lock using the provided <paramref name="name"/>.
    /// 
    /// The provided <paramref name="transaction"/> will be used to connect to the database and will provide lock scope. It is assumed to be externally managed and
    /// will not be committed or rolled back.
    /// 
    /// Unless <paramref name="exactName"/> is specified, <paramref name="name"/> will be escaped/hashed to ensure name validity.
    /// </summary>
    public SqlDistributedLock(string name, IDbTransaction transaction, bool exactName = false)
        : this(name, exactName, n => CreateInternalLock(n, transaction))
    {
    }

    private SqlDistributedLock(string name, bool exactName, Func<string, IDbDistributedLock> internalLockFactory)
    {
        if (exactName)
        {
            if (name == null) { throw new ArgumentNullException(nameof(name)); }
            if (name.Length > MaxNameLength) { throw new FormatException($"{nameof(name)}: must be at most {MaxNameLength} characters"); }
            this.Name = name;
        }
        else
        {
            this.Name = GetSafeName(name);
        }

        this._internalLock = internalLockFactory(this.Name);
    }

    /// <summary>
    /// The maximum allowed length for lock names. See https://msdn.microsoft.com/en-us/library/ms189823.aspx
    /// </summary>
    internal static int MaxNameLength => 255;

    /// <summary>
    /// Implements <see cref="IDistributedLock.Name"/>
    /// </summary>
    public string Name { get; }

    internal static string GetSafeName(string name) =>
        DistributedLockHelpers.ToSafeName(name, MaxNameLength, s => s);

    ValueTask<SqlDistributedLockHandle?> IInternalDistributedLock<SqlDistributedLockHandle>.InternalTryAcquireAsync(TimeoutValue timeout, CancellationToken cancellationToken) =>
        this._internalLock.TryAcquireAsync(timeout, SqlApplicationLock.ExclusiveLock, cancellationToken, contextHandle: null).Wrap(h => new SqlDistributedLockHandle(h));

    internal static IDbDistributedLock CreateInternalLock(string name, string connectionString, Action<SqlConnectionOptionsBuilder>? optionsBuilder)
    {
        if (connectionString == null) { throw new ArgumentNullException(nameof(connectionString)); }

        var (keepaliveCadence, useTransaction, useMultiplexing) = SqlConnectionOptionsBuilder.GetOptions(optionsBuilder);

        return useMultiplexing
            ? new OptimisticConnectionMultiplexingDbDistributedLock(name, connectionString, SqlMultiplexedConnectionLockPool.Instance, keepaliveCadence)
            : new DedicatedConnectionOrTransactionDbDistributedLock(name, () => new SqlDatabaseConnection(connectionString), useTransaction: useTransaction, keepaliveCadence);
    }

    internal static IDbDistributedLock CreateInternalLock(string name, IDbConnection connection)
    {
        return connection == null
            ? throw new ArgumentNullException(nameof(connection))
            : new DedicatedConnectionOrTransactionDbDistributedLock(name, () => new SqlDatabaseConnection(connection));
    }

    internal static IDbDistributedLock CreateInternalLock(string name, IDbTransaction transaction)
    {
        return transaction == null
            ? throw new ArgumentNullException(nameof(transaction))
            : new DedicatedConnectionOrTransactionDbDistributedLock(name, () => new SqlDatabaseConnection(transaction));
    }
}
