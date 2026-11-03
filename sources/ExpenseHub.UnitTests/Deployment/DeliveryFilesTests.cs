using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Deployment;

/// <summary>
/// Tests of the rules of the delivery files, read as text: the images are identified by the commit and never by "latest", the
/// web application and the worker share the same bases, the same database file and the same volume, nothing secret is written in
/// a versioned file, and the images are published only after the tests pass. Nothing is built or run here; the images and the
/// Compose file were exercised by hand with Docker.
/// </summary>
[TestClass]
public sealed class DeliveryFilesTests
{
    private static readonly string[] _requiredVariables = ["IMAGE_TAG", "RABBITMQ_USER", "RABBITMQ_PASSWORD", "ADMIN_EMAIL", "ADMIN_PASSWORD"];

    private static readonly string[] _blankVariables = ["IMAGE_TAG", "RABBITMQ_USER", "RABBITMQ_PASSWORD", "ADMIN_EMAIL", "ADMIN_PASSWORD"];

    /// <summary>The images of the web application and of the worker take their tag from IMAGE_TAG, with no value written in the file.</summary>
    [TestMethod]
    public void Compose_ImagesTakeTheirTagFromTheImageTagVariable()
    {
        string compose = DeliveryFiles.Read(DeliveryFiles.Compose);

        foreach (string service in new[] { "web", "worker" })
        {
            string image = DeliveryFiles.ServiceLines(compose, service).Single(line => line.TrimStart().StartsWith("image:", StringComparison.Ordinal));

            Assert.IsTrue(
                Regex.IsMatch(image, "^    image: \"ghcr\\.io/renan-utida/expensehub-(web|worker):\\$\\{IMAGE_TAG:\\?[^}]+\\}\"$"),
                $"The image of the service '{service}' must be ghcr.io/renan-utida/expensehub-<name>:${{IMAGE_TAG:?...}}.");
        }
    }

    /// <summary>No active line of the Compose file mentions the tag latest: only the commit identifies the version.</summary>
    [TestMethod]
    public void Compose_NeverUsesTheLatestTag()
    {
        string compose = DeliveryFiles.Read(DeliveryFiles.Compose);

        foreach (string line in DeliveryFiles.CodeLines(compose))
        {
            Assert.DoesNotContain("latest", line, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>RabbitMQ is pinned to the version that was validated, by tag and by digest, with the management panel.</summary>
    [TestMethod]
    public void Compose_PinsRabbitMqByVersionAndDigest()
    {
        string compose = DeliveryFiles.Read(DeliveryFiles.Compose);

        string image = DeliveryFiles.ServiceLines(compose, "rabbitmq").Single(line => line.TrimStart().StartsWith("image:", StringComparison.Ordinal));

        Assert.IsTrue(
            Regex.IsMatch(image, "^    image: rabbitmq:4\\.4\\.0-management@sha256:[0-9a-f]{64}$"),
            "RabbitMQ must be pinned as rabbitmq:4.4.0-management@sha256:<digest>.");
    }

    /// <summary>The broker keeps its data in a named volume, has a fixed host name (so the data is found again) and a health check.</summary>
    [TestMethod]
    public void Compose_RabbitMqHasAVolumeAFixedHostNameAndAHealthCheck()
    {
        string compose = DeliveryFiles.Read(DeliveryFiles.Compose);
        IReadOnlyList<string> broker = DeliveryFiles.ServiceLines(compose, "rabbitmq");

        Assert.IsTrue(broker.Contains("      - rabbitmq-data:/var/lib/rabbitmq"), "The data of the broker must be on a named volume.");
        Assert.IsTrue(broker.Contains("    hostname: expensehub-rabbitmq"), "The host name must be fixed, because it names the data folder of the node.");
        Assert.IsTrue(broker.Any(line => line.Contains("rabbitmq-diagnostics", StringComparison.Ordinal)), "The broker needs a health check.");
        Assert.IsTrue(DeliveryFiles.CodeLines(compose).Contains("  rabbitmq-data:"), "The volume must be declared.");
    }

    /// <summary>The web application and the worker mount the same volume at the same path, so they share one SQLite file.</summary>
    [TestMethod]
    public void Compose_WebAndWorkerMountTheSameDatabaseVolumeAtTheSamePath()
    {
        string compose = DeliveryFiles.Read(DeliveryFiles.Compose);

        Assert.IsTrue(DeliveryFiles.ServiceLines(compose, "web").Contains("      - expensehub-data:/data"));
        Assert.IsTrue(DeliveryFiles.ServiceLines(compose, "worker").Contains("      - expensehub-data:/data"));
    }

    /// <summary>The connection string is written once and both services take it from the same anchor, and it is an absolute path on the volume.</summary>
    [TestMethod]
    public void Compose_WebAndWorkerShareOneAbsoluteConnectionString()
    {
        string compose = DeliveryFiles.Read(DeliveryFiles.Compose);
        List<string> definitions = DeliveryFiles.CodeLines(compose).Where(line => line.Contains("ConnectionStrings__ExpenseHub:", StringComparison.Ordinal)).ToList();

        Assert.HasCount(1, definitions, "The connection string must be written once, in the shared block.");
        Assert.IsTrue(definitions[0].Contains("\"Data Source=/data/expensehub.db\"", StringComparison.Ordinal), "The database file must be an absolute path on the shared volume.");
        Assert.IsTrue(DeliveryFiles.ServiceLines(compose, "web").Contains("      <<: *expensehub-environment"));
        Assert.IsTrue(DeliveryFiles.ServiceLines(compose, "worker").Contains("      <<: *expensehub-environment"));
    }

    /// <summary>Only the web application prepares the database and keeps the keys of the data protection; the worker does neither.</summary>
    [TestMethod]
    public void Compose_OnlyTheWebApplicationMigratesAndKeepsTheKeys()
    {
        string compose = DeliveryFiles.Read(DeliveryFiles.Compose);
        IReadOnlyList<string> web = DeliveryFiles.ServiceLines(compose, "web");
        IReadOnlyList<string> worker = DeliveryFiles.ServiceLines(compose, "worker");

        Assert.IsTrue(web.Contains("      Database__MigrateOnStartup: \"true\""));
        Assert.IsTrue(web.Contains("      DataProtection__KeysPath: /keys"));
        Assert.IsTrue(web.Contains("      - expensehub-keys:/keys"));
        Assert.IsFalse(worker.Any(line => line.Contains("MigrateOnStartup", StringComparison.Ordinal)));
        Assert.IsFalse(worker.Any(line => line.Contains("KeysPath", StringComparison.Ordinal)));
        Assert.IsFalse(worker.Any(line => line.Contains("expensehub-keys", StringComparison.Ordinal)));
    }

    /// <summary>The worker waits for a healthy broker and a healthy web application, which has already prepared the database.</summary>
    [TestMethod]
    public void Compose_WorkerWaitsForTheBrokerAndForTheWebApplication()
    {
        string compose = DeliveryFiles.Read(DeliveryFiles.Compose);
        IReadOnlyList<string> worker = DeliveryFiles.ServiceLines(compose, "worker");

        Assert.IsTrue(worker.Contains("      rabbitmq:"));
        Assert.IsTrue(worker.Contains("      web:"));
        Assert.AreEqual(2, worker.Count(line => line.Trim() == "condition: service_healthy"));
    }

    /// <summary>Every mandatory variable is required with an error message, so a missing value stops the Compose file.</summary>
    [TestMethod]
    public void Compose_RequiresEveryMandatoryVariableWithAMessage()
    {
        string compose = DeliveryFiles.Read(DeliveryFiles.Compose);

        foreach (string variable in _requiredVariables)
        {
            Assert.IsTrue(compose.Contains("${" + variable + ":?", StringComparison.Ordinal), $"The variable {variable} must be required with an error message.");
        }
    }

    /// <summary>The user names and the passwords are only references to variables, never values written in the file.</summary>
    [TestMethod]
    public void Compose_CredentialsAreOnlyReferencesToVariables()
    {
        string compose = DeliveryFiles.Read(DeliveryFiles.Compose);
        string[] keys = ["RabbitMq__UserName:", "RabbitMq__Password:", "RABBITMQ_DEFAULT_USER:", "RABBITMQ_DEFAULT_PASS:", "Seed__Admin__Email:", "Seed__Admin__Password:"];

        foreach (string key in keys)
        {
            string line = DeliveryFiles.CodeLines(compose).Single(candidate => candidate.TrimStart().StartsWith(key, StringComparison.Ordinal));
            string value = line.Substring(line.IndexOf(':', StringComparison.Ordinal) + 1).Trim();

            Assert.IsTrue(value.StartsWith("\"${", StringComparison.Ordinal) && value.EndsWith("}\"", StringComparison.Ordinal), $"The value of {key} must be a reference to a variable.");
        }
    }

    /// <summary>The ports are published only on localhost, and the AMQP port of the broker is not published at all.</summary>
    [TestMethod]
    public void Compose_PublishesPortsOnlyOnLocalhost()
    {
        string compose = DeliveryFiles.Read(DeliveryFiles.Compose);
        List<string> ports = DeliveryFiles.CodeLines(compose).Where(line => line.Contains("15672\"", StringComparison.Ordinal) || line.Contains("8080\"", StringComparison.Ordinal)).ToList();

        Assert.IsNotEmpty(ports);
        Assert.IsTrue(ports.All(line => line.TrimStart().StartsWith("- \"127.0.0.1:", StringComparison.Ordinal)));
        Assert.IsFalse(DeliveryFiles.CodeLines(compose).Any(line => line.Contains(":5672", StringComparison.Ordinal)), "The AMQP port must stay on the network of the Compose file.");
    }

    /// <summary>The example environment file lists every variable the Compose file reads and leaves the mandatory ones blank.</summary>
    [TestMethod]
    public void EnvExample_ListsEveryVariableOfTheComposeFileAndLeavesTheMandatoryOnesBlank()
    {
        string compose = DeliveryFiles.Read(DeliveryFiles.Compose);
        IReadOnlyList<string> example = DeliveryFiles.CodeLines(DeliveryFiles.Read(DeliveryFiles.EnvExample));

        HashSet<string> used = Regex.Matches(compose, "\\$\\{([A-Z_]+)").Select(match => match.Groups[1].Value).ToHashSet();

        foreach (string variable in used)
        {
            Assert.IsTrue(example.Any(line => line.StartsWith(variable + "=", StringComparison.Ordinal)), $"The variable {variable} is missing in .env.example.");
        }

        foreach (string variable in _blankVariables)
        {
            Assert.Contains(variable + "=", example, $"The variable {variable} must be listed, with no value.");
        }
    }

    /// <summary>The web image and the worker image are built from exactly the same base images.</summary>
    [TestMethod]
    public void Dockerfiles_UseExactlyTheSameBaseImages()
    {
        List<string> web = BaseLines(DeliveryFiles.Read(DeliveryFiles.WebDockerfile));
        List<string> worker = BaseLines(DeliveryFiles.Read(DeliveryFiles.WorkerDockerfile));

        Assert.HasCount(4, web);
        CollectionAssert.AreEqual(web, worker, "The ARG and FROM lines of the two Dockerfiles must be equal.");
    }

    /// <summary>The runtime base is aspnet for both (the worker inherits the web framework) and no base uses a floating tag.</summary>
    [TestMethod]
    public void Dockerfiles_UseAspNetAsTheRuntimeAndNeverAFloatingTag()
    {
        foreach (string path in new[] { DeliveryFiles.WebDockerfile, DeliveryFiles.WorkerDockerfile })
        {
            List<string> bases = BaseLines(DeliveryFiles.Read(path));

            Assert.Contains("ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.14", bases, $"{path} must use the aspnet runtime image.");
            Assert.Contains("ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.403", bases, $"{path} must use the SDK image.");
            Assert.IsFalse(bases.Any(line => line.Contains("latest", StringComparison.OrdinalIgnoreCase)));
        }
    }

    /// <summary>Both images run as the user of the image, not as root, and it is the same user, which lets both write the shared file.</summary>
    [TestMethod]
    public void Dockerfiles_RunAsTheNonRootUserOfTheImage()
    {
        foreach (string path in new[] { DeliveryFiles.WebDockerfile, DeliveryFiles.WorkerDockerfile })
        {
            List<string> users = DeliveryFiles.CodeLines(DeliveryFiles.Read(path)).Where(line => line.StartsWith("USER ", StringComparison.Ordinal)).ToList();

            Assert.AreEqual("USER $APP_UID", users[^1], $"{path} must end running as the user of the image.");
        }
    }

    /// <summary>A new volume copies the owner of the directory of the image, so the directories are handed to the user of the image.</summary>
    [TestMethod]
    public void Dockerfiles_HandTheMountedDirectoriesToTheImageUser()
    {
        List<string> web = DeliveryFiles.CodeLines(DeliveryFiles.Read(DeliveryFiles.WebDockerfile)).ToList();
        List<string> worker = DeliveryFiles.CodeLines(DeliveryFiles.Read(DeliveryFiles.WorkerDockerfile)).ToList();

        Assert.Contains("RUN mkdir -p /data /keys && chown \"$APP_UID\" /data /keys", web, "The web image must hand /data and /keys to the user.");
        Assert.Contains("RUN mkdir -p /data && chown \"$APP_UID\" /data", worker, "The worker image must hand /data to the user.");
    }

    /// <summary>Both images carry the commit as an environment variable and as the revision label.</summary>
    [TestMethod]
    public void Dockerfiles_RecordTheCommitInTheImage()
    {
        foreach (string path in new[] { DeliveryFiles.WebDockerfile, DeliveryFiles.WorkerDockerfile })
        {
            string text = DeliveryFiles.Read(path);

            Assert.Contains("ARG GIT_COMMIT=unknown", text, path);
            Assert.Contains("Build__Commit=${GIT_COMMIT}", text, path);
            Assert.Contains("org.opencontainers.image.revision=\"${GIT_COMMIT}\"", text, path);
        }
    }

    /// <summary>The ignore file keeps the folders of the machine, the databases and the environment files out of the build context.</summary>
    [TestMethod]
    public void DockerIgnore_KeepsLocalFilesAndSecretsOutOfTheBuildContext()
    {
        IReadOnlyList<string> lines = DeliveryFiles.CodeLines(DeliveryFiles.Read(DeliveryFiles.DockerIgnore));

        foreach (string pattern in new[] { "**/bin", "**/obj", "**/.env", "**/*.env", "**/*.db", ".git" })
        {
            Assert.Contains(pattern, lines, $"{pattern} must be in .dockerignore.");
        }
    }

    /// <summary>The images are published only by a push to main, with no path filter.</summary>
    [TestMethod]
    public void Workflow_PublishesOnlyOnAPushToMainWithNoPathFilter()
    {
        string workflow = DeliveryFiles.Read(DeliveryFiles.Workflow);
        IReadOnlyList<string> lines = DeliveryFiles.CodeLines(workflow);

        Assert.IsTrue(lines.Contains("  push:"));
        Assert.IsTrue(lines.Contains("    branches: [\"main\"]"));
        Assert.IsFalse(lines.Any(line => line.TrimStart().StartsWith("paths", StringComparison.Ordinal)), "There must be no path filter.");

        foreach (string step in new[] { "Log in to the registry", "Push the images" })
        {
            int index = lines.ToList().FindIndex(line => line.Contains("name: " + step, StringComparison.Ordinal));

            Assert.IsGreaterThanOrEqualTo(0, index, $"The step '{step}' is missing.");
            Assert.AreEqual("        if: github.event_name == 'push' && github.ref == 'refs/heads/main'", lines[index + 1], $"The step '{step}' must run only on a push to main.");
        }
    }

    /// <summary>The images are built and pushed only after the tests job passes.</summary>
    [TestMethod]
    public void Workflow_ImagesJobNeedsTheTestsJob()
    {
        IReadOnlyList<string> lines = DeliveryFiles.CodeLines(DeliveryFiles.Read(DeliveryFiles.Workflow));

        int images = lines.ToList().FindIndex(line => line == "  images:");

        Assert.IsGreaterThan(0, images);
        Assert.IsTrue(lines.Skip(images).Take(4).Contains("    needs: test"), "The images job must depend on the test job.");
        Assert.IsTrue(lines.Take(images).Any(line => line.Contains("dotnet test", StringComparison.Ordinal)), "The test job must run the tests.");
    }

    /// <summary>The images are tagged with the full commit SHA, both are pushed, and the tag latest never appears.</summary>
    [TestMethod]
    public void Workflow_TagsWithTheFullCommitShaAndNeverWithLatest()
    {
        IReadOnlyList<string> lines = DeliveryFiles.CodeLines(DeliveryFiles.Read(DeliveryFiles.Workflow));

        Assert.IsTrue(lines.Any(line => line.Contains("expensehub-web:${GITHUB_SHA}", StringComparison.Ordinal)));
        Assert.IsTrue(lines.Any(line => line.Contains("expensehub-worker:${GITHUB_SHA}", StringComparison.Ordinal)));
        Assert.IsTrue(lines.Contains("          docker push \"${WEB_IMAGE}\""));
        Assert.IsTrue(lines.Contains("          docker push \"${WORKER_IMAGE}\""));

        foreach (string line in lines)
        {
            Assert.DoesNotContain("latest", line.Replace("ubuntu-latest", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>The permission to write packages is given only to the job that publishes, not to the whole workflow.</summary>
    [TestMethod]
    public void Workflow_GivesPackageWritePermissionOnlyToTheImagesJob()
    {
        IReadOnlyList<string> lines = DeliveryFiles.CodeLines(DeliveryFiles.Read(DeliveryFiles.Workflow));
        int images = lines.ToList().FindIndex(line => line == "  images:");

        Assert.AreEqual(1, lines.Count(line => line.Contains("packages: write", StringComparison.Ordinal)));
        Assert.IsGreaterThan(images, lines.ToList().FindIndex(line => line.Contains("packages: write", StringComparison.Ordinal)));
    }

    private static List<string> BaseLines(string dockerfile)
    {
        return DeliveryFiles.CodeLines(dockerfile)
            .Where(line => line.StartsWith("ARG DOTNET_", StringComparison.Ordinal) || line.StartsWith("FROM ", StringComparison.Ordinal))
            .ToList();
    }
}
