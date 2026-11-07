using DragonFlix.Api.Data;
using DragonFlix.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, WebRootPath = "wwwroot" });
// اسرار (مثل توکن ربات) در این فایلِ محلی که وارد گیت نمی‌شود نگه داشته می‌شوند
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
TicketPdf.RegisterFonts();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=dragonflix.db"));

builder.Services.AddSingleton<TokenService>();
builder.Services.Configure<TmdbOptions>(builder.Configuration.GetSection("Tmdb"));
builder.Services.AddHttpClient<TmdbClient>(c => { c.Timeout = TimeSpan.FromSeconds(12); c.DefaultRequestHeaders.UserAgent.ParseAdd("DragonFlix/1.0"); });
builder.Services.AddSingleton<MovieImages>();

// تلگرام
builder.Services.Configure<DragonFlix.Api.Services.Telegram.TelegramOptions>(builder.Configuration.GetSection("Telegram"));
builder.Services.AddSingleton(sp => new DragonFlix.Api.Services.Telegram.TelegramApi(
    new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) }) { Timeout = Timeout.InfiniteTimeSpan },
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<DragonFlix.Api.Services.Telegram.TelegramOptions>>(),
    sp.GetRequiredService<ILogger<DragonFlix.Api.Services.Telegram.TelegramApi>>()));
builder.Services.AddScoped<AppSettings>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<ReleaseNotifier>();
builder.Services.AddScoped<DragonFlix.Api.Services.Telegram.BotSetup>();
builder.Services.AddScoped<DragonFlix.Api.Services.Telegram.BotUpdateHandler>();
builder.Services.AddHostedService<DragonFlix.Api.Services.Telegram.TelegramBotService>();
builder.Services.AddHostedService<DragonFlix.Api.Services.Telegram.NotificationDispatcher>();
builder.Services.AddHostedService<DragonFlix.Api.Services.Telegram.Scheduler>();
builder.Services.AddSingleton<ReceiptStore>();
builder.Services.AddSingleton<BackupService>();
builder.Services.AddControllers();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = TokenService.Key(builder.Configuration),
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
PosterSource.Init(app.Environment);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    SchemaPatches.Apply(db);
    Seeder.Seed(db, app.Configuration);
}

// در محیط واقعی کلید پیش‌فرض JWT خطرناک است (هر کسی می‌تواند توکن جعل کند)؛ بدون کلید واقعی بالا نمی‌آید
if (app.Environment.IsProduction() && (app.Configuration["Jwt:Key"] ?? "").Contains("CHANGE-ME", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Jwt:Key هنوز مقدار پیش‌فرض است. متغیر JWT_KEY را با یک رشتهٔ تصادفی بلند تنظیم کنید.");

app.MapGet("/api/health", () => Results.Ok(new { ok = true }));
app.UseStaticFiles();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
