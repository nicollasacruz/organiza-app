using Microsoft.EntityFrameworkCore;
namespace Organiza.Api;

public static class Schema
{
    public static async Task Initialize(AppDb db)
    {
        await db.Database.EnsureCreatedAsync();
        // Additive upgrade for databases created before configurable monthly goals existed.
        // EnsureCreated preserves an existing schema, so this table must also be added explicitly.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "EarningTargets" (
                "EffectiveMonth" date NOT NULL,
                "TargetCents" integer NOT NULL CHECK ("TargetCents" > 0),
                "Revision" integer NOT NULL,
                CONSTRAINT "PK_EarningTargets" PRIMARY KEY ("EffectiveMonth")
            )
            """);
    }
}
