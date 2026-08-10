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
    
    // Firestore collection name used by the frontend.
    private const string CollectionName = "users";

    public FirestoreService()
    {
        // Load the service account from the backend workspace.
        string keyPath = Path.Combine(Directory.GetCurrentDirectory(), "firebase-key.json");

        if (!File.Exists(keyPath))
        {
            throw new FileNotFoundException($"[FIRESTORE ERROR] Security initialization failed. keyPath not found at: {keyPath}");
        }

        var credential = GoogleCredential.FromFile(keyPath);
        
        // Initialize Firebase before any controller accesses it.
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
            ProjectId = "cvnet2026-capstone",
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

        await docRef.SetAsync(userData, SetOptions.MergeAll);
    }
    
    // Delete the user document from Firestore.
    public async Task DeleteUserDocument(string uid)
    {
        var docRef = _db.Collection(CollectionName).Document(uid);
        await docRef.DeleteAsync();
    }
}