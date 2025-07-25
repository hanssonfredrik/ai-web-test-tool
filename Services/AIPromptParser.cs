using System.Text;
using System.Text.Json;
using WebTestAutomation.Models;

namespace WebTestAutomation.Services
{
    public class AIPromptParser
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly Dictionary<string, TestScenario> _cache = new();
        private readonly SemaphoreSlim _rateLimiter = new(1, 1); // Allow 1 request at a time
        private DateTime _lastApiCall = DateTime.MinValue;
        private readonly TimeSpan _minDelayBetweenCalls = TimeSpan.FromSeconds(2); // 2 second delay between API calls

        public AIPromptParser(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
                ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is required");
        }

        public async Task<TestScenario> ParsePromptAsync(string prompt, string baseUrl = "")
        {
            // Check cache first
            var cacheKey = $"{prompt}|{baseUrl}";
            if (_cache.TryGetValue(cacheKey, out var cachedResult))
            {
                Console.WriteLine("Using cached AI parsing result");
                return cachedResult;
            }

            var aiScenario = await ParseWithAI(prompt, baseUrl);
            if (aiScenario != null && aiScenario.Actions.Any())
            {
                Console.WriteLine("Successfully parsed with AI");
                
                // Cache the result
                _cache[cacheKey] = aiScenario;
                
                // Limit cache size to prevent memory issues
                if (_cache.Count > 50)
                {
                    var oldestKey = _cache.Keys.First();
                    _cache.Remove(oldestKey);
                }
                
                return aiScenario;
            }

            throw new InvalidOperationException("Failed to parse prompt with AI. Please check your prompt and try again.");
        }

        private async Task<TestScenario?> ParseWithAI(string prompt, string baseUrl)
        {
            // Rate limiting: wait for semaphore and ensure minimum delay between calls
            await _rateLimiter.WaitAsync();
            try
            {
                var timeSinceLastCall = DateTime.Now - _lastApiCall;
                if (timeSinceLastCall < _minDelayBetweenCalls)
                {
                    var delay = _minDelayBetweenCalls - timeSinceLastCall;
                    Console.WriteLine($"Rate limiting: waiting {delay.TotalSeconds:F1}s before API call...");
                    await Task.Delay(delay);
                }
                
                _lastApiCall = DateTime.Now;
            var systemPrompt = @"You are an expert test automation parser. Convert natural language test instructions into structured JSON format.

Available action types:
- Navigate: ONLY for full URLs (http://example.com or www.example.com)
- Type: Enter text into form fields  
- Click: Click buttons, links, or navigate to sections/pages on the current site
- WaitForElement: Wait for an element to appear
- VerifyText: Check if text exists on page
- VerifyUrl: Check if URL contains specific text

IMPORTANT RULES:
- Use Navigate ONLY for complete URLs with http/https or domain names with dots
- Use Click for navigating to sections like 'go to Products', 'go to Dashboard', etc.
- ""Go to Product"" = Click action on Product link/button, NOT Navigate
- ""Go to http://example.com"" = Navigate action to URL

For each action, identify:
- Type: The action type from above
- Target: What element to interact with (button text, field name, etc.)
- Value: What text to type or verify (empty for clicks and navigation)

IMPORTANT for VerifyText:
- Target: can be empty or describe where to look
- Value: MUST contain the exact text to verify on the page

Example input: ""Go to https://example.com, click Accept all cookies, login using admin@test.com with password secret123, go to Products, create product with name 'Test Product', verify that Test Product appears in the list""
Example output:
{
  ""actions"": [
    {""type"": ""Navigate"", ""target"": ""https://example.com"", ""value"": """"},
    {""type"": ""Click"", ""target"": ""Accept all"", ""value"": """"},
    {""type"": ""Type"", ""target"": ""email"", ""value"": ""admin@test.com""},
    {""type"": ""Type"", ""target"": ""password"", ""value"": ""secret123""},
    {""type"": ""Click"", ""target"": ""login"", ""value"": """"},
    {""type"": ""Click"", ""target"": ""Products"", ""value"": """"},
    {""type"": ""Type"", ""target"": ""product name"", ""value"": ""Test Product""},
    {""type"": ""Click"", ""target"": ""save"", ""value"": """"},
    {""type"": ""VerifyText"", ""target"": ""product list"", ""value"": ""Test Product""}
  ]
}

Parse this prompt and return ONLY the JSON response:";

            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = prompt }
                },
                temperature = 0.1,
                max_tokens = 1000
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    throw new HttpRequestException($"TooManyRequests - Rate limit exceeded. Error: {errorContent}");
                }
                throw new Exception($"OpenAI API call failed: {response.StatusCode} - {errorContent}");
            }

            var responseText = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseText);
            
            var aiResponse = apiResponse?.choices?.FirstOrDefault()?.message?.content?.Trim();
            if (string.IsNullOrEmpty(aiResponse))
            {
                throw new Exception("Empty response from AI");
            }

            // Parse the AI response JSON
            var aiResult = JsonSerializer.Deserialize<AIParseResult>(aiResponse);
            if (aiResult?.actions == null)
            {
                throw new Exception("Invalid JSON structure from AI");
            }

            // Convert to TestScenario
            var scenario = new TestScenario
            {
                Name = "AI Generated Test Scenario",
                Description = prompt,
                BaseUrl = baseUrl,
                Actions = new List<TestAction>()
            };

            foreach (var action in aiResult.actions)
            {
                if (Enum.TryParse<ActionType>(action.type, true, out var actionType))
                {
                    scenario.Actions.Add(new TestAction
                    {
                        Type = actionType,
                        Target = action.target ?? "",
                        Value = action.value ?? ""
                    });
                }
            }

            return scenario;
            }
            finally
            {
                _rateLimiter.Release();
            }
        }

        private class OpenAIResponse
        {
            public Choice[]? choices { get; set; }
        }

        private class Choice
        {
            public Message? message { get; set; }
        }

        private class Message
        {
            public string? content { get; set; }
        }

        private class AIParseResult
        {
            public AIAction[]? actions { get; set; }
        }

        private class AIAction
        {
            public string? type { get; set; }
            public string? target { get; set; }
            public string? value { get; set; }
        }
    }
}