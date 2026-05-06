using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using CVNetBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. SET ENVIRONMENT VARIABLE FOR GOOGLE SDK
var keyPath = Path.Combine(builder.Environment.ContentRootPath, "firebase-key.json");
Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", keyPath);

// 2. INITIALIZE FIREBASE ADMIN
if (File.Exists(keyPath))
{
    FirebaseApp.Create(new AppOptions()
    {
        Credential = GoogleCredential.FromFile(keyPath)
    });
}

// 3. REGISTER SERVICES FOR DEPENDENCY INJECTION
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<FirestoreService>();

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

app.Run();