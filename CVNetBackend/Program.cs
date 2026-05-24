using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using CVNetBackend.Services;
using CVNetBackend.Enhancer;
using dotenv.net;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using CVNetBackend.ProfileHandler;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using CVNetBackend.JobRoleManager.Services;


var builder = WebApplication.CreateBuilder(args);

// 0. LOAD ENVIRONMENT VARIABLES
dotenv.net.DotEnv.Load();

// 1. SET ENVIRONMENT VARIABLE FOR GOOGLE SDK
var keyPath = Path.Combine(builder.Environment.ContentRootPath, "firebase-key.json");
Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", keyPath);

// 2. INITIALIZE FIREBASE ADMIN
if (File.Exists(keyPath))
{
    var json = File.ReadAllText(keyPath);
    FirebaseApp.Create(new AppOptions()
    {
        Credential = GoogleCredential.FromJson(json)
    });
}

// --- NEW: CONFIGURE CORS ---
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

// --- NEW: CONFIGURE JWT AUTHENTICATION (FIREBASE) ---
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
builder.Services.AddScoped<SkillMatrixEngine>();   // Shortened cleanly since you added the 'using' block at the top!

// 💡 SINGLETON LIFETIMES: Safe for cross-cutting context providers or pure computational utilities
builder.Services.AddSingleton<FirestoreService>();
builder.Services.AddSingleton<EnhancerService>();

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