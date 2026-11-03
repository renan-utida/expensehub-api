using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Deployment;

/// <summary>
/// Finds and reads the files of the delivery (Dockerfiles, Compose file, example environment file and workflow) from the root of
/// the repository, so the tests can check their rules as text. Nothing is executed and no Docker is needed.
/// </summary>
internal static class DeliveryFiles
{
    /// <summary>The path of the Compose file.</summary>
    public const string Compose = "compose.yaml";

    /// <summary>The path of the example environment file.</summary>
    public const string EnvExample = ".env.example";

    /// <summary>The path of the ignore file of the build context.</summary>
    public const string DockerIgnore = ".dockerignore";

    /// <summary>The path of the Dockerfile of the web application.</summary>
    public const string WebDockerfile = "sources/ExpenseHub.Api/Dockerfile";

    /// <summary>The path of the Dockerfile of the worker.</summary>
    public const string WorkerDockerfile = "sources/ExpenseHub.Worker/Dockerfile";

    /// <summary>The path of the workflow that publishes the images.</summary>
    public const string Workflow = ".github/workflows/publish-images.yml";

    /// <summary>
    /// Reads a file of the delivery. The test is reported as inconclusive, not as failed, when the repository root cannot be found
    /// (for example when only the compiled tests were copied somewhere else).
    /// </summary>
    /// <param name="relativePath">The path from the root of the repository, with forward slashes.</param>
    /// <returns>The text of the file.</returns>
    public static string Read(string relativePath)
    {
        string? root = FindRoot();

        if (root is null)
        {
            Assert.Inconclusive("The root of the repository was not found from the test output.");
        }

        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.IsTrue(File.Exists(path), $"The delivery file '{relativePath}' does not exist.");

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Returns the lines of a file without the comment lines, so a rule such as "latest is never used" is not tripped by a comment
    /// that explains it.
    /// </summary>
    /// <param name="text">The text of the file.</param>
    /// <returns>The lines that are not comments.</returns>
    public static IReadOnlyList<string> CodeLines(string text)
    {
        return text
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !line.TrimStart().StartsWith('#'))
            .ToList();
    }

    /// <summary>
    /// Returns the lines of one service of the Compose file, from its name (two spaces of indentation) to the next service or
    /// top-level key.
    /// </summary>
    /// <param name="compose">The text of the Compose file.</param>
    /// <param name="service">The name of the service.</param>
    /// <returns>The lines of the service, without the comment lines.</returns>
    public static IReadOnlyList<string> ServiceLines(string compose, string service)
    {
        IReadOnlyList<string> lines = CodeLines(compose);
        int start = lines.ToList().FindIndex(line => line == $"  {service}:");

        Assert.IsGreaterThanOrEqualTo(0, start, $"The service '{service}' is not in the Compose file.");

        var block = new List<string>();

        for (int index = start + 1; index < lines.Count; index++)
        {
            string line = lines[index];
            bool endsBlock = line.Length > 0 && !char.IsWhiteSpace(line[0]);
            bool nextService = line.StartsWith("  ", StringComparison.Ordinal) && line.Length > 2 && !char.IsWhiteSpace(line[2]);

            if (endsBlock || nextService)
            {
                break;
            }

            block.Add(line);
        }

        return block;
    }

    private static string? FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            // The solution file marks the root. A missing delivery file must fail its test, so it is not part of the search.
            if (File.Exists(Path.Combine(directory.FullName, "sources", "ExpenseHub.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
