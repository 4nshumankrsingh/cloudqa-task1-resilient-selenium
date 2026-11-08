namespace ResilientForm.Locators;

public enum FieldKind { Text, Radio, Select }

public sealed record FieldQuery(string Name, FieldKind Kind, string[] Keywords,
                                string[] Negatives, string? OptionText = null);

public sealed class Candidate
{
    public int Index { get; set; }
    public string Tag { get; set; } = "";
    public string Type { get; set; } = "";
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Placeholder { get; set; } = "";
    public string AriaLabel { get; set; } = "";
    public string Label { get; set; } = "";
    public string Near { get; set; } = "";
    public string OptionText { get; set; } = "";
    public string Value { get; set; } = "";
    public string GroupText { get; set; } = "";
    public bool Visible { get; set; }
    public bool Enabled { get; set; }
    public bool InComponent { get; set; }
}

public sealed record Scored(Candidate Candidate, int Score, string Why);