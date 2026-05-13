using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using CVNetBackend.Services;

namespace CVNetBackend.ProfileHandler;

public class ProfileService
{
    private readonly Cloudinary _cloudinary;
    private readonly FirestoreService _firestore;
    private readonly DatabaseService _postgres;

    public ProfileService(FirestoreService firestore, DatabaseService postgres)
    {
        _firestore = firestore;
        _postgres = postgres;

        // Fetch credentials from environment
        string cloudName = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME") ?? "";
        string apiKey = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY") ?? "";
        string apiSecret = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET") ?? "";

        // DEBUG: Ensure no keys are missing
        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            throw new Exception($"Cloudinary Config Error: " +
                $"CloudName: {!string.IsNullOrEmpty(cloudName)}, " +
                $"Key: {!string.IsNullOrEmpty(apiKey)}, " +
                $"Secret: {!string.IsNullOrEmpty(apiSecret)}");
        }

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true;
    }

    public async Task<string> UploadProfileImageAsync(string userId, IFormFile file)
    {
        // 1. Validation: Ensure a valid file is provided and limit size to 5MB
        if (file == null || file.Length == 0) throw new Exception("No file provided.");
        if (file.Length > 5 * 1024 * 1024) throw new Exception("File size exceeds 5MB limit.");

        using var stream = file.OpenReadStream();
        
        // 2. Cloudinary Configuration: Overwrite the existing image using the userId
        var uploadParams = new ImageUploadParams()
        {
            File = new FileDescription(file.FileName, stream),
            Folder = "profile_pictures",
            PublicId = userId, // Using userId as PublicId ensures the old image is replaced
            Overwrite = true,
            // Transformation: AI-driven face detection and cropping for a professional look
            Transformation = new Transformation().Width(500).Height(500).Crop("fill").Gravity("face")
        };

        var result = await _cloudinary.UploadAsync(uploadParams);
        
        // 3. Error Handling: Capture and rethrow Cloudinary-specific errors
        if (result.Error != null) throw new Exception($"Cloudinary Upload Failed: {result.Error.Message}");

        string imageUrl = result.SecureUrl.ToString();

        // 4. Firestore Sync: Use the exact camelCase name from your schema.gql
        // This will now match: profileImageUrl: String
        await _firestore.UpdateUserField(userId, "profileImageUrl", imageUrl);

        // 5. PostgreSQL Sync: Calls the update logic for the SQL column
        // This targets the column: profile_image_url
        await _postgres.UpdateProfileImage(userId, imageUrl);

        return imageUrl;
    }
}