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
driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);
try { driver.Navigate().GoToUrl(opt.Url); }
catch (WebDriverTimeoutException) { Console.WriteLine("Note: page was slow to load; continuing with what has loaded."); }

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

var firstName = new FieldQuery("First Name", FieldKind.Text,
    Keywords: ["firstname", "fname", "givenname", "forename"],
    Negatives: ["last", "sur", "middle", "nick", "user", "full"]);
var gender = new FieldQuery("Gender", FieldKind.Radio,
    Keywords: ["gender", "sex"], Negatives: [], OptionText: opt.Gender);
var state = new FieldQuery("State", FieldKind.Select,
    Keywords: ["state", "province", "region"], Negatives: ["country"]);

var results = new[]
{
    actions.SetText(firstName, opt.FirstName),
    actions.SelectRadio(gender),
    actions.SelectDropdown(state, opt.State),
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