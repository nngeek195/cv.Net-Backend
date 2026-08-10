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
using CVNetBackend.Company_End.CandidateSection.Services;

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

// Allow browser clients from approved origins.
builder.Services.AddCors(options =>
{
    options.AddPolicy("CVNetCorsPolicy", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Validate Firebase JWT tokens.
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

// Apply a simple request rate limit.
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api-limiter", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
        opt.QueueLimit = 10;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

builder.Services.AddScoped<DatabaseService>();     
builder.Services.AddScoped<ProfileService>();      
builder.Services.AddScoped<SkillMatrixEngine>();   
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AdminService>();

builder.Services.AddScoped<CVNetBackend.User_End.JobApply.Services.CandidateJobService>();
builder.Services.AddScoped<CVNetBackend.User_End.JobApply.Services.ApplicationService>();

builder.Services.AddScoped<CVNetBackend.Company_End.JobManagement.Services.CompanyJobService>();
builder.Services.AddScoped<CVNetBackend.Company_End.Services.CompanyProfileService>();
builder.Services.AddScoped<CVNetBackend.Company_End.ApplicationsView.Services.JobDetailsService>();
builder.Services.AddScoped<CVNetBackend.Company_End.Interviews.Services.InterviewService>();
builder.Services.AddScoped<CVNetBackend.Company_End.Services.CompanyDashboardService>();
builder.Services.AddScoped<CandidateService>(); 

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

app.UseRouting();
app.UseCors("CVNetCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireRateLimiting("api-limiter");

app.Run();