namespace Hr.Rules.Time;

internal static class WorkEntries
{
    /// <summary>Sortuje wpisy chronologicznie i odrzuca nakładające się (stykające się są dopuszczalne).</summary>
    internal static List<WorkEntry> SortedNonOverlapping(IReadOnlyList<WorkEntry> entries)
    {
        var sorted = entries.OrderBy(e => e.Start).ThenBy(e => e.End).ToList();
        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].Start < sorted[i - 1].End)
            {
                throw new ArgumentException("Wpisy czasu pracy nie mogą się nakładać.", nameof(entries));
            }
        }

        return sorted;
    }
}
