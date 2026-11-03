namespace ExpenseHub.Api.Deployment;

/// <summary>
/// Information about the build that is running, read from the <c>Build</c> section of the configuration. In a container the
/// commit comes from the environment variable <c>Build__Commit</c>, which the Dockerfiles set from the commit the image was
/// built from.
/// </summary>
public sealed class BuildInfoOptions
{
    /// <summary>The name of the section of the configuration.</summary>
    public const string SectionName = "Build";

    /// <summary>Gets or sets the commit the running image was built from. It is not trusted: see <see cref="RunningVersion"/>.</summary>
    public string? Commit { get; set; }
}
