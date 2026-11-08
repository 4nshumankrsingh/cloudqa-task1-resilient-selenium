using Microsoft.Extensions.Configuration;

namespace ResilientForm.Config;

public sealed record RunOptions(string Url, bool Headless, bool Verbose, int TimeoutSeconds,
                                string FirstName, string Gender, string State)
{
    public static RunOptions From(IConfiguration c) => new(
        Url: c["Url"] ?? throw new InvalidOperationException("No URL configured. Pass --Url <address>."),
        Headless: bool.TryParse(c["Headless"], out var h) && h,
        Verbose: bool.TryParse(c["Verbose"], out var v) && v,
        TimeoutSeconds: int.TryParse(c["TimeoutSeconds"], out var t) ? t : 15,
        FirstName: c["FirstName"] ?? "Test",
        Gender: c["Gender"] ?? "Male",
        State: c["State"] ?? "India");
}