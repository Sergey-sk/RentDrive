using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RentDrive.db.models;

namespace RentDrive.db
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public DbSet<RentItem> RentItems { get; set; }
        public DbSet<Booking> Bookings { get; set; }

        public ApplicationDbContext(DbContextOptions options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<RentItem>()
                .HasOne(item => item.Owner)
                .WithMany(u => u.OwnedItems)
                .HasForeignKey(item => item.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RentItem>()
                .Property(i => i.Description)
                .HasColumnType("longtext");

            builder.Entity<RentItem>()
                .Property(i => i.Title)
                .HasMaxLength(255);

            builder.Entity<RentItem>().HasIndex(i => i.PricePerDay);
            builder.Entity<RentItem>().HasIndex(i => i.CreatedAt);

            builder.Entity<RentItem>()
                .HasIndex(i => new { i.Title, i.Description })
                .IsFullText();

            builder.Entity<RentItem>().HasQueryFilter(i => !i.IsDeleted);

            builder.Entity<Booking>()
                .HasOne(b => b.Customer)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.CustomerId);

            builder.Entity<Booking>()
                .HasOne(b => b.RentItem)
                .WithMany(item => item.Bookings)
                .HasForeignKey(b => b.RentItemId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
