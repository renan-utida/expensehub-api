using System;
using System.Collections.Generic;
using System.IO;
using ExpenseHub.Api.Deployment;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Deployment;

/// <summary>
/// Tests of where the keys of the data protection are kept. The bearer tokens are protected with these keys, so when the
/// directory is configured (a volume in the containers) the tokens survive the recreation of a container; when it is not, the
/// registration does nothing and the default behavior of the CP2 stays. No key is created: only the registration is checked.
/// </summary>
[TestClass]
public sealed class KeyStorageTests
{
    /// <summary>Without a path the registration adds nothing, so the default behavior of ASP.NET Core stays.</summary>
    [TestMethod]
    public void AddExpenseHubDataProtection_DoesNothing_WhenThereIsNoPath()
    {
        var services = new ServiceCollection();

        IServiceCollection result = services.AddExpenseHubDataProtection(NewConfiguration(path: null), BasePath());

        Assert.AreSame(services, result);
        Assert.HasCount(0, services);
    }

    /// <summary>A blank path counts as no path.</summary>
    /// <param name="path">The configured value.</param>
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void AddExpenseHubDataProtection_DoesNothing_WhenThePathIsBlank(string path)
    {
        var services = new ServiceCollection();

        services.AddExpenseHubDataProtection(NewConfiguration(path), BasePath());

        Assert.HasCount(0, services);
    }

    /// <summary>With a path the keys are kept in that directory, on the file system.</summary>
    [TestMethod]
    public void AddExpenseHubDataProtection_KeepsTheKeysInTheConfiguredDirectory()
    {
        string keys = Path.Combine(BasePath(), "keys-volume");
        using ServiceProvider provider = NewProvider(NewConfiguration(keys));

        IXmlRepository? repository = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository;

        FileSystemXmlRepository fileRepository = Assert.IsInstanceOfType<FileSystemXmlRepository>(repository);
        Assert.AreEqual(Path.GetFullPath(keys), fileRepository.Directory.FullName.TrimEnd(Path.DirectorySeparatorChar));
    }

    /// <summary>The application name is fixed, so the keys do not depend on where the application is installed.</summary>
    [TestMethod]
    public void AddExpenseHubDataProtection_FixesTheApplicationName()
    {
        using ServiceProvider provider = NewProvider(NewConfiguration(Path.Combine(BasePath(), "keys-volume")));

        string? name = provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator;

        Assert.AreEqual(DeploymentServiceCollectionExtensions.DataProtectionApplicationName, name);
        Assert.AreEqual("ExpenseHub", name);
    }

    /// <summary>A relative path is anchored in the base directory, like the path of the database.</summary>
    [TestMethod]
    public void AddExpenseHubDataProtection_AnchorsARelativePathInTheBaseDirectory()
    {
        string basePath = BasePath();
        using ServiceProvider provider = NewProvider(NewConfiguration("relative-keys"), basePath);

        IXmlRepository? repository = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository;

        FileSystemXmlRepository fileRepository = Assert.IsInstanceOfType<FileSystemXmlRepository>(repository);
        Assert.AreEqual(Path.Combine(basePath, "relative-keys"), fileRepository.Directory.FullName.TrimEnd(Path.DirectorySeparatorChar));
    }

    /// <summary>A null service collection or configuration is rejected.</summary>
    [TestMethod]
    public void AddExpenseHubDataProtection_RejectsNullArguments()
    {
        IConfiguration configuration = NewConfiguration(path: null);

        Assert.ThrowsExactly<ArgumentNullException>(() => ((IServiceCollection)null!).AddExpenseHubDataProtection(configuration, BasePath()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new ServiceCollection().AddExpenseHubDataProtection(null!, BasePath()));
    }

    private static string BasePath()
    {
        return Path.Combine(Path.GetTempPath(), "expensehub-key-tests");
    }

    private static IConfiguration NewConfiguration(string? path)
    {
        var values = new Dictionary<string, string?>();

        if (path is not null)
        {
            values["DataProtection:KeysPath"] = path;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static ServiceProvider NewProvider(IConfiguration configuration, string? basePath = null)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddExpenseHubDataProtection(configuration, basePath ?? BasePath());

        return services.BuildServiceProvider();
    }
}
