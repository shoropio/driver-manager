using DriverManager.Core.Models;
using DriverManager.App.Localization;
using Xunit;

namespace DriverManager.Tests;

public class MaintenanceIssueTests
{
    [Theory]
    [InlineData(MaintenanceIssueType.Obsolete, "Obsoleto")]
    [InlineData(MaintenanceIssueType.Missing, "Faltante")]
    [InlineData(MaintenanceIssueType.Broken, "Con error")]
    [InlineData(MaintenanceIssueType.Unsigned, "Sin firmar")]
    public void MaintenanceIssueType_HasCorrectSpanishText(MaintenanceIssueType type, string expected)
    {
        Assert.Equal(expected, DriverText.MaintenanceIssueTypeText(type));
    }

    [Theory]
    [InlineData(MaintenanceSeverity.Critical, "Crítico")]
    [InlineData(MaintenanceSeverity.Warning, "Advertencia")]
    [InlineData(MaintenanceSeverity.Info, "Informativo")]
    public void MaintenanceSeverity_HasCorrectSpanishText(MaintenanceSeverity severity, string expected)
    {
        Assert.Equal(expected, DriverText.MaintenanceSeverityText(severity));
    }

    [Fact]
    public void MaintenanceIssue_DefaultValues_AreCorrect()
    {
        var issue = new MaintenanceIssue
        {
            IssueType = MaintenanceIssueType.Broken,
            Severity = MaintenanceSeverity.Critical,
            DeviceName = "Test Device"
        };

        Assert.Equal(MaintenanceIssueType.Broken, issue.IssueType);
        Assert.Equal(MaintenanceSeverity.Critical, issue.Severity);
        Assert.Equal("Test Device", issue.DeviceName);
        Assert.Equal(string.Empty, issue.HardwareId);
        Assert.Equal(string.Empty, issue.Description);
        Assert.Null(issue.RelatedDriver);
    }

    [Fact]
    public void MaintenanceIssue_WithRelatedDriver_StoresReference()
    {
        var driver = new DriverInfo
        {
            DeviceName = "NVIDIA GeForce RTX 4060",
            InstalledVersion = "32.0.15.6094",
            AvailableVersion = "32.0.15.7001",
            Status = DriverStatus.UpdateAvailable
        };

        var issue = new MaintenanceIssue
        {
            IssueType = MaintenanceIssueType.Obsolete,
            Severity = MaintenanceSeverity.Info,
            DeviceName = "NVIDIA GeForce RTX 4060",
            InstalledVersion = "32.0.15.6094",
            AvailableVersion = "32.0.15.7001",
            RelatedDriver = driver
        };

        Assert.NotNull(issue.RelatedDriver);
        Assert.Equal("32.0.15.7001", issue.AvailableVersion);
    }

    [Theory]
    [InlineData(MaintenanceIssueType.Obsolete)]
    [InlineData(MaintenanceIssueType.Missing)]
    [InlineData(MaintenanceIssueType.Broken)]
    [InlineData(MaintenanceIssueType.Unsigned)]
    public void MaintenanceIssue_AllIssueTypes_AreDefined(MaintenanceIssueType type)
    {
        var text = DriverText.MaintenanceIssueTypeText(type);
        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.NotEqual("Desconocido", text);
    }
}
