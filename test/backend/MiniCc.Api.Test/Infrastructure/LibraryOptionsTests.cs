using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MiniCc.Api.Core;
using MiniCc.Api.Shared.Config;

namespace MiniCc.Api.Test.Infrastructure;

public class LibraryOptionsTests
{
    [Theory]
    [InlineData(1, 3)]
    [InlineData(365, 60)]
    public void Options_BindsValidLimits(int days, int seconds)
    {
        using var services = Services(days, seconds);
        var options = services.GetRequiredService<IOptions<LibraryOptions>>().Value;
        options.TrashRetentionDays.Should().Be(days);
        options.UndoWindowSeconds.Should().Be(seconds);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(366, 10)]
    [InlineData(30, 2)]
    [InlineData(30, 61)]
    public void Options_RejectsOutOfRangeValues(int days, int seconds)
    {
        using var services = Services(days, seconds);
        Action read = () => _ = services.GetRequiredService<IOptions<LibraryOptions>>().Value;
        read.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Options_ValidatesOnStartup()
    {
        using var services = Services(0, 10);
        Action start = () => services.GetRequiredService<IStartupValidator>().Validate();
        start.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Options_UsesDefaultsWhenSectionIsAbsent()
    {
        using var services = new ServiceCollection().AddInfrastructureServices(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ImageProxy:SignatureKey"] = "test" }).Build())
            .BuildServiceProvider();
        var options = services.GetRequiredService<IOptions<LibraryOptions>>().Value;
        options.TrashRetentionDays.Should().Be(30);
        options.UndoWindowSeconds.Should().Be(10);
    }

    private static ServiceProvider Services(int days, int seconds) => new ServiceCollection()
        .AddInfrastructureServices(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Library:TrashRetentionDays"] = days.ToString(),
            ["Library:UndoWindowSeconds"] = seconds.ToString(),
            ["ImageProxy:SignatureKey"] = "test"
        }).Build()).BuildServiceProvider();
}
