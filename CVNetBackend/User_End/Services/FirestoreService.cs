using Google.Cloud.Firestore;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore.V1;
using Grpc.Core;
using Grpc.Auth;
using FirebaseAdmin;
using FirebaseAdmin.Auth;

namespace CVNetBackend.Services;

public class FirestoreService
{
    private readonly FirestoreDb _db;
    
    // ✅ CRITICAL FIX: Changed "Users" to "users" (lowercase). 
    // Firestore is case-sensitive, and your Next.js app relies on the lowercase "users" collection!
    private const string CollectionName = "users";

    public FirestoreService()
    {
        // Explicit path mapping to your local credentials file in the current working directory
        string keyPath = Path.Combine(Directory.GetCurrentDirectory(), "firebase-key.json");

        if (!File.Exists(keyPath))
        {
            throw new FileNotFoundException($"[FIRESTORE ERROR] Security initialization failed. keyPath not found at: {keyPath}");
        }

        // Explicitly load credentials to bypass the Google ADC environment variable requirement
        var credential = GoogleCredential.FromFile(keyPath);
        
        // ✅ NEW: Fixes the NullReferenceException!
        // Ensures the global Firebase Authentication instance is strictly initialized before the Controller tries to use it.
        if (FirebaseApp.DefaultInstance == null)
        {
            FirebaseApp.Create(new AppOptions
            {
                Credential = credential,
                ProjectId = "cvnet2026-capstone"
            });
        }
        
        _db = new FirestoreDbBuilder
        {
            ProjectId = "cvnet2026-capstone", // Your target project id
            Credential = credential
        }.Build();
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
    
    // ✅ Deletes the user document from NoSQL safely
    public async Task DeleteUserDocument(string uid)
    {
        var docRef = _db.Collection(CollectionName).Document(uid);
        await docRef.DeleteAsync();
    }
}