using System.Collections.ObjectModel;
using System.Text.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace ResilientForm.Locators;

public sealed class ElementNotFoundException(string message) : Exception(message);
public sealed class AmbiguousFieldException(string message) : Exception(message);

public sealed class ElementResolver(IWebDriver driver, TimeSpan timeout, bool verbose)
{
    private const int MinScore = 40;   // below this we refuse to guess
    private const int MinMargin = 15;  // winner must lead the runner-up by this much

    public Scored? Last { get; private set; }

    public IWebElement Resolve(FieldQuery q)
    {
        var wait = new WebDriverWait(driver, timeout) { PollingInterval = TimeSpan.FromMilliseconds(500) };
        wait.IgnoreExceptionTypes(typeof(ElementNotFoundException)); // ambiguity fails immediately
        return wait.Until(_ => ResolveOnce(q));
    }

    private IWebElement ResolveOnce(FieldQuery q)
    {
        var (cands, els) = Snapshot();
        var ranked = cands
            .Where(c => Scorer.KindMatches(c, q.Kind) && c.Visible && c.Enabled)
            .Select(c => Scorer.Score(c, q))
            .OrderByDescending(s => s.Score)
            .ToList();

        if (verbose)
            foreach (var s in ranked.Take(3))
                Console.WriteLine($"    {q.Name}: #{s.Candidate.Index} score {s.Score} [{s.Why}] id='{s.Candidate.Id}'");

        if (ranked.Count == 0 || ranked[0].Score < MinScore)
            throw new ElementNotFoundException(
                $"No confident match for '{q.Name}' (best score {(ranked.Count == 0 ? 0 : ranked[0].Score)}, need {MinScore}).");
        if (ranked.Count > 1 && ranked[0].Score - ranked[1].Score < MinMargin)
            throw new AmbiguousFieldException(
                $"Ambiguous match for '{q.Name}': top scores {ranked[0].Score} and {ranked[1].Score}.");

        Last = ranked[0];
        return els[ranked[0].Candidate.Index];
    }

    private (List<Candidate>, List<IWebElement>) Snapshot()
    {
        var result = ((IJavaScriptExecutor)driver).ExecuteScript(SnapshotJs);
        var items = (result as System.Collections.IEnumerable)?.Cast<object>().ToList()
                    ?? throw new InvalidOperationException("Page snapshot returned nothing.");
        var json = items.FirstOrDefault() as string
                   ?? throw new InvalidOperationException("Page snapshot was malformed.");
        var cands = JsonSerializer.Deserialize<List<Candidate>>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidOperationException("Could not parse page snapshot.");
        return (cands, items.Skip(1).Cast<IWebElement>().ToList());
    }

    // One round trip: describes every form control so scoring happens in C#.
    private const string SnapshotJs = """
        const txt = n => ((n && (n.innerText ?? n.textContent)) || '').replace(/\s+/g, ' ').trim();
        const vis = e => e.getClientRects().length > 0 && getComputedStyle(e).visibility !== 'hidden';
        const near = node => {
          for (let n = node, d = 0; n && d < 4; n = n.parentElement, d++) {
            let p = n.previousElementSibling;
            while (p && !txt(p)) p = p.previousElementSibling;
            if (p && txt(p).length <= 40) return txt(p);
          }
          return '';
        };
        const labelOf = e => {
          if (e.labels && e.labels.length) return [...e.labels].map(txt).join(' ');
          const ids = (e.getAttribute('aria-labelledby') || '').split(/\s+/).filter(Boolean);
          return ids.map(i => txt(document.getElementById(i))).join(' ');
        };
        const optionOf = e => {
          if (e.labels && e.labels.length) return txt(e.labels[0]);
          let n = e.nextSibling;
          for (let i = 0; n && i < 4; i++, n = n.nextSibling) { const t = txt(n); if (t) return t.slice(0, 40); }
          return '';
        };
        const groupOf = e => {
          if (e.type !== 'radio' || !e.name) return '';
          for (let a = e.parentElement; a; a = a.parentElement) {
            const same = [...a.querySelectorAll('input[type=radio]')].filter(r => r.name === e.name);
            if (same.length >= 2) return txt(a).slice(0, 120) + ' | ' + near(a);
          }
          return '';
        };
        const all = [...document.querySelectorAll('input,select,textarea')].filter(e => e.type !== 'hidden');
        const rows = all.map((e, i) => ({
          index: i, tag: e.tagName, type: e.type || '', id: e.id || '', name: e.name || '',
          placeholder: e.placeholder || '', ariaLabel: e.getAttribute('aria-label') || '',
          label: labelOf(e), near: near(e),
          optionText: e.type === 'radio' ? optionOf(e) : '', value: e.type === 'radio' ? (e.value || '') : '',
          groupText: groupOf(e), visible: vis(e), enabled: !e.disabled
        }));
        return [JSON.stringify(rows), ...all];
        """;
}