using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TipGame.Infrastructure.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var basePath = Directory.GetCurrentDirectory();

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(
    "Host=db.ejcuoqbfssefkeinlkly.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=L0hcBYRSVIZEcJm3;SSL Mode=Require;Trust Server Certificate=true");
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
