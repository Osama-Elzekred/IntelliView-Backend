// Import the required packages
//==============================

using dotenv.net;
using InteliView.DataAccess.Data;
using IntelliView.API.Infrastructure;
using IntelliView.API.Services;
using IntelliView.DataAccess.Middlewares;
using IntelliView.DataAccess.Repository.IRepository;
using IntelliView.DataAccess.Repository.Repos;
using IntelliView.DataAccess.Services;
using IntelliView.DataAccess.Services.IService;
using IntelliView.Models.Models;
using IntelliView.Utility;
using IntelliView.Utility.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using System.Text;

// Set your Cloudinary credentials
//=================================

DotEnv.Load(options: new DotEnvOptions(probeForEnv: true));


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// for database sql server

builder.Configuration.AddEnvironmentVariables();

// Add a default appsettings if not present (for security, better to use environment variables)
if (string.IsNullOrEmpty(builder.Configuration["JWT:Key"]))
{
    // Try different environment variable formats
    var jwtKey = Environment.GetEnvironmentVariable("JWT__Key");

    if (string.IsNullOrEmpty(jwtKey))
    {
        jwtKey = Environment.GetEnvironmentVariable("JWT_KEY");
    }

    // Set configuration value if found in environment
    if (!string.IsNullOrEmpty(jwtKey))
    {
        builder.Configuration["JWT:Key"] = jwtKey;
    }
}

// Explicitly set other JWT settings if needed
if (string.IsNullOrEmpty(builder.Configuration["JWT:Issuer"]))
{
    var jwtIssuer = Environment.GetEnvironmentVariable("JWT__Issuer") ??
                    Environment.GetEnvironmentVariable("JWT_ISSUER");

    if (!string.IsNullOrEmpty(jwtIssuer))
    {
        builder.Configuration["JWT:Issuer"] = jwtIssuer;
    }
}

if (string.IsNullOrEmpty(builder.Configuration["JWT:Audience"]))
{
    var jwtAudience = Environment.GetEnvironmentVariable("JWT__Audience") ??
                       Environment.GetEnvironmentVariable("JWT_AUDIENCE");

    if (!string.IsNullOrEmpty(jwtAudience))
    {
        builder.Configuration["JWT:Audience"] = jwtAudience;
    }
}

if (string.IsNullOrEmpty(builder.Configuration["JWT:DurationInMinutes"]))
{
    var jwtDuration = Environment.GetEnvironmentVariable("JWT__DurationInMinutes") ??
                       Environment.GetEnvironmentVariable("JWT_DURATION_MINUTES");

    if (!string.IsNullOrEmpty(jwtDuration))
    {
        builder.Configuration["JWT:DurationInMinutes"] = jwtDuration;
    }
}

// Database connection
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("MonsterASPConnection_new");

    // If connection string is not found in configuration, try environment variable
    if (string.IsNullOrEmpty(connectionString))
    {
        connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MonsterASPConnection_new");

        // If still null, try fallback env variable
        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        }
    }

    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseSqlServer(connectionString);
    }
    else
    {
        throw new InvalidOperationException("Database connection string is not configured. Please set the connection string in environment variables.");
    }
});

// ... existing code ...

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IUploadFilesToCloud, UploadFilesToCloud>();
//builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
}).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();

// Configure JWT via options pattern to ensure it has values
builder.Services.Configure<JWT>(options =>
{
    // Only set if configuration values exist
    if (!string.IsNullOrEmpty(builder.Configuration["JWT:Key"]))
    {
        options.Key = builder.Configuration["JWT:Key"];
    }

    if (!string.IsNullOrEmpty(builder.Configuration["JWT:Issuer"]))
    {
        options.Issuer = builder.Configuration["JWT:Issuer"];
    }

    if (!string.IsNullOrEmpty(builder.Configuration["JWT:Audience"]))
    {
        options.Audience = builder.Configuration["JWT:Audience"];
    }

    if (!string.IsNullOrEmpty(builder.Configuration["JWT:DurationInMinutes"]))
    {
        options.DurationInMinutes = int.Parse(builder.Configuration["JWT:DurationInMinutes"]);
    }
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(o =>
    {
        // Ensure we have a key before trying to configure
        var jwtKey = builder.Configuration["JWT:Key"];
        if (string.IsNullOrEmpty(jwtKey))
        {
            throw new InvalidOperationException("JWT:Key is not configured. Please set JWT__Key environment variable.");
        }

        o.RequireHttpsMetadata = false;
        o.SaveToken = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidIssuer = builder.Configuration["JWT:Issuer"],
            ValidAudience = builder.Configuration["JWT:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

// Configure logging with appropriate log levels
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.ClearProviders();
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();

    // Set default log level to Information to capture all endpoint logs
    loggingBuilder.SetMinimumLevel(LogLevel.Information);

    // Filter out noisy framework logs
    loggingBuilder.AddFilter("Microsoft", LogLevel.Warning)
                  .AddFilter("System", LogLevel.Warning)
                  .AddFilter("Microsoft.AspNetCore", LogLevel.Warning)
                  .AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

    // Keep IntelliView logs at Information level to see endpoint logging
    loggingBuilder.AddFilter("IntelliView", LogLevel.Information);
});

// ... existing code ...

builder.Services.AddHttpClient<IAIModelApiService, AIModelApiClient>();
builder.Services.AddHttpClient<IAiSearchService, AiSearchService>();
builder.Services.AddTransient<IAuthService, AuthService>();
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddScoped<IVerifyService, VerifyService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IInterviewService, InterviewService>();
builder.Services.AddScoped<IJwtToken, JwtToken>();
builder.Services.AddScoped<IAvatarService, AvatarService>();
builder.Services.AddAutoMapper(typeof(Program).Assembly, typeof(IAuthService).Assembly);
builder.Services.AddControllers().AddNewtonsoftJson();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    options.OperationFilter<SecurityRequirementsOperationFilter>();
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("UserOrCompany", policy =>
    {
        policy.RequireRole(SD.ROLE_USER, SD.ROLE_COMPANY);
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();

    // Only use HTTPS redirection in development where we have the dev certificate
    app.UseHttpsRedirection();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("CorsPolicy");

// Add request logging middleware to log ALL endpoints
app.UseRequestLogging();

app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();
app.MapControllers();

app.Run();