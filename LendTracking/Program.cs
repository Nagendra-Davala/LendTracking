using System.Text;
using System.Threading.RateLimiting;
using LendTracking.Data;
using LendTracking.Model.Auth;
using LendTracking.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Add CORS policy
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? throw new InvalidOperationException("Cors:AllowedOrigins is not configured.");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddDbContext<LendTrackingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<LendingService>();
builder.Services.AddScoped<GroupService>();
builder.Services.AddScoped<SimpleInterestService>();
builder.Services.AddSingleton<PendingActionStore>();
builder.Services.AddScoped<AgentService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<SpeechService>();
builder.Services.AddHostedService<SelfPingBackgroundService>();

// Falls back to logging the link when Brevo is not configured.
if (string.IsNullOrWhiteSpace(builder.Configuration["Email:Brevo:ApiKey"]))
{
    builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();
}
else
{
    builder.Services.AddHttpClient<IEmailSender, BrevoEmailSender>();
}

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// Shared across signup/signin/resend-verification/forgot-password/reset-password: unauthenticated
// endpoints with no other identity to key on, so partition by caller IP. Keyed by IP *and* path so
// each endpoint gets its own independent budget — otherwise normal back-and-forth testing across
// several of these in a row (sign in, resend, forgot-password) would exhaust one shared pool.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("AuthLimiter", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{httpContext.Connection.RemoteIpAddress}:{httpContext.Request.Path}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
            }));

    options.OnRejected = (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(
            new { success = false, message = "Too many attempts. Please wait a few minutes and try again." },
            ct));
    };
});

builder.Services.AddControllers();

// Return validation failures in the same shape as AuthResult so the UI can read .message.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var message = context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
            ?? "Please check the details you entered.";

        return new BadRequestObjectResult(AuthResult.Fail(message));
    };
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT returned by /api/auth/signin."
    });
    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer"), new List<string>() }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "LendTracking API V1");
    });
}

app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// No auth, no CORS needed: this only exists for Render's own health checks and our
// self-ping keep-alive to see as "recent traffic" — never called by the frontend.
// Touches the database (not just the web process) so Neon's own auto-suspend never
// kicks in either — otherwise the first real query after Neon idles out (e.g. Google
// sign-in) pays that resume latency instead of this background ping.
app.MapGet("/api/health", async (LendTrackingDbContext db) =>
{
    await db.Database.CanConnectAsync();
    return Results.Ok(new { status = "ok", utc = DateTime.UtcNow });
});

app.MapControllers();

app.Run();
