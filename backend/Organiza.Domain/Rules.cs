namespace Organiza.Domain;

public enum EarningKind { Sala, Aula30, Aula50 }
public record EarningInput(DateOnly Date, EarningKind Kind, decimal Quantity, int? Capacity);
public record MonthBalance(string Month, int EarnedCents, int IncomingCents, int AvailableCents, int OutgoingCents, int ShortfallCents, int TargetCents);
public record MonthlyGoal(DateOnly EffectiveMonth, int TargetCents);

public static class Earnings
{
    public const int DefaultTargetCents = 43_000;
    public static readonly DateOnly Opening = new(2026, 8, 1);
    public static int Calculate(EarningInput input)
    {
        if (input.Date < Opening) throw new ArgumentException("A data deve ser a partir de agosto de 2026.");
        if (input.Kind == EarningKind.Sala)
        {
            if (input.Quantity <= 0 || input.Quantity > 24 || input.Quantity * 100 != decimal.Truncate(input.Quantity * 100))
                throw new ArgumentException("Indique horas positivas, até 24, com no máximo duas casas decimais.");
            return checked((int)decimal.Round(input.Quantity * 700, 0, MidpointRounding.AwayFromZero));
        }
        if (!Enum.IsDefined(input.Kind) || input.Capacity is not > 0 or > 10000 || input.Quantity < 0 || input.Quantity > input.Capacity || decimal.Truncate(input.Quantity) != input.Quantity)
            throw new ArgumentException("Indique pessoas inteiras entre zero e a lotação, e uma lotação positiva.");
        var tier = input.Quantity * 100 <= input.Capacity * 49 ? 0 : input.Quantity * 100 < input.Capacity * 90 ? 1 : 2;
        return input.Kind == EarningKind.Aula30 ? new[] { 750, 975, 1060 }[tier] : new[] { 1300, 1600, 1750 }[tier];
    }
    public static int ValidateTarget(int cents) => cents > 0 ? cents : throw new ArgumentException("A meta mensal deve ser superior a zero.");
    public static DateOnly CurrentMonth => new(TaskRules.Today.Year, TaskRules.Today.Month, 1);
    public static IReadOnlyList<MonthBalance> Roll(IEnumerable<(DateOnly Date, int Cents)> entries, DateOnly through, IEnumerable<MonthlyGoal>? goals = null)
    {
        var totals = entries.GroupBy(x => new DateOnly(x.Date.Year, x.Date.Month, 1)).ToDictionary(g => g.Key, g => g.Sum(x => x.Cents));
        var history = (goals ?? []).OrderBy(x => x.EffectiveMonth).ToArray();
        var target = DefaultTargetCents;
        var goalIndex = 0;
        var result = new List<MonthBalance>();
        var incoming = 0;
        for (var month = Opening; month <= new DateOnly(through.Year, through.Month, 1); month = month.AddMonths(1))
        {
            while (goalIndex < history.Length && history[goalIndex].EffectiveMonth <= month)
                target = ValidateTarget(history[goalIndex++].TargetCents);
            var earned = totals.GetValueOrDefault(month);
            var available = checked(earned + incoming);
            // Agosto é histórico: o transporte começa apenas de setembro para outubro.
            var outgoing = month == Opening ? 0 : Math.Max(0, available - target);
            result.Add(new(month.ToString("yyyy-MM"), earned, incoming, available, outgoing, Math.Max(0, target - available), target));
            incoming = outgoing;
        }
        return result;
    }
}
public static class TaskRules
{
    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon")).DateTime);
    public static int Interval(int count, string unit) => count is < 1 or > 3650 ? throw new ArgumentException("O intervalo deve ser entre 1 e 3650.") : unit switch
    {
        "day" => count, "week" => checked(count * 7), "month" => checked(count * 30), _ => throw new ArgumentException("Unidade inválida.")
    };
    public static DateOnly Next(DateOnly completed, int days) => days > 0 ? completed.AddDays(days) : throw new ArgumentException("Intervalo inválido.");
    public static string Urgency(DateOnly? due, bool completed, DateOnly today) => completed || due is null ? "normal" : due < today ? "overdue" : due <= today.AddDays(6) ? "soon" : "normal";
    public static IEnumerable<DateOnly> Forecast(DateOnly due, int days, DateOnly today, DateOnly from, DateOnly through)
    {
        if (days <= 0 || through.DayNumber - from.DayNumber > 370) yield break;
        var date = Next(due < today ? today : due, days);
        while (date <= through)
        {
            if (date >= from) yield return date;
            date = Next(date, days);
        }
    }
}
