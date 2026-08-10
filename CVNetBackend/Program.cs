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

// 1. ✅ DYNAMIC CORS CONFIGURATION
builder.Services.AddCors(options =>
{
    options.AddPolicy("CVNetCorsPolicy", policy =>
    {
        policy.SetIsOriginAllowed(origin => true) // Allows localhost, 10.x.x.x IPs, EC2 IPs, Vercel
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 2. CONFIGURE JWT AUTHENTICATION (FIREBASE)
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
        opt.PermitLimit = 100;
        opt.QueueLimit = 10;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

// 4. REGISTER SERVICES
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

// 5. CRITICAL: MIDDLEWARE PIPELINE ORDER
app.UseRouting();               // Ensure routing context is initialized first
app.UseCors("CVNetCorsPolicy"); // 1. Pass CORS check
app.UseAuthentication();        // 2. Verify Firebase JWT Token
app.UseAuthorization();         // 3. Check Permissions

app.MapControllers().RequireRateLimiting("api-limiter");

app.Run();