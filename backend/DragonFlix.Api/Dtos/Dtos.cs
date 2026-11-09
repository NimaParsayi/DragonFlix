using System.ComponentModel.DataAnnotations;

namespace DragonFlix.Api.Dtos;

public record RegisterDto([Required, EmailAddress] string Email, [Required, MinLength(6)] string Password, [Required] string FullName);
public record LoginDto([Required] string Email, [Required] string Password);
public record UserDto(int Id, string Email, string FullName, string Role, string Username = "", long? TelegramId = null,
    string FirstName = "", string LastName = "", bool Registered = false);
public record ProfileDto(string? FirstName, string? LastName);
public record TelegramLoginDto([Required] string InitData);
public record NotifyPrefsDto(List<string> Enabled);
public record AuthResultDto(string Token, UserDto User);

public record CinemaDto([Required] string Name, [Required] string City, string? Address);
public record HallDto([Required] string Name);

/// <summary>
/// TmdbPosterPath / TmdbBackdropPath: مسیر تصویر در TMDB؛ سرور آن را دانلود و روی خودش ذخیره می‌کند.
/// PosterUrl / BackdropUrl: مقدار فعلی تصویر (خالی = حذف).
/// </summary>
public record MovieDto([Required] string Title, string? Description, string? Genre, [Range(1, 600)] int DurationMinutes,
    string? PosterUrl, string? BackdropUrl = null, string? OriginalTitle = null, int? ReleaseYear = null, int? TmdbId = null,
    string? TmdbPosterPath = null, string? TmdbBackdropPath = null, string? AgeRating = null,
    [MaxLength(120)] string? SpecialNote = null);

public record ShowtimeInputDto(int MovieId, int HallId, DateTime StartsAt, [Range(0.1, 10)] decimal PriceFactor);

/// <summary>یک ردیف در طراح چینش. Id برای ردیف‌های موجود پر است و برای ردیف جدید null.</summary>
public record RowLayoutDto(int? Id, string Label, int SeatCount, decimal Price, int Offset, List<int> AisleAfter, List<int>? Disabled = null);
public record HallLayoutDto(List<RowLayoutDto> Rows);

public record SeatSelectionDto(int RowId, int Number);
public record CreateBookingDto(int ShowtimeId, List<SeatSelectionDto> Seats);
public record HoldRequestDto(List<SeatSelectionDto> Seats);

public record SlideInputDto(int MovieId, [MaxLength(120)] string? Note);
public record SlideUpdateDto([MaxLength(120)] string? Note, bool Active);
public record SlideOrderDto(List<int> Ids);

public record RejectDto(string? Reason);
public record PaymentSettingsDto(string? CardNumber, string? CardHolder, string? CardBank, int Minutes, string? Note);
public record WebAppUrlDto(string? Url);
public record BroadcastDto([Required, MaxLength(1000)] string Text, string Category = "promo", string? ButtonText = null, string? ButtonPath = null);
