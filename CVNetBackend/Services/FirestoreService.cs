using Google.Cloud.Firestore;

namespace CVNetBackend.Services;

public class FirestoreService
{
    private readonly FirestoreDb _db;
    private const string CollectionName = "Users";

    public FirestoreService()
    {
        // This assumes your GOOGLE_APPLICATION_CREDENTIALS env var is set in Program.cs
        _db = FirestoreDb.Create("cvnet2026-capstone");
    }

    // This method will create the field automatically if it's missing
    public async Task UpdateUserField(string userId, string fieldName, object value)
    {
        DocumentReference userRef = _db.Collection(CollectionName).Document(userId);
        
        // This will now update "profileImageUrl" to match your schema
        await userRef.UpdateAsync(fieldName, value);
    }

    public async Task CreateUserDocument(string uid, string firstName, string lastName, string email)
    {
        var docRef = _db.Collection(CollectionName).Document(uid);
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