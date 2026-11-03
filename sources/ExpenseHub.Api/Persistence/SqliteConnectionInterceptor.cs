using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ExpenseHub.Api.Persistence;

/// <summary>
/// Runs the settings of <see cref="SqlitePragmas"/> on every connection right after it is opened: the busy timeout first, then the
/// write-ahead log. The busy timeout belongs to the connection, so it has to be set every time; the write-ahead log stays in the
/// database file, and asking for it again on a database that already has it changes nothing.
/// </summary>
public sealed class SqliteConnectionInterceptor : DbConnectionInterceptor
{
    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using DbCommand command = connection.CreateCommand();
        command.CommandText = SqlitePragmas.OnOpenScript;
        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = SqlitePragmas.OnOpenScript;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
