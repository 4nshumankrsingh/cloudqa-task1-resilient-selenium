using Microsoft.Extensions.Configuration;
using OpenQA.Selenium.Chrome;

// Later wins: appsettings.json < env var FORM_Url < CLI --Url
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables("FORM_")
    .AddCommandLine(args)
    .Build();

var url = config["Url"] ?? throw new InvalidOperationException("Pass --Url <address>.");
using var driver = new ChromeDriver();
driver.Navigate().GoToUrl(url);
Console.WriteLine($"Opened: {driver.Title}");