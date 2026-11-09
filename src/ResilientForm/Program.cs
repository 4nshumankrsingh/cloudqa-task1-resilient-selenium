using Microsoft.Extensions.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using ResilientForm.Actions;
using ResilientForm.Config;
using ResilientForm.Locators;

// Later wins: appsettings.json < env var FORM_Url < command line --Url
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables("FORM_")
    .AddCommandLine(args)
    .Build();

RunOptions opt;
try { opt = RunOptions.From(config); }
catch (InvalidOperationException ex) { Console.Error.WriteLine(ex.Message); return 2; }
var hold = int.TryParse(config["HoldSeconds"], out var h) ? h : 0;

var chrome = new ChromeOptions();
chrome.AddArguments("--no-first-run", "--no-default-browser-check", "--disable-search-engine-choice-screen");
chrome.PageLoadStrategy = PageLoadStrategy.Eager; // don't wait for every image and ad
if (opt.Headless) chrome.AddArgument("--headless=new");
using var driver = new ChromeDriver(chrome);

var timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);
driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(20);

var loaded = false;
for (var attempt = 1; attempt <= 3 && !loaded; attempt++)
{
    try { driver.Navigate().GoToUrl(opt.Url); loaded = true; }
    catch (WebDriverTimeoutException)
    { Console.WriteLine($"Note: attempt {attempt}/3 timed out loading the page."); }
}
if (!loaded)
{
    Console.Error.WriteLine("Could not load the page after 3 attempts. Check your network and the URL.");
    return 3;
}

try
{
    new WebDriverWait(driver, timeout).Until(d =>
    {
        EnsureLiveWindow(d);
        var s = ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState") as string;
        return s is "interactive" or "complete";
    });
}
catch (WebDriverTimeoutException) { Console.WriteLine("Note: page did not settle in time; trying anyway."); }

var resolver = new ElementResolver(driver, timeout, opt.Verbose);
var actions = new FormActions(driver, resolver);

var results = new[]
{
    actions.SetText(FieldQueries.FirstName, opt.FirstName),
    actions.SelectRadio(FieldQueries.Gender(opt.Gender)),
    actions.SelectDropdown(FieldQueries.State, opt.State),
};

Console.WriteLine();
foreach (var r in results)
    Console.WriteLine($"[{(r.Passed ? "PASS" : "FAIL")}] {r.Field}: {r.Detail}");
var failed = results.Count(r => !r.Passed);
Console.WriteLine($"\n{results.Length - failed}/{results.Length} checks passed.");

if (hold > 0)
{
    Console.WriteLine($"Keeping the browser open for {hold}s...");
    Thread.Sleep(TimeSpan.FromSeconds(hold));
}
return failed == 0 ? 0 : 1;

static void EnsureLiveWindow(IWebDriver d)
{
    try { _ = d.CurrentWindowHandle; }
    catch (NoSuchWindowException) { d.SwitchTo().Window(d.WindowHandles.Last()); }
}