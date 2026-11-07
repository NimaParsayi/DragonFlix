using DragonFlix.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DragonFlix.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Cinema> Cinemas => Set<Cinema>();
    public DbSet<Hall> Halls => Set<Hall>();
    public DbSet<SeatRow> SeatRows => Set<SeatRow>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Showtime> Showtimes => Set<Showtime>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookedSeat> BookedSeats => Set<BookedSeat>();
    public DbSet<SeatHold> SeatHolds => Set<SeatHold>();
    public DbSet<HomeSlide> HomeSlides => Set<HomeSlide>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<MovieFollow> MovieFollows => Set<MovieFollow>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();
        b.Entity<User>().HasIndex(u => u.TelegramId).IsUnique();
        b.Entity<AppSetting>().HasKey(x => x.Key);
        b.Entity<MovieFollow>().HasIndex(x => new { x.UserId, x.MovieId }).IsUnique();
        b.Entity<MovieFollow>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<MovieFollow>().HasOne<Movie>().WithMany().HasForeignKey(x => x.MovieId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Notification>().HasIndex(x => x.DedupeKey).IsUnique();
        b.Entity<Notification>().HasIndex(x => new { x.SentAt, x.SendAfter });
        b.Entity<Notification>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Hall>().HasOne(h => h.Cinema).WithMany(c => c.Halls)
            .HasForeignKey(h => h.CinemaId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<SeatRow>().HasOne(r => r.Hall).WithMany(h => h.Rows)
            .HasForeignKey(r => r.HallId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<SeatRow>().Property(r => r.Price).HasPrecision(18, 2);

        b.Entity<Showtime>().HasOne(s => s.Movie).WithMany()
            .HasForeignKey(s => s.MovieId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Showtime>().HasOne(s => s.Hall).WithMany()
            .HasForeignKey(s => s.HallId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Showtime>().HasIndex(s => new { s.HallId, s.StartsAt });

        b.Entity<Booking>().HasOne(x => x.Showtime).WithMany()
            .HasForeignKey(x => x.ShowtimeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Booking>().HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<Booking>().HasIndex(x => x.Code).IsUnique();
        b.Entity<HomeSlide>().HasOne(x => x.Movie).WithMany().HasForeignKey(x => x.MovieId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<HomeSlide>().HasIndex(x => x.MovieId).IsUnique();
        b.Entity<BookedSeat>().HasOne(s => s.Booking).WithMany(x => x.Seats)
            .HasForeignKey(s => s.BookingId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<BookedSeat>().HasOne(s => s.SeatRow).WithMany()
            .HasForeignKey(s => s.SeatRowId).OnDelete(DeleteBehavior.Restrict);
        // جلوگیری از رزرو دوبارهٔ یک صندلی در یک سانس، حتی در درخواست‌های همزمان
        b.Entity<SeatHold>().HasOne<Showtime>().WithMany().HasForeignKey(h => h.ShowtimeId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<SeatHold>().HasOne<SeatRow>().WithMany().HasForeignKey(h => h.SeatRowId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<SeatHold>().HasOne<User>().WithMany().HasForeignKey(h => h.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<SeatHold>().HasIndex(h => new { h.ShowtimeId, h.SeatRowId, h.SeatNumber }).IsUnique();
        b.Entity<BookedSeat>().HasIndex(s => new { s.ShowtimeId, s.SeatRowId, s.SeatNumber }).IsUnique();
    }
}
