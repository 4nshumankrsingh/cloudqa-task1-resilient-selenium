namespace ResilientForm.Locators;

public static class FieldQueries
{
    public static readonly FieldQuery FirstName = new("First Name", FieldKind.Text,
        ["firstname", "fname", "givenname", "forename"],
        ["last", "sur", "middle", "nick", "user", "full"]);

    public static FieldQuery Gender(string option) => new("Gender", FieldKind.Radio,
        ["gender", "sex"], [], option);

    public static readonly FieldQuery State = new("State", FieldKind.Select,
        ["state", "province", "region"], ["country"]);
}