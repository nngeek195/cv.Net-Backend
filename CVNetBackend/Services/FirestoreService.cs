using Google.Cloud.Firestore;

namespace CVNetBackend.Services;

public class FirestoreService
{
    private readonly FirestoreDb _db;

    public FirestoreService()
    {
        // This assumes your GOOGLE_APPLICATION_CREDENTIALS env var is set in Program.cs
        _db = FirestoreDb.Create("cvnet2026-capstone");
    }

    public async Task CreateUserDocument(string uid, string firstName, string lastName, string email)
    {
        var docRef = _db.Collection("Users").Document(uid);
        var userData = new Dictionary<string, object>
        {
            { "firstName", firstName },
            { "lastName", lastName },
            { "email", email },
            { "role", "candidate" },
            { "createdAt", Timestamp.GetCurrentTimestamp() }
        };
        await docRef.SetAsync(userData);
    }
}