using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using ResilientForm.Actions;
using ResilientForm.Locators;

namespace ResilientForm.Tests;

[TestFixture]
public class FixtureTests
{
    private IWebDriver _driver = null!;

    [OneTimeSetUp]
    public void StartBrowser()
    {
        var options = new ChromeOptions { PageLoadStrategy = PageLoadStrategy.Eager };
        options.AddArguments("--headless=new", "--no-first-run");
        _driver = new ChromeDriver(options);
        _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(20);
    }

    [OneTimeTearDown]
    public void StopBrowser()
    {
        _driver.Quit();
        _driver.Dispose();
    }

    private static string FixturesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "fixtures")))
            dir = dir.Parent;
        if (dir is null) throw new DirectoryNotFoundException("Could not find the fixtures folder.");
        return Path.Combine(dir.FullName, "fixtures");
    }

    private static IEnumerable<string> Files(bool negative) =>
        Directory.GetFiles(FixturesDir(), "*.html")
            .Select(f => Path.GetFileName(f))
            .Where(n => n.StartsWith("neg-") == negative)
            .OrderBy(n => n);

    public static IEnumerable<string> PassingFixtures() => Files(negative: false);
    public static IEnumerable<string> NegativeFixtures() => Files(negative: true);

    private FormActions Open(string fileName, int timeoutSeconds)
    {
        _driver.Navigate().GoToUrl(new Uri(Path.Combine(FixturesDir(), fileName)).AbsoluteUri);
        var resolver = new ElementResolver(_driver, TimeSpan.FromSeconds(timeoutSeconds), false);
        return new FormActions(_driver, resolver);
    }

    [TestCaseSource(nameof(PassingFixtures))]
    public void Fills_all_three_fields(string fileName)
    {
        var actions = Open(fileName, 5);
        var results = new[]
        {
            actions.SetText(FieldQueries.FirstName, "Test"),
            actions.SelectRadio(FieldQueries.Gender("Male")),
            actions.SelectDropdown(FieldQueries.State, "India"),
        };
        var failures = results.Where(r => !r.Passed).Select(r => $"{r.Field}: {r.Detail}").ToList();
        Assert.That(failures, Is.Empty);
    }

    [TestCaseSource(nameof(NegativeFixtures))]
    public void Refuses_to_guess_when_field_is_missing(string fileName)
    {
        var actions = Open(fileName, 2);
        var result = actions.SetText(FieldQueries.FirstName, "Test");
        Assert.That(result.Passed, Is.False, "It filled a field that is not First Name.");
    }
}