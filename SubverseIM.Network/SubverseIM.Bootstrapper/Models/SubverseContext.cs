using Microsoft.EntityFrameworkCore;
using SubverseIM.Core;

namespace SubverseIM.Bootstrapper.Models
{
    public class SubverseContext : DbContext
    {
        public SubverseContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<SubverseMessage> Messages { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SubverseMessage>()
                .Property(x => x.OtherPeer)
                .HasConversion(v => v.ToString(), 
                v => SubversePeerId.FromString(v));

            modelBuilder.Entity<SubverseMessage>()
                .HasIndex(x => new { x.CallId, x.OtherPeer })
                .IsUnique();
        }
    }
}
