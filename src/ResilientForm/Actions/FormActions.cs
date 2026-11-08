using OpenQA.Selenium;
using ResilientForm.Locators;

namespace ResilientForm.Actions;

public sealed record StepResult(string Field, bool Passed, string Detail);

public sealed class FormActions(IWebDriver driver, ElementResolver resolver)
{
    private sealed record Opt(int Index, string Text);

    public StepResult SetText(FieldQuery q, string value) => Run(q, el =>
    {
        el.Clear();
        el.SendKeys(value);
        var actual = el.GetDomProperty("value"); // read back what the page really holds
        return actual == value ? (true, $"typed '{actual}'") : (false, $"expected '{value}', page holds '{actual}'");
    });

    public StepResult SelectRadio(FieldQuery q) => Run(q, el =>
    {
        if (!el.Selected)
        {
            try { el.Click(); }
            catch (ElementClickInterceptedException)
            { ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", el); }
        }
        return el.Selected ? (true, $"selected '{q.OptionText}'") : (false, $"'{q.OptionText}' not selected after click");
    });

    public StepResult SelectDropdown(FieldQuery q, string wanted) => Run(q, el =>
    {
        var optionEls = el.FindElements(By.TagName("option"));
        var options = optionEls.Select((o, i) => new Opt(i, (o.GetDomProperty("textContent") ?? "").Trim()))
            .Where(o => o.Text.Length > 0 && !o.Text.StartsWith("--")) // skip the placeholder
            .ToList();
        var target = Scorer.Norm(wanted);

        var exact = options.Where(o => Scorer.Norm(o.Text) == target).ToList();
        var partial = options.Where(o => Scorer.Norm(o.Text).Contains(target) || target.Contains(Scorer.Norm(o.Text))).ToList();
        var fuzzy = options.Select(o => (o, d: Lev(Scorer.Norm(o.Text), target)))
            .Where(x => x.d <= 2).OrderBy(x => x.d).ToList();

        Opt? pick = exact.Count == 1 ? exact[0]
                  : exact.Count == 0 && partial.Count == 1 ? partial[0]
                  : exact.Count == 0 && partial.Count == 0 && fuzzy.Count >= 1
                    && (fuzzy.Count == 1 || fuzzy[0].d < fuzzy[1].d) ? fuzzy[0].o
                  : null;

        if (pick is null)
            return (false, $"no unambiguous option for '{wanted}'. Available: " +
                           string.Join(", ", options.Take(8).Select(o => o.Text)) + "...");

        optionEls[pick.Index].Click();
        var shown = (el.FindElement(By.CssSelector("option:checked")).GetDomProperty("textContent") ?? "").Trim();
        return Scorer.Norm(shown) == Scorer.Norm(pick.Text)
            ? (true, $"selected '{shown}'")
            : (false, $"expected '{pick.Text}', dropdown shows '{shown}'");
    });

    private StepResult Run(FieldQuery q, Func<IWebElement, (bool ok, string detail)> act)
    {
        try
        {
            var el = resolver.Resolve(q);
            var (ok, detail) = act(el);
            var how = resolver.Last is { } l ? $" (matched #{l.Candidate.Index} id='{l.Candidate.Id}', score {l.Score})" : "";
            return new StepResult(q.Name, ok, detail + how);
        }
        catch (Exception ex) { return new StepResult(q.Name, false, ex.Message); }
    }

    private static int Lev(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                                   d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }
}