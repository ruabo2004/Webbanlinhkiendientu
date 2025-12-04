using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WebBanLinhKienDienTu.Services
{
    /// <summary>
    /// Service để gọi Google Gemini API
    /// </summary>
    public class GeminiService
    {
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _endpoint;
        private readonly HttpClient _httpClient;

        public GeminiService()
        {
            _apiKey = ConfigurationManager.AppSettings["GeminiApiKey"];
            _model = ConfigurationManager.AppSettings["GeminiModel"] ?? "gemini-1.5-flash";
            _endpoint = ConfigurationManager.AppSettings["GeminiEndpoint"] ?? "https://generativelanguage.googleapis.com/v1beta/models/";
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
            
            // Validation và logging
            System.Diagnostics.Debug.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            System.Diagnostics.Debug.WriteLine("🔧 [GeminiService] Constructor");
            System.Diagnostics.Debug.WriteLine($"   API Key: {(string.IsNullOrEmpty(_apiKey) ? "❌ NULL/EMPTY" : _apiKey.Substring(0, Math.Min(10, _apiKey.Length)) + "...")}");
            System.Diagnostics.Debug.WriteLine($"   Model: {_model ?? "NULL"}");
            System.Diagnostics.Debug.WriteLine($"   Endpoint: {_endpoint ?? "NULL"}");
            
            if (string.IsNullOrEmpty(_apiKey))
            {
                System.Diagnostics.Debug.WriteLine("❌❌❌ [GeminiService] API KEY KHÔNG ĐƯỢC CẤU HÌNH!");
            }
            
            if (string.IsNullOrEmpty(_endpoint))
            {
                System.Diagnostics.Debug.WriteLine("❌❌❌ [GeminiService] ENDPOINT KHÔNG ĐƯỢC CẤU HÌNH!");
            }
            
            System.Diagnostics.Debug.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }

        /// <summary>
        /// Gọi Gemini API để chat
        /// </summary>
        public async Task<string> ChatAsync(string prompt, string context = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                System.Diagnostics.Debug.WriteLine("🌟 [GeminiService] ChatAsync được gọi");
                System.Diagnostics.Debug.WriteLine($"📝 [GeminiService] API Key: {_apiKey?.Substring(0, 10)}...{_apiKey?.Substring(_apiKey.Length - 4)}");
                System.Diagnostics.Debug.WriteLine($"🤖 [GeminiService] Model: {_model}");
                System.Diagnostics.Debug.WriteLine($"🌐 [GeminiService] Endpoint: {_endpoint}");
                
                // Xây dựng full prompt với context (RAG)
                var fullPrompt = context != null 
                    ? $"{context}\n\n{prompt}" 
                    : prompt;

                System.Diagnostics.Debug.WriteLine($"📏 [GeminiService] Prompt length: {fullPrompt.Length} chars");

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = fullPrompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        topK = 40,
                        topP = 0.95,
                        maxOutputTokens = 2048, // ✅ Tăng từ 1024 → 2048 để tránh MAX_TOKENS
                    },
                    safetySettings = new[]
                    {
                        new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                        new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                        new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                        new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" }
                    }
                };

                var json = JsonConvert.SerializeObject(requestBody);

                // Validate trước khi gọi API
                if (string.IsNullOrEmpty(_apiKey))
                {
                    throw new Exception("API Key không được cấu hình trong Web.config!");
                }
                
                if (string.IsNullOrEmpty(_model))
                {
                    throw new Exception("Model không được cấu hình trong Web.config!");
                }
                
                if (string.IsNullOrEmpty(_endpoint))
                {
                    throw new Exception("Endpoint không được cấu hình trong Web.config!");
                }
                
                var url = $"{_endpoint}{_model}:generateContent?key={_apiKey}";
                System.Diagnostics.Debug.WriteLine($"🔗 [GeminiService] Full URL: {url.Replace(_apiKey, "***API_KEY***")}");
                System.Diagnostics.Debug.WriteLine($"📋 [GeminiService] Request JSON length: {json.Length} chars");
                System.Diagnostics.Debug.WriteLine("📤 [GeminiService] Đang gửi request...");
                
                // Retry logic cho 503 ServiceUnavailable
                int maxRetries = 3;
                int retryDelay = 2000; // 2 giây
                HttpResponseMessage response = null;
                
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    // ✅ Fix: Tạo lại StringContent cho mỗi retry (tránh ObjectDisposedException)
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    
                    var startTime = DateTime.Now;
                    response = await _httpClient.PostAsync(url, content);
                    var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                    
                    // Dispose content sau khi dùng
                    content.Dispose();
                    
                    System.Diagnostics.Debug.WriteLine($"📥 [GeminiService] Response Status: {response.StatusCode} (took {elapsed:F0}ms) [Attempt {attempt}/{maxRetries}]");

                    // Nếu thành công → break ngay
                    if (response.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ [GeminiService] Thành công ở attempt {attempt}");
                        break;
                    }
                    
                    // Nếu là 503 ServiceUnavailable → retry
                    if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                    {
                        if (attempt < maxRetries)
                        {
                            System.Diagnostics.Debug.WriteLine($"⏳ [GeminiService] Model overloaded (503), đợi {retryDelay}ms rồi retry (attempt {attempt + 1}/{maxRetries})...");
                            await Task.Delay(retryDelay);
                            retryDelay *= 2; // Exponential backoff: 2s → 4s → 8s
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ [GeminiService] Đã retry {maxRetries} lần nhưng vẫn bị 503. Dừng retry.");
                        }
                    }
                    else
                    {
                        // Lỗi khác (400, 401, 404...) → không retry, break ngay
                        System.Diagnostics.Debug.WriteLine($"❌ [GeminiService] Lỗi {response.StatusCode}, không retry.");
                        break;
                    }
                }

                if (response == null)
                {
                    throw new Exception("Không nhận được response từ Gemini API!");
                }
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"❌ [GeminiService] API Error {response.StatusCode}:");
                    System.Diagnostics.Debug.WriteLine($"   Error Content: {errorContent}");
                    
                    // Parse error để hiển thị rõ hơn
                    try
                    {
                        var errorObj = JsonConvert.DeserializeObject<dynamic>(errorContent);
                        if (errorObj?.error != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"   Error Message: {errorObj.error.message}");
                            System.Diagnostics.Debug.WriteLine($"   Error Code: {errorObj.error.code}");
                            System.Diagnostics.Debug.WriteLine($"   Error Status: {errorObj.error.status}");
                        }
                    }
                    catch (Exception parseEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"   Không parse được error JSON: {parseEx.Message}");
                    }
                    
                    System.Diagnostics.Debug.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    
                    // Tạo error message thân thiện hơn (dùng if-else thay vì switch expression cho C# 7.3)
                    string friendlyMessage;
                    if (response.StatusCode == HttpStatusCode.BadRequest)
                    {
                        friendlyMessage = "API Key hoặc Request không hợp lệ. Kiểm tra lại cấu hình!";
                    }
                    else if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        friendlyMessage = "API Key không hợp lệ hoặc đã hết hạn. Tạo API key mới tại: https://aistudio.google.com/app/apikey";
                    }
                    else if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        friendlyMessage = "API Key bị từ chối. Kiểm tra quyền truy cập!";
                    }
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        friendlyMessage = $"Model '{_model}' không tồn tại. Kiểm tra lại tên model!";
                    }
                    else if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                    {
                        friendlyMessage = "Model đang quá tải. Vui lòng thử lại sau vài giây!";
                    }
                    else if ((int)response.StatusCode == 429) // TooManyRequests
                    {
                        friendlyMessage = "Vượt quá giới hạn request. Đợi một lúc rồi thử lại!";
                    }
                    else
                    {
                        friendlyMessage = $"Lỗi từ Gemini API: {response.StatusCode}";
                    }
                    
                    throw new Exception($"{friendlyMessage} (Status: {response.StatusCode})");
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"✅ [GeminiService] Response nhận được, length: {responseJson.Length} chars");
                
                // Log response để debug
                if (responseJson.Length < 500)
                {
                    System.Diagnostics.Debug.WriteLine($"📄 [GeminiService] Response JSON: {responseJson}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"📄 [GeminiService] Response JSON preview: {responseJson.Substring(0, 500)}...");
                }
                
                var result = JsonConvert.DeserializeObject<GeminiResponse>(responseJson);

                if (result?.Candidates != null && result.Candidates.Count > 0)
                {
                    var candidate = result.Candidates[0];
                    System.Diagnostics.Debug.WriteLine($"📋 [GeminiService] Candidate found: FinishReason={candidate.FinishReason}");
                    
                    if (candidate?.Content?.Parts != null && candidate.Content.Parts.Count > 0)
                    {
                        var aiText = candidate.Content.Parts[0].Text;
                        System.Diagnostics.Debug.WriteLine($"💬 [GeminiService] AI trả lời: {aiText.Substring(0, Math.Min(100, aiText.Length))}...");
                        System.Diagnostics.Debug.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        return aiText;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ [GeminiService] Candidate không có Parts. FinishReason: {candidate.FinishReason}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ [GeminiService] Response không có Candidates. PromptFeedback: {result?.PromptFeedback != null}");
                }

                System.Diagnostics.Debug.WriteLine("⚠️ [GeminiService] Response không có content");
                System.Diagnostics.Debug.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                return "Xin lỗi, tôi không thể tạo câu trả lời lúc này.";
            }
            catch (Exception ex)
            {
                // Log error (có thể dùng log4net hoặc NLog)
                System.Diagnostics.Debug.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                System.Diagnostics.Debug.WriteLine($"❌❌❌ [GeminiService] EXCEPTION: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                throw new Exception("Lỗi kết nối với AI service. Vui lòng thử lại!", ex);
            }
        }

        /// <summary>
        /// Tạo embedding vector cho text (dùng cho semantic search)
        /// Note: Gemini chưa có embedding API, có thể dùng text-embedding-004 của Google
        /// </summary>
        public async Task<List<double>> GetEmbeddingAsync(string text)
        {
            // TODO: Implement khi cần semantic search thực sự
            // Hiện tại dùng full-text search là đủ
            throw new NotImplementedException("Embedding feature will be added in future");
        }
    }

    #region Response Models

    public class GeminiResponse
    {
        [JsonProperty("candidates")]
        public List<Candidate> Candidates { get; set; }

        [JsonProperty("promptFeedback")]
        public PromptFeedback PromptFeedback { get; set; }
    }

    public class Candidate
    {
        [JsonProperty("content")]
        public Content Content { get; set; }

        [JsonProperty("finishReason")]
        public string FinishReason { get; set; }

        [JsonProperty("safetyRatings")]
        public List<SafetyRating> SafetyRatings { get; set; }
    }

    public class Content
    {
        [JsonProperty("parts")]
        public List<Part> Parts { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }
    }

    public class Part
    {
        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public class SafetyRating
    {
        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("probability")]
        public string Probability { get; set; }
    }

    public class PromptFeedback
    {
        [JsonProperty("safetyRatings")]
        public List<SafetyRating> SafetyRatings { get; set; }
    }

    #endregion
}

