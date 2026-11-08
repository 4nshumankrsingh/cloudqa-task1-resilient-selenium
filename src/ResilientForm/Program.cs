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

var chrome = new ChromeOptions();
if (opt.Headless) chrome.AddArgument("--headless=new");
using var driver = new ChromeDriver(chrome);

var timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);
driver.Navigate().GoToUrl(opt.Url);
new WebDriverWait(driver, timeout).Until(d =>
    (string)((IJavaScriptExecutor)d).ExecuteScript("return document.readyState") == "complete");

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
return failed == 0 ? 0 : 1;