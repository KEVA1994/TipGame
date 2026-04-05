using Microsoft.EntityFrameworkCore;
using TipGame.Domain;
using TipGame.Domain.Entities;


namespace TipGame.Infrastructure.Data
{

    public class AppDbContext : DbContext
    {
        public DbSet<Match> Matches => Set<Match>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Prediction> Predictions => Set<Prediction>();

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.ClientId)
                .IsUnique();

            modelBuilder.Entity<Prediction>()
                .HasIndex(p => new { p.UserId, p.MatchId })
                .IsUnique();
        }
    }
}
