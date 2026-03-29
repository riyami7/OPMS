using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using OperationalPlanMS.Data;
using OperationalPlanMS.Services;
using System.IO.Compression;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// DbContext with connection pooling
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Response Compression (gzip/brotli)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/javascript",
        "text/css",
        "application/json",
        "image/svg+xml"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.SmallestSize);

// Add Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "OPS.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

// Add Authorization
builder.Services.AddAuthorization();

// Rate Limiting — حماية من brute force
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // حد عام للطلبات
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(5);
        opt.PermitLimit = 10;
        opt.QueueLimit = 0;
    });

    // حد لكل IP على API endpoints
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 60;
        opt.QueueLimit = 2;
    });
});

// Add MVC services
var mvcBuilder = builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<OperationalPlanMS.Filters.PendingApprovalsFilter>();
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<OperationalPlanMS.Filters.ChatbotSettingsFilter>();
})
.AddJsonOptions(options =>
{
    // Enums as strings in JSON (e.g. "InProgress" not 3)
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}

// CORS — allow external systems (chatbot, BI tools) to call /api/data/*
builder.Services.AddCors(options =>
{
    options.AddPolicy("ExternalApi", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("ApiSettings:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:8000", "http://localhost:3000" };
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});


// Swagger — API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "OPMS Data API", Version = "v1",
        Description = "Read-only API for AI chatbot and external system integration" });
    c.AddSecurityDefinition("ApiKey", new()
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Name = "X-API-Key"
    });
    c.AddSecurityRequirement(new()
    {
        { new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "ApiKey" } }, Array.Empty<string>() }
    });
});

// External API Service
builder.Services.AddHttpClient<IExternalApiService, ExternalApiService>();
builder.Services.AddMemoryCache();
//builder.Services.AddExternalApiClients(builder.Configuration);


// User Management Service
builder.Services.AddScoped<IUserService, UserService>();

// Initiative Management Service
builder.Services.AddScoped<IInitiativeService, InitiativeService>();

// Project Management Service
builder.Services.AddScoped<IProjectService, ProjectService>();

// Step Management Service
builder.Services.AddScoped<IStepService, StepService>();

// Audit Service
builder.Services.AddScoped<IAuditService, AuditService>();

// === Ollama AI Services ===
builder.Services.Configure<OperationalPlanMS.Services.AI.Models.OllamaSettings>(
    builder.Configuration.GetSection("Ollama"));
builder.Services.AddHttpClient<OperationalPlanMS.Services.AI.IOllamaService,
    OperationalPlanMS.Services.AI.OllamaService>();
builder.Services.AddScoped<OperationalPlanMS.Services.AI.ChatContextBuilder>();
var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Swagger UI (available at /swagger)
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OPMS Data API v1"));

// CORS (before response compression)
app.UseCors("ExternalApi");

// Response Compression (before static files)
app.UseResponseCompression();

// Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none';";
    await next();
});

// Static Files with cache headers (CSS, JS, images cached 7 days)
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "public,max-age=604800";
    }
});
app.UseRouting();

// Rate Limiting
app.UseRateLimiter();

// Authentication & Authorization (order matters!)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed demo data (development only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(db);
}

await app.RunAsync();