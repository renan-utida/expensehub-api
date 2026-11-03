namespace ExpenseHub.Api.Deployment;

/// <summary>
/// Where the keys of the data protection are kept, read from the <c>DataProtection</c> section of the configuration. The bearer
/// tokens are protected with these keys, so they must survive the recreation of a container for the tokens to stay valid.
/// </summary>
public sealed class KeyStorageOptions
{
    /// <summary>The name of the section of the configuration.</summary>
    public const string SectionName = "DataProtection";

    /// <summary>
    /// Gets or sets the directory that holds the keys. When it is empty nothing changes and ASP.NET Core keeps its default
    /// location. In the containers it is a directory on a named volume. The keys are never versioned.
    /// </summary>
    public string? KeysPath { get; set; }
}
