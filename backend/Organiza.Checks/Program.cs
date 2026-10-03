using Organiza.Domain;
var checks = 0;
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Esperado {expected}, obtido {actual}"); checks++; }
void Invalid(Action action) { try { action(); } catch (ArgumentException) { checks++; return; } throw new Exception("A entrada inválida foi aceite."); }
var date = new DateOnly(2026, 8, 3);
Equal(1750, Earnings.Calculate(new(date, EarningKind.Sala, 2.5m, null)));
foreach (var (people, capacity, tier) in new[] { (49,100,0),(50,100,1),(89,100,1),(90,100,2),(0,16,0),(11,13,1),(12,13,2) })
{
    Equal(new[] {750,975,1060}[tier], Earnings.Calculate(new(date,EarningKind.Aula30,people,capacity)));
    Equal(new[] {1300,1600,1750}[tier], Earnings.Calculate(new(date,EarningKind.Aula50,people,capacity)));
}
Invalid(() => Earnings.Calculate(new(date, EarningKind.Sala, 0, null)));
Invalid(() => Earnings.Calculate(new(date, EarningKind.Aula30, 2, 0)));
Invalid(() => Earnings.Calculate(new(date, EarningKind.Aula30, 2.5m, 13)));
Invalid(() => Earnings.Calculate(new(date, EarningKind.Aula30, 14, 13)));
var originalGoal = new MonthlyGoal(Earnings.Opening,50000);
var balances = Earnings.Roll(new[] { (date,50600), (date.AddMonths(1),48975) },date.AddMonths(2),[originalGoal]);
Equal(0, balances[0].OutgoingCents); Equal(0, balances[1].IncomingCents); Equal(48975, balances[1].AvailableCents); Equal(1025, balances[1].ShortfallCents); Equal(0, balances[2].IncomingCents);
var excess = Earnings.Roll(new[] { (date.AddMonths(1),70000), (date.AddMonths(2),40000) },date.AddMonths(4),[originalGoal]);
Equal(20000,excess[2].IncomingCents); Equal(10000,excess[3].IncomingCents); Equal(0,excess[4].IncomingCents);
foreach(var days in new[] {1,3,7,15,30,60,90,180}) Equal(date.AddDays(days),TaskRules.Next(date,days));
Equal(30,TaskRules.Interval(1,"month")); Equal(21,TaskRules.Interval(3,"week")); Invalid(()=>TaskRules.Interval(0,"day"));
Equal("overdue",TaskRules.Urgency(date,false,date.AddDays(1))); Equal("soon",TaskRules.Urgency(date.AddDays(6),false,date)); Equal("normal",TaskRules.Urgency(date.AddDays(7),false,date)); Equal("normal",TaskRules.Urgency(date,true,date.AddDays(1)));
Equal(date.AddDays(10),TaskRules.Forecast(date,7,date.AddDays(3),date,date.AddDays(30)).First());
Equal(new DateOnly(2027,1,15),TaskRules.Next(new(2026,12,16),30));
var october = new DateOnly(2026,10,1);
var history = new[] {originalGoal,new MonthlyGoal(october,60000),new MonthlyGoal(october.AddMonths(1),55000)};
var entries = new[] {(date,50600),(date.AddMonths(1),48975),(october,65000),(october.AddMonths(1),100000)};
var changed = Earnings.Roll(entries,october.AddMonths(2),history);
Equal(balances[0],changed[0]);Equal(balances[1],changed[1]);
Equal(60000,changed[2].TargetCents);Equal(5000,changed[2].OutgoingCents);
Equal(55000,changed[3].TargetCents);Equal(5000,changed[3].IncomingCents);Equal(50000,changed[3].OutgoingCents);
Equal(55000,changed[4].TargetCents);Equal(5000,changed[4].ShortfallCents);
var revised = Earnings.Roll(entries,october,[originalGoal,new MonthlyGoal(october,45000)]);
Equal(changed[0],revised[0]);Equal(changed[1],revised[1]);Equal(20000,revised[2].OutgoingCents);
Invalid(()=>Earnings.ValidateTarget(0));Invalid(()=>Earnings.ValidateTarget(-1));
var corrected = Earnings.Roll(new[] {(date,50600),(date.AddMonths(1),48975)},october);
Equal(43000,corrected[0].TargetCents);Equal(0,corrected[0].OutgoingCents);
Equal(43000,corrected[1].TargetCents);Equal(0,corrected[1].IncomingCents);Equal(48975,corrected[1].AvailableCents);Equal(5975,corrected[1].OutgoingCents);
Equal(5975,corrected[2].IncomingCents);Equal(37025,corrected[2].ShortfallCents);
var changedAugust = Earnings.Roll(new[] {(date,90000),(date.AddMonths(1),48975)},october);
Equal(corrected[1],changedAugust[1]);Equal(corrected[2],changedAugust[2]);
var now = new DateTimeOffset(2026,10,3,18,30,20,TimeSpan.Zero); // 19:30 in Lisbon.
var planningDay = Planning.Today(now);
var family = new HashSet<string> { "a", "b" };
var late = Guid.NewGuid(); var nextDay = Guid.NewGuid(); var shared = Guid.NewGuid(); var unknownTime = Guid.NewGuid();
var planningTasks = new[] {
    new PlanningTask(late,"Atrasada","a",planningDay.AddDays(-2)),
    new PlanningTask(nextDay,"Amanhã","a",planningDay.AddDays(1)),
    new PlanningTask(shared,"Partilhada",null,null),
    new PlanningTask(unknownTime,"Sem estimativa",null,null)
};
var windows = new[] { new FreeWindow("a",planningDay,new(19,0),new(20,30)), new FreeWindow("a",planningDay,new(20,0),new(21,0)), new FreeWindow("b",planningDay,new(20,0),new(21,0)) };
var estimates = new[] { new TaskEstimate(late,30),new TaskEstimate(nextDay,30),new TaskEstimate(shared,45) };
var proposal = Planning.Schedule(planningTasks,windows,estimates,[],family,now);
Equal(3,proposal.Items.Length);Equal(late,proposal.Items[0].TaskId);Equal("a",proposal.Items[0].MemberId);
Equal(new TimeOnly(19,31),TimeOnly.FromDateTime(proposal.Items[0].Start.DateTime));
Equal("b",proposal.Items.Single(t=>t.TaskId==shared).MemberId);
Equal("Duração por confirmar",proposal.Unplaced.Single().Reason);
Equal(true,proposal.Items.All(t=>t.Start>=now));
foreach(var member in family) { var perMember=proposal.Items.Where(t=>t.MemberId==member).OrderBy(t=>t.Start).ToArray();for(int i=1;i<perMember.Length;i++)Equal(true,perMember[i].Start>=perMember[i-1].End); }
var tooLong=Planning.Schedule([planningTasks[0]],windows,[new(late,120)],[],family,now);
Equal(0,tooLong.Items.Length);Equal("Sem tempo disponível",tooLong.Unplaced.Single().Reason);
var reassigned=Planning.Schedule([planningTasks[0]],windows,[new(late,30)],[new(late,"b",false,"")],family,now);
Equal("a",reassigned.Items.Single().MemberId);
reassigned=Planning.Schedule([planningTasks[0]],windows,[new(late,30)],[new(late,"b",true,"Pedido na conversa")],family,now);
Equal("b",reassigned.Items.Single().MemberId);
Equal(0,Planning.ValidateWindows([new("a",planningDay.AddDays(-1),new(10,0),new(11,0))],family,now).Length);
Equal(0,Planning.ValidateWindows([new("a",planningDay,new(10,0),new(11,0))],family,now).Length);
Equal(0,Planning.ValidateWindows([new("a",planningDay.AddDays(2),new(10,0),new(11,0))],family,now).Length);
Invalid(()=>Planning.ValidateWindows([new("missing",planningDay,new(20,0),new(21,0))],family,now));
Invalid(()=>Planning.ValidateWindows([new("a",planningDay,new(21,0),new(20,0))],family,now));
Invalid(()=>Planning.Schedule(planningTasks,windows,[new(late,0)],[],family,now));
Invalid(()=>Planning.Schedule(planningTasks,windows,[new(late,721)],[],family,now));
Invalid(()=>Planning.Schedule(planningTasks,windows,[new(late,30),new(late,45)],[],family,now));
Equal(false,Planning.Eligible(planningDay.AddDays(8),now));Equal(true,Planning.Eligible(planningDay.AddDays(7),now));
Equal(true,Planning.Eligible(null,now));Equal(true,Planning.Eligible(planningDay.AddDays(-100),now));
Equal(0,Planning.Schedule([],windows,[],[],family,now).Items.Length);
Equal(0,Planning.Schedule(planningTasks,[],estimates,[],family,now).Items.Length);
var midnight = new DateTimeOffset(2026,10,4,0,10,0,TimeSpan.Zero);
Equal(0,Planning.ValidateWindows([new("a",planningDay,new(20,0),new(21,0))],family,midnight).Length);
var spring = new DateTimeOffset(2026,3,28,12,0,0,TimeSpan.Zero);
Invalid(()=>Planning.ValidateWindows([new("a",new(2026,3,29),new(1,15),new(2,15))],family,spring));
var autumn = new DateTimeOffset(2026,10,25,1,30,0,TimeSpan.Zero);
var autumnPlan = Planning.Schedule([new(late,"Teste mudança de hora","a",null)],[new("a",new(2026,10,25),new(1,0),new(3,0))],[new(late,20)],[],family,autumn);
Equal(true,autumnPlan.Items.Single().Start>=autumn);
Console.WriteLine($"{checks} verificações de regras aprovadas.");
