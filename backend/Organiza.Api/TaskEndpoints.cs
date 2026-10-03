using Microsoft.EntityFrameworkCore;
using Organiza.Domain;
namespace Organiza.Api;
public static class TaskEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/tasks",async (AppDb db) => Results.Ok(new{today=TaskRules.Today,items=await db.TaskItems.Where(x=>!x.Archived || x.Status=="done").OrderBy(x=>x.Due).ToListAsync()}));
        api.MapGet("/tasks/forecast",async (DateOnly from,DateOnly through,AppDb db) => {
            if(through<from || through.DayNumber-from.DayNumber>370) throw new ArgumentException("Escolha um período até 370 dias.");
            var active=await db.TaskItems.Where(x=>!x.Archived && x.Status!="done" && x.IntervalDays!=null && x.Due!=null).ToListAsync();
            return Results.Ok(active.SelectMany(x=>TaskRules.Forecast(x.Due!.Value,x.IntervalDays!.Value,TaskRules.Today,from,through).Select(date=>new{sourceId=x.Id,x.SeriesId,x.Title,x.Description,x.AssigneeId,due=date,predicted=true})));
        });
        api.MapPost("/tasks",async (TaskWrite input,AppDb db) => {
            var task=new TaskItem();await Apply(task,input,db);db.TaskItems.Add(task);await db.SaveChangesAsync();return Results.Ok(task);
        });
        api.MapPut("/tasks/{id:guid}",async (Guid id,TaskWrite input,AppDb db) => {
            var task=await db.TaskItems.FindAsync(id);if(task is null || task.Archived) return Results.NotFound();
            if(task.Status=="done") return Results.BadRequest(new{message="O histórico de tarefas concluídas é preservado."});
            if(task.Revision!=input.Revision) return Results.Conflict();await Apply(task,input,db);task.Revision++;await db.SaveChangesAsync();return Results.Ok(task);
        });
        api.MapPost("/tasks/{id:guid}/status",async (Guid id,StatusWrite input,AppDb db,HttpContext c) => {
            if(!new[]{"todo","doing","done"}.Contains(input.Status)) throw new ArgumentException("Estado inválido.");
            await using var tx=await db.Database.BeginTransactionAsync();
            var task=await Lock(db,id);if(task is null || task.Archived) return Results.NotFound();
            if(task.Status==input.Status) return Results.Ok(task);
            if(task.Status=="done") return Results.BadRequest(new{message="Use Desfazer conclusão para reabrir a tarefa."});
            if(task.Revision!=input.Revision) return Results.Conflict(new{message="A tarefa mudou. Atualize o quadro."});
            task.Status=input.Status;task.Revision++;
            if(input.Status=="done") { task.CompletedAt=DateTimeOffset.UtcNow;task.CompletedBy=AuthEndpoints.UserId(c); }
            // Persist the completed row before inserting the next one: the filtered index permits one active occurrence.
            await db.SaveChangesAsync();
            if(input.Status=="done" && task.IntervalDays is >0) db.TaskItems.Add(new TaskItem {
                SeriesId=task.SeriesId,PreviousId=task.Id,Title=task.Title,Description=task.Description,AssigneeId=task.AssigneeId,
                Due=TaskRules.Next(TaskRules.Today,task.IntervalDays.Value),IntervalDays=task.IntervalDays,IntervalCount=task.IntervalCount,IntervalUnit=task.IntervalUnit
            });
            await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(task);
        });
        api.MapPost("/tasks/{id:guid}/undo",async (Guid id,AppDb db) => {
            await using var tx=await db.Database.BeginTransactionAsync();
            var task=await Lock(db,id);if(task is null || task.Status!="done" || task.Archived) return Results.BadRequest(new{message="Esta conclusão não pode ser desfeita."});
            var next=await db.TaskItems.FromSqlInterpolated($"SELECT * FROM \"TaskItems\" WHERE \"PreviousId\"={id} FOR UPDATE").SingleOrDefaultAsync();
            if(task.IntervalDays is not null && (next is null || next.Status!="todo" || next.Revision!=0 || next.Archived || await db.CalendarCopies.AnyAsync(x=>x.TaskId==next.Id)))
                return Results.Conflict(new{message="A próxima ocorrência já foi alterada. A conclusão foi preservada."});
            if(next is not null) { db.TaskItems.Remove(next);await db.SaveChangesAsync(); }
            task.Status="todo";task.CompletedAt=null;task.CompletedBy=null;task.Revision++;await db.SaveChangesAsync();await tx.CommitAsync();return Results.Ok(task);
        });
        api.MapDelete("/tasks/{id:guid}",async (Guid id,int revision,AppDb db) => {
            await using var tx=await db.Database.BeginTransactionAsync();var task=await Lock(db,id);
            if(task is null || task.Archived) return Results.NotFound();
            if(task.Revision!=revision) return Results.Conflict();
            if(task.Status=="done") return Results.BadRequest(new{message="O histórico é preservado. Elimine a ocorrência ativa para terminar a rotina."});
            task.Archived=true;task.Revision++;await db.SaveChangesAsync();await tx.CommitAsync();return Results.NoContent();
        });
    }
    private static Task<TaskItem?> Lock(AppDb db,Guid id) => db.TaskItems.FromSqlInterpolated($"SELECT * FROM \"TaskItems\" WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync();
    private static async Task Apply(TaskItem task,TaskWrite input,AppDb db) {
        var title=input.Title.Trim();var description=(input.Description??"").Trim();
        if(title.Length is <1 or >160 || description.Length>4000) throw new ArgumentException("Título obrigatório até 160 caracteres; descrição até 4000.");
        if(input.AssigneeId is not null && !await db.Users.AnyAsync(x=>x.Id==input.AssigneeId && x.IsActive)) throw new ArgumentException("Responsável inválido.");
        if(input.IntervalCount is not null && input.Due is null) throw new ArgumentException("A primeira data é obrigatória nas recorrências.");
        task.Title=title;task.Description=description;task.AssigneeId=input.AssigneeId;task.Due=input.Due;
        task.IntervalDays=input.IntervalCount is null ? null : TaskRules.Interval(input.IntervalCount.Value,input.IntervalUnit??"");
        task.IntervalCount=input.IntervalCount;task.IntervalUnit=input.IntervalCount is null ? null : input.IntervalUnit;
    }
}
