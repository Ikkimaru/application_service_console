using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Domain.Tests;

public sealed class ProfileModelTests
{
    [Fact]
    public void NewProfileDefaultsRequireExplicitEnablement()
    {
        ProjectProfile profile = new()
        {
            SchemaVersion = 1,
            Id = "sample",
            Name = "Sample",
            ProjectRootSettingKey = "sample.root"
        };

        Assert.Equal("requireEnablement", profile.Execution.NewServicePolicy);
        Assert.Empty(profile.ServiceOverrides);
    }
}
