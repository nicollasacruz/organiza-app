namespace Organiza.Domain;

public record PlanningTask(Guid Id, string Title, string? AssigneeId, DateOnly? Due);
public record FreeWindow(string MemberId, DateOnly Date, TimeOnly Start, TimeOnly End);
public record TaskEstimate(Guid TaskId, int Minutes);
public record PlanningChoice(Guid TaskId, string? MemberId, bool AllowReassignment, string Reason);
public record PlannedTask(Guid TaskId, string Title, string MemberId, DateTimeOffset Start, DateTimeOffset End, int Minutes, string Reason);
public record UnplacedTask(Guid TaskId, string Title, string Reason);
public record DayPlan(PlannedTask[] Items, UnplacedTask[] Unplaced);

public static class Planning
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");
    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Zone).DateTime);
    public static bool Eligible(DateOnly? due, DateTimeOffset now) => due is null || due <= Today(now).AddDays(7);

    public static FreeWindow[] ValidateWindows(IEnumerable<FreeWindow> windows, IReadOnlySet<string> members, DateTimeOffset now)
    {
        var today = Today(now);
        var result = new List<FreeWindow>();
        foreach (var window in windows)
        {
            if (!members.Contains(window.MemberId) || window.Start >= window.End)
                throw new ArgumentException("Indique um membro ativo e um intervalo com início antes do fim.");
            // A conversation may cross midnight: old windows must never become tomorrow's availability.
            if (window.Date < today || window.Date > today.AddDays(1)) continue;
            var start = Instant(window.Date, window.Start);
            var end = Instant(window.Date, window.End);
            if (end <= now) continue;
            if (start < now)
            {
                var nextMinute = new DateTimeOffset(now.UtcDateTime.AddTicks(-now.UtcDateTime.Ticks % TimeSpan.TicksPerMinute).AddMinutes(1), TimeSpan.Zero);
                var local = TimeZoneInfo.ConvertTime(nextMinute, Zone);
                if (DateOnly.FromDateTime(local.DateTime) != window.Date || nextMinute >= end) continue;
                result.Add(window with { Start = TimeOnly.FromDateTime(local.DateTime) });
            }
            else result.Add(window);
        }
        return result.OrderBy(w => w.Date).ThenBy(w => w.Start).ToArray();
    }

    private static DateTimeOffset Instant(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(local)) throw new ArgumentException("Esse horário não existe em Lisboa devido à mudança de hora.");
        var offset = Zone.IsAmbiguousTime(local) ? Zone.GetAmbiguousTimeOffsets(local).Max() : Zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }

    public static DayPlan Schedule(IReadOnlyList<PlanningTask> tasks, IEnumerable<FreeWindow> windows,
        IReadOnlyList<TaskEstimate> estimates, IReadOnlyList<PlanningChoice> choices, IReadOnlySet<string> members, DateTimeOffset now)
    {
        if (estimates.Any(e => e.Minutes is < 1 or > 720) || estimates.Select(e => e.TaskId).Distinct().Count() != estimates.Count)
            throw new ArgumentException("As durações devem ser únicas por tarefa e estar entre 1 e 720 minutos.");
        if (choices.Select(c => c.TaskId).Distinct().Count() != choices.Count || choices.Any(c => c.MemberId is not null && !members.Contains(c.MemberId)))
            throw new ArgumentException("Responsável sugerido inválido.");
        var slots = new List<Slot>();
        foreach (var group in ValidateWindows(windows, members, now).GroupBy(w => w.MemberId))
        {
            foreach (var window in group.OrderBy(w => Instant(w.Date, w.Start)))
            {
                var start = Instant(window.Date, window.Start); var end = Instant(window.Date, window.End);
                // During the repeated autumn hour, local times alone can point at an earlier instant.
                if (start < now) start = new DateTimeOffset(now.UtcDateTime.AddTicks(-now.UtcDateTime.Ticks % TimeSpan.TicksPerMinute).AddMinutes(1), TimeSpan.Zero);
                if (start >= end) continue;
                var previous = slots.LastOrDefault(s => s.MemberId == group.Key);
                if (previous is not null && start <= previous.End) previous.End = end > previous.End ? end : previous.End;
                else slots.Add(new Slot(group.Key, start, end));
            }
        }
        var today = Today(now); var planned = new List<PlannedTask>(); var unplaced = new List<UnplacedTask>();
        var ranking = choices.Select((c, i) => (c.TaskId, i)).ToDictionary(x => x.TaskId, x => x.i);
        foreach (var task in tasks.Where(t => Eligible(t.Due, now)).OrderBy(t => t.Due is null ? 3 : t.Due <= today ? 0 : t.Due <= today.AddDays(1) ? 1 : 2)
            .ThenBy(t => t.Due).ThenBy(t => ranking.GetValueOrDefault(t.Id, int.MaxValue)).ThenBy(t => t.Id))
        {
            var estimate = estimates.FirstOrDefault(e => e.TaskId == task.Id);
            if (estimate is null) { unplaced.Add(new(task.Id, task.Title, "Duração por confirmar")); continue; }
            var choice = choices.FirstOrDefault(c => c.TaskId == task.Id);
            var owner = choice?.MemberId is not null && (task.AssigneeId is null || task.AssigneeId == choice.MemberId || choice.AllowReassignment)
                ? choice.MemberId : task.AssigneeId;
            var slot = slots.Where(s => (owner is null || s.MemberId == owner) && s.Cursor.AddMinutes(estimate.Minutes) <= s.End)
                .OrderBy(s => s.Cursor).ThenBy(s => s.MemberId).FirstOrDefault();
            if (slot is null) { unplaced.Add(new(task.Id, task.Title, "Sem tempo disponível")); continue; }
            var finish = slot.Cursor.AddMinutes(estimate.Minutes);
            var reason = task.Due < today ? "Tarefa atrasada" : task.Due <= today.AddDays(1) ? "Prazo próximo" : choice?.Reason ?? "Cabe no tempo disponível";
            planned.Add(new(task.Id, task.Title, slot.MemberId, TimeZoneInfo.ConvertTime(slot.Cursor, Zone), TimeZoneInfo.ConvertTime(finish, Zone), estimate.Minutes, reason));
            slot.Cursor = finish;
        }
        return new(planned.OrderBy(p => p.Start).ThenBy(p => p.MemberId).ToArray(), unplaced.ToArray());
    }
    private sealed class Slot(string memberId, DateTimeOffset start, DateTimeOffset end)
    {
        public string MemberId { get; } = memberId;
        public DateTimeOffset Cursor { get; set; } = start;
        public DateTimeOffset End { get; set; } = end;
    }
}
