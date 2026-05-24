using Google.Cloud.Firestore;

namespace CVNetBackend.Services;

public class FirestoreService
{
    private readonly FirestoreDb _db;
    private const string CollectionName = "Users";

    public FirestoreService()
    {
        // Assumes your GOOGLE_APPLICATION_CREDENTIALS env var is set in Program.cs
        _db = FirestoreDb.Create("cvnet2026-capstone");
    }

    /// <summary>
    /// HIGH-PROFESSIONAL FIX: Changed UpdateAsync to SetAsync with MergeAll.
    /// This seamlessly creates the document if it is missing, or updates it if present.
    /// </summary>
    public async Task UpdateUserField(string userId, string fieldName, object value)
    {
        DocumentReference userRef = _db.Collection(CollectionName).Document(userId);
        
        var updates = new Dictionary<string, object>
        {
            { fieldName, value }
        };

        // SetAsync + MergeAll creates missing documents automatically, stopping the NotFound error.
        await userRef.SetAsync(updates, SetOptions.MergeAll);
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

    /// <summary>
    /// Resolves data gaps for Single Sign-On (SSO) Google connections.
    /// Ensures base user fields exist in NoSQL without wiping out existing changes.
    /// </summary>
    public async Task UpsertUserDocument(string uid, string firstName, string lastName, string email)
    {
        var docRef = _db.Collection(CollectionName).Document(uid);
        var userData = new Dictionary<string, object>
        {
            { "firstName", firstName },
            { "lastName", lastName },
            { "email", email },
            { "updatedAt", Timestamp.GetCurrentTimestamp() }
        };

        // MergeAll keeps previous additions like custom job roles or bio sections completely safe
        await docRef.SetAsync(userData, SetOptions.MergeAll);
    }
}