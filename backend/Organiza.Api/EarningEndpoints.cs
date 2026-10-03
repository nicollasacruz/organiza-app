using Microsoft.EntityFrameworkCore;
using Organiza.Domain;
namespace Organiza.Api;
public static class EarningEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/earnings",async (AppDb db) => Results.Ok(await db.Earnings.OrderBy(x=>x.Date).ThenBy(x=>x.Id).ToListAsync()));
        api.MapGet("/earnings/balances",async (string through,AppDb db) => {
            if(!DateOnly.TryParseExact(through+"-01","yyyy-MM-dd",out var date) || date<Earnings.Opening || date.Year>2100) throw new ArgumentException("Mês inválido.");
            var goals=(await db.EarningTargets.AsNoTracking().ToListAsync()).Select(x=>new MonthlyGoal(x.EffectiveMonth,x.TargetCents));
            return Results.Ok(Earnings.Roll((await db.Earnings.Select(x=>new{x.Date,x.Cents}).ToListAsync()).Select(x=>(x.Date,x.Cents)),date,goals));
        });
        api.MapGet("/earnings/target",async (AppDb db) => {
            var month=Earnings.CurrentMonth;
            var goal=await db.EarningTargets.AsNoTracking().Where(x=>x.EffectiveMonth<=month).OrderByDescending(x=>x.EffectiveMonth).FirstOrDefaultAsync();
            return Results.Ok(new{targetCents=goal?.TargetCents??Earnings.DefaultTargetCents,effectiveMonth=month,revision=goal?.EffectiveMonth==month?goal.Revision:0});
        });
        api.MapPut("/earnings/target",async (TargetWrite input,AppDb db) => {
            Earnings.ValidateTarget(input.TargetCents);
            // The server chooses the current Lisbon month; callers cannot change a past goal.
            var month=Earnings.CurrentMonth;
            var goal=await db.EarningTargets.FindAsync(month);
            if((goal?.Revision??0)!=input.Revision) return Results.Conflict(new{message="A meta foi alterada por outro membro. Atualize antes de guardar."});
            if(goal is null){goal=new EarningTarget{EffectiveMonth=month};db.EarningTargets.Add(goal);}
            goal.TargetCents=input.TargetCents;goal.Revision++;
            await db.SaveChangesAsync();
            return Results.Ok(new{goal.TargetCents,goal.EffectiveMonth,goal.Revision});
        });
        api.MapPost("/earnings/preview",(EarningWrite input) => Results.Ok(new{cents=Earnings.Calculate(new(input.Date,input.Kind,input.Quantity,input.Capacity))}));
        api.MapPost("/earnings",async (EarningWrite input,AppDb db) => {
            var record=new Earning();Apply(record,input);db.Earnings.Add(record);await db.SaveChangesAsync();return Results.Ok(record);
        });
        api.MapPut("/earnings/{id:guid}",async (Guid id,EarningWrite input,AppDb db) => {
            var record=await db.Earnings.FindAsync(id);if(record is null) return Results.NotFound();
            if(record.Revision!=input.Revision) return Results.Conflict(new{message="O registo mudou. Atualize antes de editar."});
            Apply(record,input);record.Revision++;await db.SaveChangesAsync();return Results.Ok(record);
        });
        api.MapDelete("/earnings/{id:guid}",async (Guid id,int revision,AppDb db) => {
            var record=await db.Earnings.FindAsync(id);if(record is null) return Results.NotFound();
            if(record.Revision!=revision) return Results.Conflict();db.Earnings.Remove(record);await db.SaveChangesAsync();return Results.NoContent();
        });
    }
    private static void Apply(Earning record,EarningWrite input) {
        record.Cents=Earnings.Calculate(new(input.Date,input.Kind,input.Quantity,input.Capacity));record.Date=input.Date;record.Kind=input.Kind;record.Quantity=input.Quantity;record.Capacity=input.Kind==EarningKind.Sala ? null : input.Capacity;
    }
}
