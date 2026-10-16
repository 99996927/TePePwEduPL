namespace Hr.Rules.Overtime;

/// <summary>Pora nocna jako parametr pracodawcy: 8 godzin w oknie 21:00–07:00 (art. 151⁷ Kodeksu pracy).</summary>
public sealed record NightWindow(TimeOnly Start, TimeOnly End)
{
    public static NightWindow Default { get; } = new(new TimeOnly(21, 0), new TimeOnly(5, 0));

    public bool Contains(TimeOnly time) =>
        Start > End ? time >= Start || time < End : time >= Start && time < End;
}
