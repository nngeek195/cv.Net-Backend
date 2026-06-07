using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using CVNetBackend.Services;
using CVNetBackend.User_End.Enhancer;
using dotenv.net;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using CVNetBackend.ProfileHandler;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using CVNetBackend.JobRoleManager.Services;
using CVNetBackend.User_End.JobApply.Services;

// 0. LOAD ENVIRONMENT VARIABLES FIRST!
// This MUST happen before WebApplication.CreateBuilder so the .NET framework 
// configuration engine caches your .env variables on startup.
DotEnv.Load();

var root = Directory.GetCurrentDirectory();
var dotenvPath = Path.Combine(root, ".env");

if (File.Exists(dotenvPath))
{
    Console.WriteLine($"\n✅ [SUCCESS] Loading .env file from: {dotenvPath}\n");
    DotEnv.Load(options: new DotEnvOptions(envFilePaths: new[] { dotenvPath }));
}
else
{
    Console.WriteLine($"\n🚨 [CRITICAL WARNING] No .env file found at: {dotenvPath} - Database will fail!\n");
}

var builder = WebApplication.CreateBuilder(args);

// --- CONFIGURE CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("CVNetCorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000") // Your Next.js local URL
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// --- CONFIGURE JWT AUTHENTICATION (FIREBASE) ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://securetoken.google.com/cvnet2026-capstone";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://securetoken.google.com/cvnet2026-capstone",
            ValidateAudience = true,
            ValidAudience = "cvnet2026-capstone",
            ValidateLifetime = true
        };
    });

// 3. RATE LIMITING
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api-limiter", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5; 
        opt.QueueLimit = 2;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

// 4. REGISTER SERVICES

// 💡 SCOPED LIFETIMES: Required for anything executing isolated database tasks per-request
builder.Services.AddScoped<DatabaseService>();     
builder.Services.AddScoped<ProfileService>();      
builder.Services.AddScoped<SkillMatrixEngine>();   
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AdminService>();

// 💡 SINGLETON LIFETIMES: Safe for cross-cutting context providers or pure computational utilities
builder.Services.AddSingleton<FirestoreService>();
builder.Services.AddSingleton<EnhancerService>();
builder.Services.AddScoped<CVNetBackend.Company_End.Services.CompanyJobService>();
builder.Services.AddScoped<CVNetBackend.User_End.JobApply.Services.CandidateJobService>();
builder.Services.AddScoped<CVNetBackend.User_End.JobApply.Services.ApplicationService>();
builder.Services.AddScoped<CVNetBackend.Company_End.Services.CompanyProfileService>();
builder.Services.AddScoped<CVNetBackend.Company_End.JobManagement.Services.CompanyJobService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --- CRITICAL: MIDDLEWARE ORDER ---
app.UseCors("CVNetCorsPolicy"); // 1. Allow access from Next.js
app.UseAuthentication();        // 2. Verify the Token
app.UseAuthorization();         // 3. Check Permissions

app.MapControllers().RequireRateLimiting("api-limiter");

app.Run();