namespace ResilientForm.Locators;

public static class Scorer
{
    public static string Norm(string? s) =>
        new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    // 2 = exact match, 1 = contains, 0 = none
    private static int Match(string? text, string[] keywords)
    {
        var n = Norm(text);
        if (n.Length == 0) return 0;
        if (keywords.Contains(n)) return 2;
        return keywords.Any(k => k.Length >= 3 && n.Contains(k)) ? 1 : 0;
    }

    private static int Pts(int m, int exact, int partial) => m == 2 ? exact : m == 1 ? partial : 0;

    public static bool KindMatches(Candidate c, FieldKind k) => k switch
    {
        FieldKind.Select => c.Tag == "SELECT",
        FieldKind.Radio => c.Tag == "INPUT" && c.Type == "radio",
        _ => c.Tag == "INPUT" && (c.Type == "text" || c.Type == "search" || c.Type == ""),
    };

    public static Scored Score(Candidate c, FieldQuery q)
    {
        var kw = q.Keywords.Select(Norm).ToArray();
        var neg = q.Negatives.Select(Norm).ToArray();
        var score = 0;
        var why = new List<string>();
        void Add(string what, int pts) { if (pts != 0) { score += pts; why.Add($"{what}{pts:+#;-#}"); } }

        // Negative evidence comes only from label-like attributes, never from option text.
        // (The State dropdown's options say "Select Country"; that must not count against it.)
        if (new[] { c.Label, c.AriaLabel, c.Placeholder, c.Id, c.Name }
            .Any(t => neg.Any(x => x.Length > 0 && Norm(t).Contains(x))))
            Add("negative", -100);

        if (q.Kind == FieldKind.Radio)
        {
            Add("group", Match(c.GroupText, kw) > 0 ? 30 : 0);
            Add("name", Pts(Match(c.Name, kw), 10, 6));
            if (q.OptionText is not null)
            {
                var target = Norm(q.OptionText);
                var opt = Norm(c.OptionText.Length > 0 ? c.OptionText : c.Label);
                // Exact equality on purpose: "female" contains "male".
                if (opt == target) Add("option", 60);
                else
                {
                    if (Norm(c.Value) == target) Add("value", 40);
                    if (Norm(c.Id) == target) Add("id=option", 20);
                }
            }
        }
        else
        {
            Add("label", Pts(Match(c.Label, kw), 60, 36));
            Add("aria", Pts(Match(c.AriaLabel, kw), 50, 30));
            Add("near", Pts(Match(c.Near, kw), 30, 18));
            Add("placeholder", Pts(Match(c.Placeholder, kw), 12, 7));
            Add("id", Pts(Match(c.Id, kw), 10, 6));
            Add("name", Pts(Match(c.Name, kw), 10, 6));
        }
        return new Scored(c, score, string.Join(" ", why));
    }
}