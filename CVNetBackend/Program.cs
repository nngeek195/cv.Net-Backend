using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using CVNetBackend.Services;
using CVNetBackend.Enhancer;
using dotenv.net;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using CVNetBackend.ProfileHandler;

var builder = WebApplication.CreateBuilder(args);

// 0. LOAD ENVIRONMENT VARIABLES
dotenv.net.DotEnv.Load();

// 1. SET ENVIRONMENT VARIABLE FOR GOOGLE SDK
var keyPath = Path.Combine(builder.Environment.ContentRootPath, "firebase-key.json");
Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", keyPath);

// 2. INITIALIZE FIREBASE ADMIN
if (File.Exists(keyPath))
{
    // Fix: Use FromJson with File.ReadAllText to avoid the obsolete warning
    var json = File.ReadAllText(keyPath);
    FirebaseApp.Create(new AppOptions()
    {
        Credential = GoogleCredential.FromJson(json)
    });
}


builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api-limiter", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5; // Allow only 5 requests per minute per user
        opt.QueueLimit = 2;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

// 3. REGISTER SERVICES
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<FirestoreService>();
builder.Services.AddSingleton<EnhancerService>();
builder.Services.AddSingleton<ProfileService>(); 

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.MapControllers().RequireRateLimiting("api-limiter");

app.Run();