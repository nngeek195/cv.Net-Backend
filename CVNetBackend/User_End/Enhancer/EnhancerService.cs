using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.RegularExpressions;

namespace CVNetBackend.User_End.Enhancer;

public class EnhancerService
{
    private readonly ChatClient _client;
    private const int MaxInputLength = 2000;

    public EnhancerService()
    {
        string apiKey = Environment.GetEnvironmentVariable("API_KEY") ?? "";
        var options = new OpenAIClientOptions { Endpoint = new Uri("https://integrate.api.nvidia.com/v1") };
        var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        _client = openAIClient.GetChatClient("mistralai/mistral-nemotron");
    }

    public async Task<string> EnhanceTextAsync(string inputText, string mode, string? customPrompt = null)
    {
        // 1. Validation: Prevent abuse by limiting input size
        if (string.IsNullOrWhiteSpace(inputText) || inputText.Length > MaxInputLength)
            return "Error: Input text is too long or empty.";

        // 2. Sanitization: Block common bypass/malicious keywords
        string pattern = @"(ignore|previous|instruction|system|developer|admin|override|bypass)";
        if (customPrompt != null && Regex.IsMatch(customPrompt.ToLower(), pattern))
            return "Error: Malicious instructions detected and blocked.";

        // 3. Anchoring: Provide a strict system instruction to prevent bypass
        string guardian = "CRITICAL: You are strictly a career assistant. If asked to do anything unrelated to career development, resumes, or professional writing, you MUST refuse and say 'I can only assist with professional career tasks.'";

        string systemMsg = mode.ToLower() switch
        {
            "summarize" => $"{guardian} Summarize the user's input into clear, concise bullet points.",
            "formalize" => $"{guardian} Rewrite the input in a highly professional, formal tone.",
            "custom" => $"{guardian} Specific career task: {customPrompt}",
            _ => $"{guardian} Assist with professional writing."
        };

        try
        {
            List<ChatMessage> messages = new()
            {
                new SystemChatMessage(systemMsg),
                new UserChatMessage(inputText)
            };

            ChatCompletionOptions options = new()
            {
                Temperature = 0.3f, // Lower temperature is more secure and predictable
                MaxOutputTokenCount = 1000
            };

            ChatCompletion completion = await _client.CompleteChatAsync(messages, options);
            return completion.Content[0].Text;
        }
        catch (Exception) // Removed 'ex' to fix CS0168 warning
        {
            return "Security Error: Processing failed.";
        }
    }
}