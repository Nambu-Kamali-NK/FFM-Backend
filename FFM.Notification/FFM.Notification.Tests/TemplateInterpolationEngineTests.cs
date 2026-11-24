using FFM.Notification.Application.Services;
using Xunit;

namespace FFM.Notification.Tests;

public class TemplateInterpolationEngineTests
{
    private readonly TemplateInterpolationEngine _engine = new();

    [Fact]
    public void Interpolate_ValidPlaceholders_SubstitutesCorrectly()
    {
        var template = "Hi {{EmployeeName}}, your task #{{TaskNumber}} at {{SiteName}} is ready.";
        var placeholders = new Dictionary<string, string>
        {
            ["EmployeeName"] = "Ramesh Kumar",
            ["TaskNumber"] = "TK-101",
            ["SiteName"] = "Metro Bank ATM"
        };

        var result = _engine.Interpolate(template, placeholders);

        Assert.Equal("Hi Ramesh Kumar, your task #TK-101 at Metro Bank ATM is ready.", result);
    }

    [Fact]
    public void Interpolate_CaseInsensitiveKeys_SubstitutesCorrectly()
    {
        var template = "Task: {{TASKNUMBER}} for {{employeename}}.";
        var placeholders = new Dictionary<string, string>
        {
            ["TaskNumber"] = "TK-999",
            ["EmployeeName"] = "Priya"
        };

        var result = _engine.Interpolate(template, placeholders);

        Assert.Equal("Task: TK-999 for Priya.", result);
    }

    [Fact]
    public void Interpolate_MissingPlaceholderInDictionary_ReplacesWithEmptyString()
    {
        var template = "Hello {{EmployeeName}}, note: {{MissingNote}}.";
        var placeholders = new Dictionary<string, string>
        {
            ["EmployeeName"] = "Ramesh"
        };

        var result = _engine.Interpolate(template, placeholders);

        Assert.Equal("Hello Ramesh, note: .", result);
    }

    [Fact]
    public void Interpolate_EmptyOrNullTemplate_ReturnsEmpty()
    {
        var result1 = _engine.Interpolate("", new Dictionary<string, string>());
        var result2 = _engine.Interpolate(null!, new Dictionary<string, string>());

        Assert.Equal(string.Empty, result1);
        Assert.Equal(string.Empty, result2);
    }

    [Fact]
    public void Interpolate_EmptyDictionary_StripsAllPlaceholdersGracefully()
    {
        var template = "Alert: {{Code}} at {{Site}}.";
        var result = _engine.Interpolate(template, new Dictionary<string, string>());

        Assert.Equal("Alert:  at .", result);
    }
}

