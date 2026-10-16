namespace Hr.Core.Domain;

/// <summary>Artykuł Kodeksu pracy (dane publiczne, import z API Sejmu); <see cref="Number"/> w zapisie źródła, np. <c>151[1]</c>.</summary>
public class LabourCodeArticle
{
    public string Number { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}
