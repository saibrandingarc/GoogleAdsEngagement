using GoogleAdsEngagement.Models;
using Microsoft.EntityFrameworkCore;

namespace GoogleAdsEngagement.Context
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Define DbSets for your entities
        public DbSet<Settings> Settings { get; set; }
        public DbSet<SocialSettings> SocialSettings { get; set; }
        public DbSet<ClientDetail> ClientDetails { get; set; }
        public DbSet<GoogleAdsEngagementModel> GoogleAdsEngagementModels { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Configure entity properties and relationships here
        }
    }
}

