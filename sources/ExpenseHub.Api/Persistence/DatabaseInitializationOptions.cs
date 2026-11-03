namespace ExpenseHub.Api.Persistence;

/// <summary>
/// How the database is prepared when the web application starts, read from the <c>Database</c> section of the configuration.
/// </summary>
public sealed class DatabaseInitializationOptions
{
    /// <summary>The name of the section of the configuration.</summary>
    public const string SectionName = "Database";

    /// <summary>
    /// Gets or sets a value indicating whether the web application applies the pending migrations when it starts. It is off by
    /// default, so the behavior of the CP2 does not change and <c>dotnet ef database update</c> stays the way to create the
    /// database locally. The Compose file turns it on, so a new volume gets its schema without another image. Only the web
    /// application migrates; the worker never does.
    /// </summary>
    public bool MigrateOnStartup { get; set; }
}
