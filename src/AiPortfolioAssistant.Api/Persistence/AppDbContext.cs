using AiPortfolioAssistant.Api.Unanswered;
using Microsoft.EntityFrameworkCore;

namespace AiPortfolioAssistant.Api.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UnansweredQuestion> UnansweredQuestions => Set<UnansweredQuestion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UnansweredQuestion>()
            .Property(q => q.Status)
            .HasConversion<string>();  
    }
}