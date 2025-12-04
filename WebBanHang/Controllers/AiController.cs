using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using WebBanLinhKienDienTu.Core;
using WebBanLinhKienDienTu.Services;
using WebBanLinhKienDienTu.Models;

namespace WebBanLinhKienDienTu.Controllers
{
    /// <summary>
    /// API Controller cho AI Chatbot
    /// </summary>
    [RoutePrefix("api/ai")]
    public class AiController : ApiController
    {
        private readonly GeminiService _geminiService;
        private readonly ProductIndexService _indexService;
        
        // ✅ Fix: Cache categories trong request scope
        private List<CategoryInfo> _cachedCategories = null;

        public AiController()
        {
            _geminiService = new GeminiService();
            _indexService = new ProductIndexService();
        }
        
        /// <summary>
        /// Lấy categories với caching
        /// </summary>
        private List<CategoryInfo> GetCachedCategories()
        {
            if (_cachedCategories == null)
            {
                _cachedCategories = _indexService.GetAvailableCategories();
            }
            return _cachedCategories;
        }

        /// <summary>
        /// Chat endpoint chính
        /// POST /api/ai/chat
        /// </summary>
        [HttpPost]
        [Route("chat")]
        public async Task<IHttpActionResult> Chat([FromBody] ChatRequest request)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("==============================================");
                System.Diagnostics.Debug.WriteLine("🤖 [AiController] Nhận request mới");
                
                if (request == null || string.IsNullOrWhiteSpace(request.Question))
                {
                    System.Diagnostics.Debug.WriteLine("❌ [AiController] Request null hoặc Question rỗng");
                    return BadRequest("Vui lòng nhập câu hỏi!");
                }

                // Làm sạch input
                var question = request.Question.Trim();
                System.Diagnostics.Debug.WriteLine($"📝 [AiController] Question: {question}");

                // 1. Kiểm tra form câu hỏi trước khi tìm kiếm
                var questionForm = DetectQuestionForm(question);
                System.Diagnostics.Debug.WriteLine($"📋 [AiController] Form câu hỏi: {questionForm}");
                
                // ✅ LUỒNG 1: Load TẤT CẢ dữ liệu sản phẩm và gửi lên Gemini để AI tự phân tích
                System.Diagnostics.Debug.WriteLine("📦 [AiController] LUỒNG 1: Load tất cả dữ liệu sản phẩm...");
                var allProductsDataResult = _indexService.GetAllProductsForGemini(limit: 500); // Giới hạn 500 sản phẩm để tránh quá tải
                var allProductsData = allProductsDataResult.Data;
                var allProductIds = allProductsDataResult.ProductIds; // Lấy danh sách ProductID để validation
                System.Diagnostics.Debug.WriteLine($"📊 [AiController] Đã load dữ liệu {allProductsData.Length} chars, {allProductIds.Count} ProductID");
                
                // ✅ Gửi tất cả dữ liệu + câu hỏi lên Gemini để AI tự tìm sản phẩm phù hợp
                System.Diagnostics.Debug.WriteLine("🤖 [AiController] Gửi dữ liệu lên Gemini để AI phân tích...");
                var geminiSelectedProducts = await AskGeminiToFindProducts(question, allProductsData, allProductIds);
                System.Diagnostics.Debug.WriteLine($"✅ [AiController] Gemini đã chọn {geminiSelectedProducts?.Count ?? 0} sản phẩm");
                
                // ✅ Query lại CSDL để lấy thông tin chi tiết của sản phẩm Gemini đã chọn
                List<ProductSearchResult> searchResults = null;
                
                if (geminiSelectedProducts != null && geminiSelectedProducts.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"🔍 [AiController] Query lại CSDL theo {geminiSelectedProducts.Count} ProductID từ Gemini: [{string.Join(", ", geminiSelectedProducts)}]");
                    searchResults = _indexService.GetProductsByIds(geminiSelectedProducts);
                    
                    // ✅ VALIDATE: Kiểm tra xem sản phẩm trả về có khớp với câu hỏi không
                    if (searchResults != null && searchResults.Count > 0)
                    {
                        var questionLower = question.ToLower();
                        var validatedResults = new List<ProductSearchResult>();
                        
                        System.Diagnostics.Debug.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        System.Diagnostics.Debug.WriteLine($"🔍 [AiController] VALIDATION: Kiểm tra sản phẩm có khớp với câu hỏi '{question}' không...");
                        
                        foreach (var result in searchResults)
                        {
                            if (!geminiSelectedProducts.Contains(result.ProductID))
                            {
                                System.Diagnostics.Debug.WriteLine($"❌ [AiController] VALIDATION ERROR: ProductID {result.ProductID} không có trong danh sách Gemini trả về!");
                                continue;
                            }
                            
                            // ✅ VALIDATE: Kiểm tra tên sản phẩm có khớp với câu hỏi không
                            var productNameLower = result.Name.ToLower();
                            var categoryLower = result.CategoryName?.ToLower() ?? "";
                            
                            // Extract keywords từ câu hỏi
                            var questionKeywords = ExtractKeywords(question);
                            
                            // Kiểm tra matching
                            bool nameMatches = productNameLower.Contains(questionLower) || questionLower.Contains(productNameLower);
                            bool keywordMatches = questionKeywords.Any(keyword => 
                                productNameLower.Contains(keyword.ToLower()) || 
                                (result.Description != null && result.Description.ToLower().Contains(keyword.ToLower())));
                            bool categoryMatches = !string.IsNullOrEmpty(categoryLower) && questionLower.Contains(categoryLower);
                            
                            // ✅ Nếu tên sản phẩm chứa tất cả keywords chính → Khớp cao
                            bool highMatch = questionKeywords.Count > 0 && questionKeywords.All(k => productNameLower.Contains(k.ToLower()));
                            
                            if (nameMatches || highMatch || keywordMatches)
                            {
                                validatedResults.Add(result);
                                System.Diagnostics.Debug.WriteLine($"✅ [AiController] VALIDATION OK: ProductID {result.ProductID} - '{result.Name}' KHỚP với câu hỏi (Name: {nameMatches}, Keyword: {keywordMatches}, High: {highMatch})");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"⚠️ [AiController] VALIDATION WARNING: ProductID {result.ProductID} - '{result.Name}' CÓ VẺ KHÔNG KHỚP với câu hỏi '{question}'");
                                System.Diagnostics.Debug.WriteLine($"   → Name match: {nameMatches}, Keyword match: {keywordMatches}, High match: {highMatch}");
                                // ✅ Vẫn thêm vào nếu không có sản phẩm nào khác khớp (fallback)
                                if (validatedResults.Count == 0)
                                {
                                    validatedResults.Add(result);
                                    System.Diagnostics.Debug.WriteLine($"   → Nhưng vẫn chấp nhận vì không có sản phẩm nào khác khớp");
                                }
                            }
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        System.Diagnostics.Debug.WriteLine($"📊 [AiController] VALIDATION: {validatedResults.Count}/{searchResults.Count} sản phẩm KHỚP với câu hỏi");
                        
                        // ✅ Chỉ giữ lại sản phẩm đã validate
                        if (validatedResults.Count > 0)
                        {
                            searchResults = validatedResults;
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ [AiController] Gemini không trả về ProductID, dùng logic fallback...");
                    searchResults = await AnalyzeAndSearch(question); // Fallback: dùng logic cũ nếu Gemini không trả về
                }

                System.Diagnostics.Debug.WriteLine($"📊 [AiController] Kết quả cuối cùng: {searchResults?.Count ?? 0} sản phẩm");

                string context;
                string systemPrompt;

                // 2. CHECK: Nếu KHÔNG tìm thấy sản phẩm nào
                if (searchResults == null || searchResults.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ [AiController] Không tìm thấy sản phẩm phù hợp, lấy sản phẩm gợi ý...");
                    
                    // ✅ LUÔN lấy 1 sản phẩm gợi ý để hiển thị (ngay cả khi không tìm thấy chính xác)
                    // Thử lấy sản phẩm tốt nhất (có khuyến mãi, còn hàng)
                    var fallbackProducts = _indexService.GetTopProductsByCategory(null, 5); // Lấy top 5 sản phẩm
                    if (fallbackProducts != null && fallbackProducts.Count > 0)
                    {
                        searchResults = fallbackProducts; // Dùng sản phẩm gợi ý
                        System.Diagnostics.Debug.WriteLine($"✅ [AiController] Đã lấy {fallbackProducts.Count} sản phẩm gợi ý thay thế");
                    }
                }
                
                // 2b. Nếu vẫn không có sản phẩm nào, kiểm tra categories
                if (searchResults == null || searchResults.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ [AiController] Không tìm thấy sản phẩm, kiểm tra categories...");
                    
                    // ✅ Fix: Dùng cached categories
                    var availableCategories = GetCachedCategories();
                    System.Diagnostics.Debug.WriteLine($"📂 [AiController] Số categories: {availableCategories?.Count ?? 0}");
                    
                    // ✅ Fix: Check nếu DB rỗng hoàn toàn
                    if (availableCategories == null || availableCategories.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("❌ [AiController] DB rỗng! Trả về thông báo mặc định");
                        return Ok(new ChatResponse
                        {
                            Answer = "Xin lỗi! Hiện tại shop đang cập nhật sản phẩm mới. Vui lòng quay lại sau hoặc liên hệ hotline để được hỗ trợ! 😊",
                            RelatedProducts = new List<ProductSuggestion>(),
                            Success = true
                        });
                    }
                    
                    // ✅ Fix Lỗi 2.7: Giới hạn top 10 categories để tránh prompt quá dài
                    var topCategories = availableCategories
                        .OrderByDescending(c => c.ProductCount)
                        .Take(10)
                        .ToList();
                    
                    // Build danh sách categories động
                    var categoryList = string.Join("\n", topCategories.Select(c => 
                        $"   - {GetCategoryIcon(c.CategoryName)} **{c.CategoryName}**\n" +
                        $"     • Số lượng: {c.ProductCount} sản phẩm\n" +
                        $"     • Giá: {c.MinPrice:N0}đ - {c.MaxPrice:N0}đ\n" +
                        $"     • Giá trung bình: {c.AvgPrice:N0}đ"
                    ));

                    // Kiểm tra category cụ thể user đang hỏi
                    var requestedCategory = ExtractCategory(question);
                    string specificCategoryInfo = "";
                    
                    if (!string.IsNullOrEmpty(requestedCategory))
                    {
                        var categoryInfo = _indexService.GetCategoryPriceInfo(requestedCategory);
                        if (categoryInfo != null)
                        {
                            specificCategoryInfo = $@"

⚠️ PHÂN TÍCH YÊU CẦU:
Khách hỏi về: '{requestedCategory}'
→ Shop CÓ BÁN {requestedCategory}!

Thông tin {requestedCategory}:
- Giá thấp nhất: {categoryInfo.MinPrice:N0}đ
- Giá cao nhất: {categoryInfo.MaxPrice:N0}đ
- Giá trung bình: {categoryInfo.AvgPrice:N0}đ
- Tổng số sản phẩm: {categoryInfo.ProductCount} mẫu

KẾT LUẬN: Có thể do:
1. Giá yêu cầu NGOÀI khoảng {categoryInfo.MinPrice:N0}đ - {categoryInfo.MaxPrice:N0}đ
2. Từ khóa tìm kiếm không match với tên sản phẩm trong DB
3. Yêu cầu quá cụ thể (VD: brand không có, model không có)

→ Hỏi lại khách về ngân sách hoặc yêu cầu linh hoạt hơn.
→ Có thể gợi ý {requestedCategory} GIÁ PHẢI HỢP (từ {categoryInfo.MinPrice:N0}đ).
";
                        }
                        else
                        {
                            specificCategoryInfo = $@"

⚠️ PHÂN TÍCH YÊU CẦU:
Khách hỏi về: '{requestedCategory}'
→ Shop KHÔNG BÁN {requestedCategory}!

KẾT LUẬN:
- Shop không kinh doanh mặt hàng này
- Cần gợi ý các DANH MỤC KHÁC từ danh sách bên dưới
- QUAN TRỌNG: Chỉ gợi ý các danh mục CÓ THẬT trong danh sách!
";
                        }
                    }

                    // ✅ Kiểm tra form câu hỏi để điều hướng chính xác hơn
                    var detectedForm = DetectQuestionForm(question);
                    var formGuidance = "";
                    
                    if (detectedForm == "NEEDS_CATEGORY")
                    {
                        formGuidance = @"
⚠️ PHÁT HIỆN: Câu hỏi có giá nhưng THIẾU DANH MỤC!

Hãy điều hướng khách hỏi theo form:
[Danh mục] + [Giá tiền]

Ví dụ cụ thể dựa trên giá khách vừa hỏi:
- ""Linh kiện máy tính giá [giá khách vừa hỏi]""
- ""Phụ kiện máy tính giá [giá khách vừa hỏi]""
- ""Combo lắp ráp giá [giá khách vừa hỏi]""
";
                    }
                    else if (detectedForm == "NEEDS_PRICE")
                    {
                        formGuidance = $@"
⚠️ PHÁT HIỆN: Câu hỏi có danh mục nhưng THIẾU GIÁ!

Hãy điều hướng khách hỏi theo form:
[Danh mục] + [Giá tiền]

Ví dụ cụ thể:
- ""{requestedCategory} giá 5 triệu""
- ""{requestedCategory} giá trên 5 triệu""
- ""{requestedCategory} giá dưới 5 triệu""
- ""{requestedCategory} khoảng 4-5 triệu""
";
                    }
                    else if (detectedForm == "INVALID" || detectedForm == "UNKNOWN")
                    {
                        formGuidance = @"
⚠️ PHÁT HIỆN: Câu hỏi không đúng form!

Hãy điều hướng khách hỏi theo các form sau:
";
                    }

                    // ✅ Rút ngắn context khi không có sản phẩm
                    context = $@"KHÔNG TÌM THẤY SẢN PHẨM PHÙ HỢP
{specificCategoryInfo}

{formGuidance}

DANH MỤC ĐANG BÁN:
{categoryList}

⚠️ ĐIỀU HƯỚNG KHÁCH HỎI THEO FORM ĐÚNG:

Khi không tìm thấy sản phẩm, cần ĐIỀU HƯỚNG khách hỏi theo các form sau:

**FORM 1: Danh mục + Giá tiền**
Ví dụ: ""Linh kiện máy tính giá 5 triệu"", ""Phụ kiện máy tính giá 2 triệu""

**FORM 2: Tên sản phẩm**
Ví dụ: ""CPU Intel Core i5"", ""RAM DDR4 16GB"", ""SSD NVMe 500GB""

**FORM 3: Danh mục + Giá trên + Khoảng giá**
Ví dụ: ""Linh kiện máy tính giá trên 5 triệu"", ""Combo lắp ráp giá trên 10 triệu""

**FORM 4: Danh mục + Giá thấp hơn + Giá tiền**
Ví dụ: ""Linh kiện máy tính giá dưới 5 triệu"", ""Phụ kiện máy tính giá dưới 1 triệu""

**FORM 5: Danh mục + Khoảng - 2 giá tiền**
Ví dụ: ""Linh kiện máy tính khoảng 4-5 triệu"", ""Combo lắp ráp khoảng 10-15 triệu""

HƯỚNG DẪN:
1. Xin lỗi ngắn gọn vì không tìm thấy sản phẩm
2. GIẢI THÍCH RÕ RÀNG các form để hỏi (liệt kê 5 form trên)
3. ĐƯA VÍ DỤ cụ thể dựa trên danh mục khách đang quan tâm
4. Khuyến khích khách hỏi lại theo form đúng
5. CHỈ dùng danh mục có trong danh sách, KHÔNG bịa ra

VÍ DỤ TRẢ LỜI:
""Xin lỗi! Mình chưa tìm thấy sản phẩm phù hợp với yêu cầu của bạn. 

Để mình có thể tư vấn chính xác hơn, bạn có thể hỏi theo các cách sau:

1. [Danh mục] + [Giá tiền]
   Ví dụ: 'Linh kiện máy tính giá 5 triệu'

2. Tên sản phẩm cụ thể
   Ví dụ: 'CPU Intel Core i5'

3. [Danh mục] + Giá trên [giá]
   Ví dụ: 'Linh kiện máy tính giá trên 5 triệu'

4. [Danh mục] + Giá dưới [giá]
   Ví dụ: 'Linh kiện máy tính giá dưới 5 triệu'

5. [Danh mục] + Khoảng [giá] - [giá]
   Ví dụ: 'Linh kiện máy tính khoảng 4-5 triệu'

Bạn muốn tìm sản phẩm nào trong danh mục nào, và ngân sách của bạn là bao nhiêu? 😊""
";
                }
                else
                {
                    // Context bình thường khi có sản phẩm
                    context = _indexService.BuildContextFromProducts(searchResults);
                }

                // 3. Xây dựng system prompt
                systemPrompt = BuildSystemPrompt(context);
                System.Diagnostics.Debug.WriteLine($"📋 [AiController] System prompt length: {systemPrompt?.Length ?? 0} chars");

                // 4. Gọi Gemini API
                var fullPrompt = $"{systemPrompt}\n\nCÂU HỎI KHÁCH HÀNG: {question}";
                System.Diagnostics.Debug.WriteLine("🚀 [AiController] Gọi Gemini API...");
                
                // Delay nhỏ để tránh rate limit (nếu vừa gọi ExtractIntentWithGemini)
                await Task.Delay(500);
                
                var aiResponse = await _geminiService.ChatAsync(fullPrompt);
                System.Diagnostics.Debug.WriteLine($"✅ [AiController] Gemini API trả về: {aiResponse?.Substring(0, Math.Min(100, aiResponse?.Length ?? 0))}...");

                // 5. Trả về kết quả - LUÔN CHỈ TRẢ VỀ 1 SẢN PHẨM TỐT NHẤT
                // ✅ Nếu có sản phẩm từ Gemini, ưu tiên lấy sản phẩm đầu tiên (vì Gemini đã chọn)
                ProductSearchResult bestProduct = null;
                
                if (searchResults != null && searchResults.Count > 0)
                {
                    // ✅ QUAN TRỌNG: Nếu có sản phẩm từ Gemini, LUÔN ưu tiên sản phẩm ĐẦU TIÊN
                    // Vì ProductID đã được sắp xếp theo thứ tự từ Gemini (ưu tiên nhất)
                    // Và đã được validate để đảm bảo khớp với câu hỏi
                    if (geminiSelectedProducts != null && geminiSelectedProducts.Count > 0)
                    {
                        // ✅ Lấy sản phẩm đầu tiên từ danh sách (vì đã được sắp xếp theo thứ tự ưu tiên của Gemini)
                        bestProduct = searchResults.FirstOrDefault();
                        
                        if (bestProduct != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                            System.Diagnostics.Debug.WriteLine($"✅ [AiController] CHỌN SẢN PHẨM TỪ GEMINI (sản phẩm đầu tiên):");
                            System.Diagnostics.Debug.WriteLine($"   ProductID: {bestProduct.ProductID}");
                            System.Diagnostics.Debug.WriteLine($"   Name: {bestProduct.Name}");
                            System.Diagnostics.Debug.WriteLine($"   Price: {bestProduct.Price:N0}đ");
                            System.Diagnostics.Debug.WriteLine($"   Category: {bestProduct.CategoryName ?? "NULL"}");
                            System.Diagnostics.Debug.WriteLine($"   User hỏi: '{question}'");
                            
                            // ✅ VALIDATE: Kiểm tra xem sản phẩm có khớp với câu hỏi không
                            var questionLower = question.ToLower();
                            var productNameLower = bestProduct.Name.ToLower();
                            var categoryLower = bestProduct.CategoryName?.ToLower() ?? "";
                            
                            bool nameMatches = productNameLower.Contains(questionLower) || questionLower.Contains(productNameLower);
                            bool categoryMatches = !string.IsNullOrEmpty(categoryLower) && questionLower.Contains(categoryLower);
                            
                            if (!nameMatches && !categoryMatches)
                            {
                                System.Diagnostics.Debug.WriteLine($"   ⚠️ WARNING: Sản phẩm '{bestProduct.Name}' có vẻ KHÔNG KHỚP với câu hỏi '{question}'!");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"   ✅ Sản phẩm KHỚP với câu hỏi");
                            }
                            System.Diagnostics.Debug.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                        }
                    }
                    else
                    {
                        // Nếu không có sản phẩm từ Gemini, dùng logic cũ để chọn sản phẩm tốt nhất
                        bestProduct = GetBestProduct(searchResults);
                    }
                }
                
                // ✅ Nếu vẫn không có sản phẩm, lấy 1 sản phẩm bất kỳ (fallback)
                if (bestProduct == null)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ [AiController] Không có sản phẩm nào, lấy fallback...");
                    var fallbackProducts = _indexService.GetTopProductsByCategory(null, 1); // Lấy 1 sản phẩm tốt nhất
                    if (fallbackProducts != null && fallbackProducts.Count > 0)
                    {
                        bestProduct = fallbackProducts.First();
                        System.Diagnostics.Debug.WriteLine($"✅ [AiController] Đã lấy fallback product: {bestProduct.Name}");
                    }
                }
                
                List<ProductSuggestion> productsToReturn = new List<ProductSuggestion>();
                if (bestProduct != null)
                {
                    // ✅ VALIDATE: Log chi tiết sản phẩm được chọn
                    System.Diagnostics.Debug.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    System.Diagnostics.Debug.WriteLine($"🎯 [AiController] Sản phẩm được chọn:");
                    System.Diagnostics.Debug.WriteLine($"   ProductID: {bestProduct.ProductID}");
                    System.Diagnostics.Debug.WriteLine($"   Name: {bestProduct.Name}");
                    System.Diagnostics.Debug.WriteLine($"   Price: {bestProduct.Price:N0}đ");
                    System.Diagnostics.Debug.WriteLine($"   PromotionPrice: {(bestProduct.PromotionPrice.HasValue ? bestProduct.PromotionPrice.Value.ToString("N0") + "đ" : "NULL")}");
                    System.Diagnostics.Debug.WriteLine($"   ImagePath: {(string.IsNullOrEmpty(bestProduct.ImagePath) ? "NULL" : bestProduct.ImagePath)}");
                    System.Diagnostics.Debug.WriteLine($"   RelevanceScore: {bestProduct.RelevanceScore}");
                    System.Diagnostics.Debug.WriteLine($"   CategoryName: {bestProduct.CategoryName ?? "NULL"}");
                    System.Diagnostics.Debug.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    
                    productsToReturn = new List<ProductSuggestion>
                    {
                        new ProductSuggestion
                        {
                            Id = bestProduct.ProductID,
                            Name = bestProduct.Name,
                            Price = bestProduct.Price,
                            PromotionPrice = bestProduct.PromotionPrice,
                            ImageUrl = bestProduct.ImagePath != null && !string.IsNullOrEmpty(bestProduct.ImagePath)
                                ? Utils.ImageHelper.ImageUrl(bestProduct.ImagePath) 
                            : "/Content/images/no_image.jpg",
                            Url = $"/Product/Detail/{bestProduct.ProductID}"
                        }
                    };
                }
                
                System.Diagnostics.Debug.WriteLine("==============================================");
                System.Diagnostics.Debug.WriteLine($"✅ [AiController] Trả về {productsToReturn.Count} sản phẩm:");
                foreach (var p in productsToReturn)
                {
                    // ✅ VALIDATE: Kiểm tra lại hình ảnh có đúng không
                    System.Diagnostics.Debug.WriteLine($"   - ProductID: {p.Id}, Name: {p.Name}");
                    System.Diagnostics.Debug.WriteLine($"     Price: {p.Price:N0}đ, PromotionPrice: {(p.PromotionPrice.HasValue ? p.PromotionPrice.Value.ToString("N0") + "đ" : "NULL")}");
                    System.Diagnostics.Debug.WriteLine($"     ImageUrl: {p.ImageUrl}");
                    System.Diagnostics.Debug.WriteLine($"     Url: {p.Url}");
                    
                    // ✅ VALIDATE hình ảnh bằng cách query lại từ DB
                    if (bestProduct != null && bestProduct.ProductID == p.Id)
                    {
                        using (var db = new ecommerceEntities())
                        {
                            var validateImage = db.ImageProducts
                                .Where(img => img.ProductID == p.Id && 
                                             img.ImagePath != null && 
                                             !string.IsNullOrEmpty(img.ImagePath))
                                .OrderBy(img => img.ImageID)
                                .Select(img => img.ImagePath)
                                .FirstOrDefault();
                            
                            if (!string.IsNullOrEmpty(validateImage))
                            {
                                var validatedImageUrl = Utils.ImageHelper.ImageUrl(validateImage);
                                if (validatedImageUrl != p.ImageUrl)
                                {
                                    System.Diagnostics.Debug.WriteLine($"     ⚠️ ImageUrl MISMATCH! Expected: {validatedImageUrl}, Got: {p.ImageUrl}");
                                    // ✅ Sửa lại hình ảnh nếu khác nhau
                                    p.ImageUrl = validatedImageUrl;
                                }
                            }
                        }
                    }
                }
                System.Diagnostics.Debug.WriteLine("==============================================");
                
                return Ok(new ChatResponse
                {
                    Answer = aiResponse,
                    RelatedProducts = productsToReturn,
                    Success = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("==============================================");
                System.Diagnostics.Debug.WriteLine($"❌❌❌ [AiController] EXCEPTION: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                System.Diagnostics.Debug.WriteLine("==============================================");
                
                return Ok(new ChatResponse
                {
                    Answer = "Xin lỗi, tôi đang gặp chút vấn đề kỹ thuật. Bạn có thể thử lại hoặc liên hệ hotline để được hỗ trợ trực tiếp! 😊",
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        /// <summary>
        /// Quick suggestion endpoint
        /// GET /api/ai/suggestions
        /// </summary>
        [HttpGet]
        [Route("suggestions")]
        public IHttpActionResult GetSuggestions()
        {
            var allSuggestions = new List<string>
            {
                // Linh kiện máy tính
                "CPU Intel Core i5 giá tốt",
                "Mainboard ASUS B660M",
                "RAM DDR4 16GB gaming",
                "SSD NVMe tốc độ cao",
                "VGA RTX 3060 gaming",
                "PSU 650W 80+ Gold",
                
                // Phụ kiện máy tính
                "Quạt case RGB tản nhiệt",
                "Hub USB 3.0 nhiều cổng",
                "Keo tản nhiệt CPU",
                "Cáp SATA kết nối",
                "Adapter WiFi USB",
                "Card Reader đa định dạng",
                
                // Thiết bị ngoại vi
                "Bàn phím cơ gaming",
                "Chuột gaming RGB",
                "Tai nghe gaming 7.1",
                "Webcam Full HD 1080p",
                "Loa gaming 2.1",
                "Microphone USB studio",
                
                // Phần mềm & Bản quyền
                "Windows 11 Home bản quyền",
                "Microsoft Office 2021",
                "Antivirus Kaspersky",
                "Adobe Photoshop 2024",
                "AutoCAD 2024",
                "VPN ExpressVPN",
                
                // Combo lắp ráp
                "Combo PC văn phòng",
                "Combo PC gaming cơ bản",
                "Combo gaming cao cấp",
                "Combo streaming",
                "Combo workstation",
                "Combo PC budget"
            };

            // Random 3 suggestions
            var random = new Random();
            var randomSuggestions = allSuggestions
                .OrderBy(x => random.Next())
                .Take(3)
                .ToList();

            return Ok(new { suggestions = randomSuggestions });
        }

        /// <summary>
        /// Phân tích câu hỏi và tìm kiếm sản phẩm
        /// </summary>
        private async Task<List<ProductSearchResult>> AnalyzeAndSearch(string question)
        {
            var lowerQuestion = question.ToLower();

            // ✅ Extract số lượng sản phẩm user muốn xem (tối đa 3)
            int requestedCount = ExtractProductCount(question);
            int maxResults = requestedCount > 0 ? requestedCount : 5; // Nếu user không yêu cầu số lượng cụ thể, mặc định 5 để chọn 1 sản phẩm tốt nhất
            
            System.Diagnostics.Debug.WriteLine($"🔢 [AiController] User yêu cầu {requestedCount} sản phẩm, maxResults = {maxResults}");
            
            // ✅ LUỒNG 1: Tìm kiếm trực tiếp theo tên sản phẩm trước (giống thanh tìm kiếm)
            // Nếu câu hỏi đơn giản (không có từ khóa đặc biệt), tìm kiếm ngay
            bool hasSpecialKeywords = lowerQuestion.Contains("giá rẻ nhất") || lowerQuestion.Contains("giá đắt nhất") ||
                                     lowerQuestion.Contains("dưới") || lowerQuestion.Contains("trên") ||
                                     lowerQuestion.Contains("khoảng") || lowerQuestion.Contains("triệu") ||
                                     lowerQuestion.Contains("tr") || lowerQuestion.Contains("combo") ||
                                     System.Text.RegularExpressions.Regex.IsMatch(lowerQuestion, @"\d+\s*(?:triệu|tr|m)");
            
            if (!hasSpecialKeywords)
            {
                // ✅ Câu hỏi đơn giản → tìm kiếm trực tiếp (giống thanh tìm kiếm)
                System.Diagnostics.Debug.WriteLine($"🔍 [AiController] Câu hỏi đơn giản, tìm kiếm trực tiếp: '{question}'");
                var directResults = _indexService.SearchProducts(question, maxResults);
                if (directResults != null && directResults.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ [AiController] Tìm thấy {directResults.Count} sản phẩm trực tiếp");
                    return directResults;
                }
            }

            // ✅ NEW: Nhận diện "giá rẻ nhất" hoặc "giá đắt nhất"
            if (lowerQuestion.Contains("giá rẻ nhất") || lowerQuestion.Contains("rẻ nhất") || 
                lowerQuestion.Contains("cheapest") || lowerQuestion.Contains("giá thấp nhất"))
            {
                System.Diagnostics.Debug.WriteLine("🔍 [AiController] Tìm sản phẩm giá rẻ nhất");
                var cheapestProducts = _indexService.GetCheapestProducts(maxResults);
                if (cheapestProducts != null && cheapestProducts.Count > 0)
                    return cheapestProducts;
            }
            
            if (lowerQuestion.Contains("giá đắt nhất") || lowerQuestion.Contains("đắt nhất") || 
                lowerQuestion.Contains("most expensive") || lowerQuestion.Contains("giá cao nhất"))
            {
                System.Diagnostics.Debug.WriteLine("🔍 [AiController] Tìm sản phẩm giá đắt nhất");
                var expensiveProducts = _indexService.GetMostExpensiveProducts(maxResults);
                if (expensiveProducts != null && expensiveProducts.Count > 0)
                    return expensiveProducts;
            }

            // ✅ NEW: Nhận diện số người → tự động tìm combo
            int? peopleCount = ExtractPeopleCount(question);
            if (peopleCount.HasValue && peopleCount.Value >= 2)
            {
                System.Diagnostics.Debug.WriteLine($"Detected {peopleCount} people, searching for combo");
                
                // Tìm combo theo số người
                if (peopleCount.Value >= 4)
                {
                    // 4+ người → tìm combo gia đình
                    var results = _indexService.SearchProducts("combo gia đình", maxResults);
                    if (results != null && results.Count > 0)
                        return results;
                }
                else if (peopleCount.Value >= 2)
                {
                    // 2-3 người → tìm combo couple hoặc combo nhỏ
                    var results = _indexService.SearchProducts("combo", maxResults);
                    if (results != null && results.Count > 0)
                        return results;
                }
            }

            // ✅ Extract category TRƯỚC để có thể kết hợp với giá
            var category = ExtractCategory(question);
            System.Diagnostics.Debug.WriteLine($"📂 [AiController] Category extracted: {(category ?? "null")}");
            
            // ✅ XỬ LÝ HỎI VỀ GIÁ - Ưu tiên cao nhất
            // Pattern "triệu" hoặc "tr"
            var millionMatch = System.Text.RegularExpressions.Regex.Match(lowerQuestion, @"(\d+(?:[.,]\d+)?)\s*(?:triệu|tr|m)");
            
            // Pattern khoảng giá "X-Y triệu" hoặc "X đến Y triệu"
            var rangeMatch = System.Text.RegularExpressions.Regex.Match(lowerQuestion, @"(\d+(?:[.,]\d+)?)\s*(?:triệu|tr|m)?\s*(?:[-đến–]\s*|\s+)\s*(\d+(?:[.,]\d+)?)\s*(?:triệu|tr|m)");
            
            if (rangeMatch.Success && rangeMatch.Groups.Count >= 3)
            {
                // Khoảng giá: "4-5 triệu" hoặc "4 đến 5 triệu"
                var minValue = SafeParseDecimal(rangeMatch.Groups[1].Value);
                var maxValue = SafeParseDecimal(rangeMatch.Groups[2].Value);
                if (minValue.HasValue && maxValue.HasValue)
                {
                    var minPrice = minValue.Value * 1000000;
                    var maxPrice = maxValue.Value * 1000000;
                    System.Diagnostics.Debug.WriteLine($"💰 [AiController] Khoảng giá: {minPrice:N0} - {maxPrice:N0}đ, category: {(category ?? "null")}, lấy {maxResults} sản phẩm");
                    
                    // ✅ Kết hợp với category nếu có
                    var results = !string.IsNullOrEmpty(category)
                        ? _indexService.GetProductsInPriceRangeWithCategory(minPrice, maxPrice, category, maxResults)
                        : _indexService.GetProductsInPriceRange(minPrice, maxPrice, maxResults);
                    
                    if (results != null && results.Count > 0)
                        return results;
                }
            }
            else if (millionMatch.Success)
            {
                var value = SafeParseDecimal(millionMatch.Groups[1].Value);
                if (value.HasValue)
                {
                    var priceInVND = value.Value * 1000000;
                    
                    if (lowerQuestion.Contains("dưới") || lowerQuestion.Contains("duoi") || lowerQuestion.Contains("thấp hơn"))
                    {
                        // Giá dưới X: Lấy sản phẩm giá dưới X gần nhất
                        System.Diagnostics.Debug.WriteLine($"💰 [AiController] Giá dưới {priceInVND:N0}đ, category: {(category ?? "null")}, lấy {maxResults} sản phẩm gần nhất");
                        
                        // ✅ Kết hợp với category nếu có
                        var results = !string.IsNullOrEmpty(category)
                            ? _indexService.GetProductsBelowPriceWithCategory(priceInVND, category, maxResults)
                            : _indexService.GetProductsBelowPrice(priceInVND, maxResults);
                        
                        if (results != null && results.Count > 0)
                            return results;
                    }
                    else if (lowerQuestion.Contains("trên") || lowerQuestion.Contains("tren") || lowerQuestion.Contains("cao hơn"))
                    {
                        // Giá trên X: Lấy sản phẩm giá trên X gần nhất
                        System.Diagnostics.Debug.WriteLine($"💰 [AiController] Giá trên {priceInVND:N0}đ, category: {(category ?? "null")}, lấy {maxResults} sản phẩm gần nhất");
                        
                        // ✅ Kết hợp với category nếu có
                        var results = !string.IsNullOrEmpty(category)
                            ? _indexService.GetProductsAbovePriceWithCategory(priceInVND, category, maxResults)
                            : _indexService.GetProductsAbovePrice(priceInVND, maxResults);
                        
                        if (results != null && results.Count > 0)
                            return results;
                    }
                    else if (lowerQuestion.Contains("khoảng") || lowerQuestion.Contains("tầm") || lowerQuestion.Contains("gần"))
                    {
                        // Khoảng X: Lấy sản phẩm trong khoảng ±20%
                        System.Diagnostics.Debug.WriteLine($"💰 [AiController] Khoảng {priceInVND:N0}đ (±20%), category: {(category ?? "null")}, lấy {maxResults} sản phẩm");
                        
                        // ✅ Kết hợp với category nếu có
                        var results = !string.IsNullOrEmpty(category)
                            ? _indexService.GetProductsInPriceRangeWithCategory(
                                priceInVND * 0.8m,
                                priceInVND * 1.2m,
                                category,
                                maxResults
                            )
                            : _indexService.GetProductsInPriceRange(
                                priceInVND * 0.8m,
                                priceInVND * 1.2m,
                                maxResults
                            );
                        
                        if (results != null && results.Count > 0)
                            return results;
                    }
                    else
                    {
                        // Không có từ khóa rõ ràng → coi như khoảng
                        System.Diagnostics.Debug.WriteLine($"💰 [AiController] Giá ~{priceInVND:N0}đ (khoảng), category: {(category ?? "null")}, lấy {maxResults} sản phẩm");
                        
                        // ✅ Kết hợp với category nếu có
                        var results = !string.IsNullOrEmpty(category)
                            ? _indexService.GetProductsInPriceRangeWithCategory(
                            priceInVND * 0.8m,
                            priceInVND * 1.2m,
                                category,
                                maxResults
                            )
                            : _indexService.GetProductsInPriceRange(
                                priceInVND * 0.8m,
                                priceInVND * 1.2m,
                            maxResults
                        );
                        
                        if (results != null && results.Count > 0)
                            return results;
                    }
                }
            }
            
            // Nếu có giá (pattern khác) → tìm theo giá
            var priceRange = ExtractPriceRange(lowerQuestion);
            if (priceRange.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"💰 [AiController] Price range: {priceRange.Value.Min:N0} - {priceRange.Value.Max:N0}đ, category: {(category ?? "null")}, lấy {maxResults} sản phẩm");
                
                // ✅ Kết hợp với category nếu có
                var priceResults = !string.IsNullOrEmpty(category)
                    ? _indexService.GetProductsInPriceRangeWithCategory(
                        priceRange.Value.Min, 
                        priceRange.Value.Max, 
                        category,
                        maxResults
                    )
                    : _indexService.GetProductsInPriceRange(
                        priceRange.Value.Min, 
                        priceRange.Value.Max, 
                        maxResults
                    );
                
                if (priceResults != null && priceResults.Count > 0)
                    return priceResults;
            }
            
            // ✅ Nếu chỉ có category, không có giá → tìm sản phẩm trong category
            if (!string.IsNullOrEmpty(category))
            {
                System.Diagnostics.Debug.WriteLine($"📂 [AiController] Chỉ có category: {category}, tìm sản phẩm trong category này");
                var categoryResults = _indexService.GetTopProductsByCategory(category, maxResults);
                if (categoryResults != null && categoryResults.Count > 0)
                    return categoryResults;
            }

            // ✅ LUỒNG 2: Fallback - Tìm kiếm trực tiếp theo tên sản phẩm (không dùng category)
            // Nếu không tìm thấy với logic đặc biệt, thử tìm kiếm đơn giản
            System.Diagnostics.Debug.WriteLine($"🔍 [AiController] Fallback: Tìm kiếm trực tiếp theo tên sản phẩm: '{question}'");
            var fallbackResults = _indexService.SearchProducts(question, maxResults);
            if (fallbackResults != null && fallbackResults.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"✅ [AiController] Fallback tìm thấy {fallbackResults.Count} sản phẩm");
                return fallbackResults;
            }
            
            // Không tìm thấy gì cả
            System.Diagnostics.Debug.WriteLine($"⚠️ [AiController] KHÔNG TÌM THẤY sản phẩm nào với câu hỏi: '{question}'");
            return new List<ProductSearchResult>();
        }
        
        /// <summary>
        /// Dùng Gemini để extract intent từ câu hỏi (giải quyết vấn đề từ đồng nghĩa)
        /// </summary>
        private async Task<UserIntent> ExtractIntentWithGemini(string question)
        {
            try
            {
                // Lấy danh sách categories hiện có
                var categories = GetCachedCategories();
                var categoryNames = string.Join(", ", categories.Select(c => $"\"{c.CategoryName}\""));
                
                var prompt = $@"Phân tích câu hỏi của khách hàng và trả về JSON với thông tin sau:
- category: Tên danh mục sản phẩm (PHẢI LÀ 1 TRONG CÁC GIÁ TRỊ: {categoryNames}). Nếu không match chính xác thì để null.
- priceMin: Giá tối thiểu (số nguyên, đơn vị VNĐ)
- priceMax: Giá tối đa (số nguyên, đơn vị VNĐ)

LƯU Ý CATEGORY:
- ""cpu"", ""processor"", ""chip"" → category = ""Linh Kiện Máy Tính""
- ""mainboard"", ""bo mạch chủ"", ""motherboard"" → category = ""Linh Kiện Máy Tính""
- ""ram"", ""bộ nhớ"", ""memory"" → category = ""Linh Kiện Máy Tính""
- ""ssd"", ""hdd"", ""ổ cứng"", ""vga"", ""card đồ họa"", ""psu"", ""nguồn"" → category = ""Linh Kiện Máy Tính""
- ""quạt"", ""cáp"", ""hub"", ""adapter"", ""phụ kiện"" → category = ""Phụ Kiện Máy Tính""
- ""bàn phím"", ""chuột"", ""keyboard"", ""mouse"" → category = ""Thiết Bị Ngoại Vi""
- ""tai nghe"", ""webcam"", ""loa"", ""microphone"" → category = ""Thiết Bị Ngoại Vi""
- ""windows"", ""office"", ""phần mềm"", ""bản quyền"", ""antivirus"" → category = ""Phần Mềm & Bản Quyền""
- ""combo"", ""lắp ráp"", ""build pc"" → category = ""Combo Lắp Ráp""
- Nếu khách hỏi ""giới thiệu/cho xem/tìm [category]"" mà KHÔNG nhắc giá → chỉ set category, để priceMin=null, priceMax=null

LƯU Ý GIÁ - QUAN TRỌNG:
- Giá ""5 triệu"" = 5000000, ""10 triệu"" = 10000000, ""20 triệu"" = 20000000
- ""dưới 5 triệu"" → priceMin=0, priceMax=5000000
- ""từ 3 triệu đến 6 triệu"" → priceMin=3000000, priceMax=6000000
- ""khoảng 5 triệu"", ""giá 5 triệu"", ""tầm 5 triệu"", ""gần 5 triệu"" → priceMin=4000000, priceMax=6000000 (±20%)
- ""5 triệu"" (không có từ chính xác) → priceMin=4000000, priceMax=6000000 (±20%)
- Luôn tạo KHOẢNG GIÁ linh hoạt, KHÔNG tìm giá chính xác!

CHỈ TRẢ VỀ JSON, KHÔNG THÊM BẤT KỲ TEXT NÀO KHÁC:
{{""category"": ""...|null"", ""priceMin"": 0|null, ""priceMax"": 0|null}}

Câu hỏi: ""{question}""";

                var response = await _geminiService.ChatAsync(prompt);
                
                // Parse JSON response
                var cleanJson = response.Trim();
                if (cleanJson.StartsWith("```json"))
                {
                    cleanJson = cleanJson.Substring(7);
                }
                if (cleanJson.StartsWith("```"))
                {
                    cleanJson = cleanJson.Substring(3);
                }
                if (cleanJson.EndsWith("```"))
                {
                    cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
                }
                cleanJson = cleanJson.Trim();
                
                var intent = Newtonsoft.Json.JsonConvert.DeserializeObject<UserIntent>(cleanJson);
                
                // Validate category từ DB
                if (!string.IsNullOrEmpty(intent.Category))
                {
                    var validCategory = categories.FirstOrDefault(c => 
                        c.CategoryName.Equals(intent.Category, StringComparison.OrdinalIgnoreCase));
                    
                    if (validCategory == null)
                    {
                        intent.Category = null; // Category không hợp lệ
                    }
                }
                
                return intent;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExtractIntentWithGemini Error: {ex.Message}");
                return null; // Fallback to regex extraction
            }
        }

        /// <summary>
        /// Trích xuất khoảng giá từ câu hỏi
        /// </summary>
        private (decimal Min, decimal Max)? ExtractPriceRange(string question)
        {
            // Pattern: "dưới X triệu", "dưới X tr", "under X million"
            if (question.Contains("dưới") || question.Contains("under"))
            {
                var numbers = System.Text.RegularExpressions.Regex.Matches(question, @"(\d+(?:[.,]\d+)?)");
                if (numbers.Count > 0)
                {
                    var value = SafeParseDecimal(numbers[0].Value);
                    if (value.HasValue)
                    {
                        var multiplier = question.Contains("triệu") || question.Contains("million") ? 1000000 : 1;
                        return (0, value.Value * multiplier);
                    }
                }
            }

            // Pattern: "từ X đến Y triệu"
            if (question.Contains("từ") && question.Contains("đến"))
            {
                var numbers = System.Text.RegularExpressions.Regex.Matches(question, @"(\d+(?:[.,]\d+)?)");
                if (numbers.Count >= 2)
                {
                    var min = SafeParseDecimal(numbers[0].Value);
                    var max = SafeParseDecimal(numbers[1].Value);
                    if (min.HasValue && max.HasValue)
                    {
                        var multiplier = question.Contains("triệu") || question.Contains("million") ? 1000000 : 1;
                        return (min.Value * multiplier, max.Value * multiplier);
                    }
                }
            }

            // Pattern: "khoảng X triệu", "around X million"
            if (question.Contains("khoảng") || question.Contains("around"))
            {
                var numbers = System.Text.RegularExpressions.Regex.Matches(question, @"(\d+(?:[.,]\d+)?)");
                if (numbers.Count > 0)
                {
                    var value = SafeParseDecimal(numbers[0].Value);
                    if (value.HasValue)
                    {
                        var multiplier = question.Contains("triệu") || question.Contains("million") ? 1000000 : 1;
                        var price = value.Value * multiplier;
                        return (price * 0.8m, price * 1.2m); // ±20%
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Trích xuất từ khóa chính từ câu hỏi để tìm kiếm sản phẩm
        /// </summary>
        private List<string> ExtractKeywords(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
                return new List<string>();
            
            // Loại bỏ các từ dừng (stop words) không cần thiết
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "cho", "tôi", "xem", "sản", "phẩm", "này", "đó", "đây", "đấy",
                "có", "không", "và", "hoặc", "giá", "tiền", "triệu", "tr",
                "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín", "mười",
                "trên", "dưới", "khoảng", "từ", "đến", "trong", "ngoài",
                "với", "theo", "về", "là", "của", "các", "để", "được"
            };
            
            // Chuyển thành lowercase và split
            var words = question.ToLower()
                .Split(new[] { ' ', ',', '.', '!', '?', ';', ':', '-', '(', ')' }, 
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 1 && !stopWords.Contains(w))
                .ToList();
            
            return words.Distinct().ToList();
        }

        /// <summary>
        /// ✅ LUỒNG 1: Gửi TẤT CẢ dữ liệu sản phẩm + câu hỏi lên Gemini để AI tự tìm sản phẩm phù hợp
        /// Gemini sẽ trả về danh sách ProductID
        /// </summary>
        private async Task<List<int>> AskGeminiToFindProducts(string question, string allProductsData, List<int> validProductIds)
        {
            try
            {
                if (string.IsNullOrEmpty(allProductsData))
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ [AiController] AskGeminiToFindProducts: allProductsData rỗng");
                    return new List<int>();
                }
                
                // ✅ Prompt cho Gemini để tìm sản phẩm phù hợp - NHẤN MẠNH TÊN SẢN PHẨM KHỚP CHÍNH XÁC
                var questionLower = question.ToLower();
                var keywords = ExtractKeywords(question);
                var keywordsText = keywords.Count > 0 ? string.Join(", ", keywords) : "(không có)";
                
                var prompt = $@"Bạn là trợ lý mua sắm thông minh. Nhiệm vụ của bạn là tìm sản phẩm KHỚP CHÍNH XÁC với câu hỏi của khách hàng.

{allProductsData}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📝 CÂU HỎI KHÁCH HÀNG: {question}
📝 TỪ KHÓA CHÍNH: {keywordsText}
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

⚠️ YÊU CẦU QUAN TRỌNG - ĐỌC KỸ:

1. **TÌM SẢN PHẨM KHỚP CHÍNH XÁC TÊN**:
   - Câu hỏi có thể chứa TÊN SẢN PHẨM CỤ THỂ (ví dụ: ""Combo PC Gaming Cơ Bản"", ""CPU Intel Core i5"", ""RAM DDR4 16GB"")
   - Bạn PHẢI tìm sản phẩm có TÊN khớp hoặc GẦN KHỚP NHẤT với tên trong câu hỏi
   - Ưu tiên tìm sản phẩm có TÊN chứa các từ khóa chính: {keywordsText}

2. **KIỂM TRA KỸ TÊN SẢN PHẨM**:
   - Khi tìm thấy ProductID, bạn PHẢI kiểm tra lại: ""Tên sản phẩm trong danh sách có khớp với câu hỏi '{question}' không?""
   - Nếu không khớp → ĐỪNG chọn ProductID đó!
   - Chỉ chọn ProductID khi TÊN SẢN PHẨM thực sự khớp với câu hỏi

3. **ƯU TIÊN SẢN PHẨM** (nếu có nhiều sản phẩm khớp tên):
   - Sản phẩm còn hàng (Stock > 0)
   - Sản phẩm có khuyến mãi (SalePrice > 0)
   - Giá phù hợp (nếu câu hỏi có đề cập giá)

4. **CHỈ TRẢ VỀ 1 SẢN PHẨM TỐT NHẤT**:
   - Nếu có nhiều sản phẩm khớp, chỉ trả về ProductID của sản phẩm TỐT NHẤT (theo thứ tự ưu tiên ở trên)

📋 FORMAT TRẢ VỀ (QUAN TRỌNG):
- CHỈ trả về ProductID (số), mỗi dòng 1 ProductID
- KHÔNG cần giải thích, KHÔNG cần text khác, CHỈ cần ProductID
- Ví dụ:
  ProductID: 123

- Nếu không tìm thấy sản phẩm nào phù hợp, trả về: NONE

⚠️ KIỂM TRA LẠI TRƯỚC KHI TRẢ VỀ:
- Trước khi trả về ProductID, bạn PHẢI kiểm tra lại: ""Tên sản phẩm trong danh sách có khớp với câu hỏi '{question}' không?""
- Nếu không khớp → Trả về NONE
- CHỈ trả về ProductID khi TÊN SẢN PHẨM thực sự khớp với câu hỏi!";
                
                System.Diagnostics.Debug.WriteLine("🤖 [AiController] Gửi prompt lên Gemini để tìm sản phẩm...");
                var response = await _geminiService.ChatAsync(prompt);
                
                if (string.IsNullOrEmpty(response))
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ [AiController] AskGeminiToFindProducts: Gemini không trả về gì");
                    return new List<int>();
                }
                
                System.Diagnostics.Debug.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                System.Diagnostics.Debug.WriteLine($"📝 [AiController] Gemini response (full):");
                System.Diagnostics.Debug.WriteLine($"{response}");
                System.Diagnostics.Debug.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                
                // ✅ Parse ProductID từ response - ƯU TIÊN tìm "ProductID:" prefix
                var productIds = new List<int>();
                var lines = response.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                
                System.Diagnostics.Debug.WriteLine($"🔍 [AiController] Parsing response, có {lines.Length} dòng");
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    // Bỏ qua các dòng không phải số
                    if (trimmedLine.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ [AiController] Gemini trả về NONE - không tìm thấy sản phẩm");
                        return new List<int>();
                    }
                    
                    // ✅ Ưu tiên tìm pattern "ProductID: 123" hoặc "ProductID:123"
                    if (trimmedLine.StartsWith("ProductID:", StringComparison.OrdinalIgnoreCase))
                    {
                        var productIdStr = trimmedLine.Substring("ProductID:".Length).Trim();
                        if (int.TryParse(productIdStr, out int productId))
                        {
                            System.Diagnostics.Debug.WriteLine($"✅ [AiController] Tìm thấy ProductID từ prefix: {productId}");
                            productIds.Add(productId);
                            continue;
                        }
                    }
                    
                    // ✅ Nếu không có prefix, tìm số đứng độc lập trên dòng (chỉ số, không có text khác)
                    if (System.Text.RegularExpressions.Regex.IsMatch(trimmedLine, @"^\d+$"))
                    {
                        if (int.TryParse(trimmedLine, out int productId))
                        {
                            System.Diagnostics.Debug.WriteLine($"✅ [AiController] Tìm thấy ProductID độc lập: {productId}");
                            productIds.Add(productId);
                            continue;
                        }
                    }
                    
                    // ✅ Fallback: Tìm số đầu tiên trong dòng (nếu có text kèm theo)
                    var match = System.Text.RegularExpressions.Regex.Match(trimmedLine, @"\b(\d+)\b");
                    if (match.Success)
                    {
                        if (int.TryParse(match.Groups[1].Value, out int productId))
                        {
                            System.Diagnostics.Debug.WriteLine($"✅ [AiController] Tìm thấy ProductID từ regex: {productId} (từ dòng: {trimmedLine})");
                            productIds.Add(productId);
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"📊 [AiController] Tổng cộng parse được {productIds.Count} ProductID");
                
                // ✅ VALIDATION: Chỉ giữ lại ProductID có trong danh sách gửi lên Gemini
                var validatedProductIds = productIds
                    .Where(id => validProductIds.Contains(id))
                    .Distinct()
                    .Take(5)
                    .ToList();
                
                // ✅ Log những ProductID không hợp lệ (nếu có)
                var invalidIds = productIds.Where(id => !validProductIds.Contains(id)).ToList();
                if (invalidIds.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ [AiController] Phát hiện {invalidIds.Count} ProductID KHÔNG HỢP LỆ (không có trong danh sách): [{string.Join(", ", invalidIds)}]");
                }
                
                // ✅ VALIDATION: Log tất cả ProductID parse được và validated
                System.Diagnostics.Debug.WriteLine($"📊 [AiController] Parse được {productIds.Count} ProductID, sau validation còn {validatedProductIds.Count} ProductID hợp lệ");
                if (validatedProductIds.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"✅ [AiController] ProductID HỢP LỆ: [{string.Join(", ", validatedProductIds)}]");
                }
                else if (productIds.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"❌ [AiController] KHÔNG CÓ ProductID HỢP LỆ! ProductID parse được: [{string.Join(", ", productIds)}]");
                }
                
                return validatedProductIds;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [AiController] AskGeminiToFindProducts Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
                return new List<int>();
            }
        }
        
        /// <summary>
        /// Trích xuất số lượng sản phẩm user muốn xem (tối đa 3)
        /// Ví dụ: "3 sản phẩm", "2 món", "cho tôi xem 1 cái"
        /// </summary>
        private int ExtractProductCount(string question)
        {
            var lowerQuestion = question.ToLower();
            
            // Pattern: "3 sản phẩm", "2 món", "1 cái", "cho tôi xem 3 cái"
            var patterns = new[]
            {
                @"(\d+)\s*(?:sản phẩm|san pham|món|mon|cái|caí|item)",
                @"(?:cho tôi xem|cho xem|show|hiện)\s*(\d+)\s*(?:sản phẩm|san pham|món|mon|cái|caí)?",
                @"(?:lấy|lấy ra|get|get me)\s*(\d+)"
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(lowerQuestion, pattern);
                if (match.Success && match.Groups.Count > 1)
                {
                    var count = SafeParseInt(match.Groups[1].Value);
                    if (count.HasValue && count.Value > 0)
                    {
                        // Giới hạn tối đa 3
                        return Math.Min(count.Value, 3);
                    }
                }
            }
            
            return 0; // Không tìm thấy số lượng
        }
        
        /// <summary>
        /// Safe parse int
        /// </summary>
        private int? SafeParseInt(string value)
        {
            if (int.TryParse(value, out int result))
                return result;
            return null;
        }
        
        /// <summary>
        /// Trích xuất số lượng sản phẩm user muốn xem (OLD - giữ lại để tương thích)
        /// </summary>
        private int ExtractQuantity(string question)
        {
            return ExtractProductCount(question);
        }

        /// <summary>
        /// Parse decimal an toàn, xử lý cả "," và "." (VD: "5,5" hoặc "5.5")
        /// </summary>
        private decimal? SafeParseDecimal(string value)
        {
            try
            {
                // Remove all dots (thousands separator): "5.000.000" → "5000000"
                // Keep comma as decimal separator: "5,5" → "5,5"
                var normalized = value.Replace(".", "");
                
                // Convert comma to dot for parsing: "5,5" → "5.5"
                normalized = normalized.Replace(",", ".");
                
                if (decimal.TryParse(normalized, 
                    System.Globalization.NumberStyles.Any, 
                    System.Globalization.CultureInfo.InvariantCulture, 
                    out decimal result))
                {
                    return result;
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Trích xuất danh mục từ câu hỏi (query DB động)
        /// </summary>
        private string ExtractCategory(string question)
        {
            try
            {
                // ✅ Fix: Dùng cached categories
                var categories = GetCachedCategories();
                
                var lowerQuestion = question.ToLower();
                
                // Match tên category từ DB
                // Sắp xếp theo độ dài (dài nhất trước) để match chính xác hơn
                // VD: "Món Ăn Chính" sẽ match trước "Món Ăn"
                var sortedCategories = categories
                    .OrderByDescending(c => c.CategoryName.Length)
                    .ToList();
                
                foreach (var category in sortedCategories)
                {
                    var categoryLower = category.CategoryName.ToLower();
                    
                    // Match chính xác tên category
                    if (lowerQuestion.Contains(categoryLower))
                    {
                        return category.CategoryName;
                    }
                }
                
                // Không tìm thấy category nào match
                // → Để SearchProducts() tự xử lý bằng keyword search
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExtractCategory Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Nhận diện số người từ câu hỏi (VD: "4 người", "2 người ăn")
        /// </summary>
        private int? ExtractPeopleCount(string question)
        {
            try
            {
                var lowerQuestion = question.ToLower();
                
                // Pattern: "2 người", "4 người ăn", "cho 3 người", "nhóm 5 người"
                var patterns = new[]
                {
                    @"(\d+)\s*người",           // "4 người"
                    @"cho\s*(\d+)",             // "cho 4"
                    @"nhóm\s*(\d+)",            // "nhóm 4"
                    @"bàn\s*(\d+)",             // "bàn 4"
                    @"(\d+)\s*(?:ng(?:ười|uoi))", // "4 ng" (typo)
                };
                
                foreach (var pattern in patterns)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(lowerQuestion, pattern);
                    if (match.Success)
                    {
                        if (int.TryParse(match.Groups[1].Value, out int count))
                        {
                            System.Diagnostics.Debug.WriteLine($"ExtractPeopleCount: Found {count} people");
                            return count;
                        }
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExtractPeopleCount Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Nhận diện loại linh kiện & nhu cầu từ câu hỏi
        /// </summary>
        private string ExtractComponentType(string question)
        {
            try
            {
                var lowerQuestion = question.ToLower();
                
                // 1. LOẠI LINH KIỆN
                if (lowerQuestion.Contains("cpu") || lowerQuestion.Contains("processor") || 
                    lowerQuestion.Contains("chip") || lowerQuestion.Contains("bộ xử lý"))
                {
                    return "cpu processor chip";
                }
                
                if (lowerQuestion.Contains("mainboard") || lowerQuestion.Contains("bo mạch chủ") || 
                    lowerQuestion.Contains("motherboard") || lowerQuestion.Contains("main"))
                {
                    return "mainboard motherboard bo mạch";
                }
                
                if (lowerQuestion.Contains("ram") || lowerQuestion.Contains("bộ nhớ") || 
                    lowerQuestion.Contains("memory") || lowerQuestion.Contains("ddr"))
                {
                    return "ram memory bộ nhớ ddr";
                }
                
                if (lowerQuestion.Contains("ssd") || lowerQuestion.Contains("hdd") || 
                    lowerQuestion.Contains("ổ cứng") || lowerQuestion.Contains("hard drive"))
                {
                    return "ssd hdd ổ cứng storage";
                }
                
                if (lowerQuestion.Contains("vga") || lowerQuestion.Contains("card đồ họa") || 
                    lowerQuestion.Contains("gpu") || lowerQuestion.Contains("graphics"))
                {
                    return "vga gpu card đồ họa graphics";
                }
                
                if (lowerQuestion.Contains("psu") || lowerQuestion.Contains("nguồn") || 
                    lowerQuestion.Contains("power supply"))
                {
                    return "psu nguồn power supply";
                }
                
                // 2. PHỤ KIỆN
                if (lowerQuestion.Contains("quạt") || lowerQuestion.Contains("fan") || 
                    lowerQuestion.Contains("tản nhiệt") || lowerQuestion.Contains("cooler"))
                {
                    return "quạt fan tản nhiệt cooler";
                }
                
                if (lowerQuestion.Contains("bàn phím") || lowerQuestion.Contains("keyboard"))
                {
                    return "bàn phím keyboard";
                }
                
                if (lowerQuestion.Contains("chuột") || lowerQuestion.Contains("mouse"))
                {
                    return "chuột mouse";
                }
                
                if (lowerQuestion.Contains("tai nghe") || lowerQuestion.Contains("headphone") || 
                    lowerQuestion.Contains("headset"))
                {
                    return "tai nghe headphone headset";
                }
                
                // 3. NHU CẦU
                if (lowerQuestion.Contains("gaming") || lowerQuestion.Contains("chơi game") || 
                    lowerQuestion.Contains("game"))
                {
                    return "gaming cpu vga ram";
                }
                
                if (lowerQuestion.Contains("văn phòng") || lowerQuestion.Contains("office") || 
                    lowerQuestion.Contains("làm việc"))
                {
                    return "cpu ram ssd văn phòng";
                }
                
                if (lowerQuestion.Contains("streaming") || lowerQuestion.Contains("stream"))
                {
                    return "cpu ram vga webcam microphone";
                }
                
                if (lowerQuestion.Contains("budget") || lowerQuestion.Contains("giá rẻ") || 
                    lowerQuestion.Contains("tiết kiệm"))
                {
                    return "cpu ram ssd giá rẻ";
                }
                
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExtractComponentType Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Phát hiện form câu hỏi của người dùng
        /// Trả về: "FORM1", "FORM2", "FORM3", "FORM4", "FORM5", "INVALID"
        /// </summary>
        private string DetectQuestionForm(string question)
        {
            var lowerQuestion = question.ToLower();
            var category = ExtractCategory(question);
            var hasPrice = System.Text.RegularExpressions.Regex.IsMatch(lowerQuestion, @"(\d+(?:[.,]\d+)?)\s*(?:triệu|tr|m)");
            
            // FORM 2: Chỉ tên sản phẩm (không có danh mục và giá)
            if (string.IsNullOrEmpty(category) && !hasPrice)
            {
                // Kiểm tra xem có phải là tên sản phẩm cụ thể không (VD: CPU, RAM, SSD)
                var productKeywords = new[] { "cpu", "ram", "ssd", "hdd", "vga", "psu", "mainboard", "bo mạch chủ", 
                    "bàn phím", "chuột", "tai nghe", "webcam", "loa", "combo", "windows", "office" };
                if (productKeywords.Any(keyword => lowerQuestion.Contains(keyword)))
                {
                    return "FORM2";
                }
                return "INVALID";
            }
            
            // FORM 1: Danh mục + Giá tiền (VD: "Linh kiện máy tính giá 5 triệu")
            if (!string.IsNullOrEmpty(category) && hasPrice && 
                !lowerQuestion.Contains("trên") && !lowerQuestion.Contains("dưới") && 
                !lowerQuestion.Contains("duoi") && !lowerQuestion.Contains("tren") &&
                !lowerQuestion.Contains("khoảng") && !lowerQuestion.Contains("từ") && !lowerQuestion.Contains("đến"))
            {
                return "FORM1";
            }
            
            // FORM 3: Danh mục + Giá trên + Khoảng giá (VD: "Linh kiện máy tính giá trên 5 triệu")
            if (!string.IsNullOrEmpty(category) && hasPrice && 
                (lowerQuestion.Contains("trên") || lowerQuestion.Contains("tren") || lowerQuestion.Contains("cao hơn")))
            {
                return "FORM3";
            }
            
            // FORM 4: Danh mục + Giá thấp hơn + Giá tiền (VD: "Linh kiện máy tính giá dưới 5 triệu")
            if (!string.IsNullOrEmpty(category) && hasPrice && 
                (lowerQuestion.Contains("dưới") || lowerQuestion.Contains("duoi") || lowerQuestion.Contains("thấp hơn")))
            {
                return "FORM4";
            }
            
            // FORM 5: Danh mục + Khoảng - 2 giá tiền (VD: "Linh kiện máy tính khoảng 4-5 triệu")
            if (!string.IsNullOrEmpty(category) && hasPrice && 
                (lowerQuestion.Contains("khoảng") || lowerQuestion.Contains("từ") || 
                 lowerQuestion.Contains("đến") || System.Text.RegularExpressions.Regex.IsMatch(lowerQuestion, @"\d+\s*(?:triệu|tr|m)\s*[-–]\s*\d+")))
            {
                return "FORM5";
            }
            
            // Nếu có giá nhưng không có danh mục → Có thể là form không đầy đủ
            if (string.IsNullOrEmpty(category) && hasPrice)
            {
                return "NEEDS_CATEGORY";
            }
            
            // Nếu có danh mục nhưng không có giá → Có thể là form không đầy đủ
            if (!string.IsNullOrEmpty(category) && !hasPrice)
            {
                return "NEEDS_PRICE";
            }
            
            return "UNKNOWN";
        }

        /// <summary>
        /// Xây dựng system prompt cho AI
        /// </summary>
        private string BuildSystemPrompt(string productContext)
        {
            return $@"BẠN LÀ TRỢ LÝ TƯ VẤN LINH KIỆN MÁY TÍNH

**VAI TRÒ:** Tư vấn linh kiện máy tính thân thiện, chuyên nghiệp.

**QUY TẮC:**
✅ CHỈ giới thiệu sản phẩm có trong danh sách dưới đây
✅ KHÔNG nhắc lại tên đầy đủ (đã có trên ảnh)
✅ KHÔNG nhắc giá (đã hiển thị)
✅ Nếu KHÔNG có sản phẩm → ĐIỀU HƯỚNG khách hỏi theo form đúng
✅ Trả lời ngắn gọn (~80 từ), thân thiện

**KHI CÓ SẢN PHẨM - CÁCH TRẢ LỜI:**
1. Chào hỏi ngắn (1 câu)
2. Giới thiệu ưu điểm chính (2-3 điểm)
3. Câu hỏi mở

**KHI KHÔNG CÓ SẢN PHẨM - ĐIỀU HƯỚNG KHÁCH:**
- Xin lỗi ngắn gọn
- GIẢI THÍCH RÕ RÀNG các form để hỏi
- ĐƯA VÍ DỤ cụ thể dựa trên yêu cầu của khách
- Khuyến khích khách hỏi lại theo form đúng

**CÁC FORM HỎI ĐÚNG:**
1. [Danh mục] + [Giá tiền] - VD: ""Linh kiện máy tính giá 5 triệu""
2. Tên sản phẩm cụ thể - VD: ""CPU Intel Core i5""
3. [Danh mục] + Giá trên [giá] - VD: ""Linh kiện máy tính giá trên 5 triệu""
4. [Danh mục] + Giá dưới [giá] - VD: ""Linh kiện máy tính giá dưới 5 triệu""
5. [Danh mục] + Khoảng [giá] - [giá] - VD: ""Linh kiện máy tính khoảng 4-5 triệu""

{productContext}

Hãy tư vấn dựa trên danh sách trên!";
        }

        /// <summary>
        /// Helper: Get icon based on category name
        /// </summary>
        private string GetCategoryIcon(string categoryName)
        {
            var lower = categoryName.ToLower();
            if (lower.Contains("món ăn chính") || lower.Contains("cơm") || lower.Contains("phở")) return "🍜";
            if (lower.Contains("món ăn nhẹ") || lower.Contains("ăn nhẹ") || lower.Contains("bánh")) return "🥐";
            if (lower.Contains("đồ uống") || lower.Contains("nước") || lower.Contains("trà") || lower.Contains("cà phê")) return "☕";
            if (lower.Contains("tráng miệng") || lower.Contains("chè") || lower.Contains("ngọt")) return "🍰";
            if (lower.Contains("combo") || lower.Contains("tiết kiệm")) return "🎁";
            return "🍽️";
        }
        
        /// <summary>
        /// Chọn sản phẩm tốt nhất từ danh sách kết quả tìm kiếm
        /// Logic: Ưu tiên sản phẩm có khuyến mãi, còn hàng, relevance score cao, giá hợp lý
        /// </summary>
        private ProductSearchResult GetBestProduct(List<ProductSearchResult> searchResults)
        {
            if (searchResults == null || searchResults.Count == 0)
                return null;
            
            // Nếu chỉ có 1 sản phẩm, trả về luôn
            if (searchResults.Count == 1)
                return searchResults[0];
            
            // Tính điểm số cho mỗi sản phẩm để chọn sản phẩm tốt nhất
            var scoredProducts = searchResults
                .Select(p => new
                {
                    Product = p,
                    Score = CalculateProductScore(p)
                })
                .OrderByDescending(x => x.Score)
                .ToList();
            
            System.Diagnostics.Debug.WriteLine($"🏆 [AiController] Chọn sản phẩm tốt nhất:");
            foreach (var item in scoredProducts.Take(3))
            {
                System.Diagnostics.Debug.WriteLine($"   - {item.Product.Name}: Score = {item.Score}");
            }
            
            return scoredProducts.First().Product;
        }
        
        /// <summary>
        /// Tính điểm số cho sản phẩm để chọn sản phẩm tốt nhất
        /// Điểm số cao hơn = sản phẩm tốt hơn
        /// </summary>
        private double CalculateProductScore(ProductSearchResult product)
        {
            double score = 0;
            
            // 1. Điểm Relevance Score (từ 0-1, cao hơn = tốt hơn)
            score += product.RelevanceScore * 100; // 0-100 điểm
            
            // 2. Ưu tiên sản phẩm có khuyến mãi (+50 điểm)
            if (product.PromotionPrice.HasValue && product.PromotionPrice.Value > 0 && 
                product.PromotionPrice.Value < product.Price)
            {
                score += 50;
                
                // Phần trăm giảm giá càng cao, điểm càng cao (tối đa +30 điểm)
                var discountPercent = ((product.Price - product.PromotionPrice.Value) / product.Price) * 100;
                score += (double)Math.Min(discountPercent * 0.3m, 30m); // Tối đa 30 điểm - cast kết quả về double
            }
            
            // 3. Ưu tiên sản phẩm còn hàng (+20 điểm nếu có stock)
            if (product.TotalQuantity > 0)
            {
                score += 20;
                
                // Stock càng nhiều, điểm càng cao (tối đa +10 điểm)
                score += Math.Min(product.TotalQuantity / 10.0, 10);
            }
            
            // 4. Ưu tiên giá hợp lý (không quá đắt, không quá rẻ)
            // Giảm điểm nếu giá quá cao (> 50 triệu) hoặc quá thấp (< 10,000đ)
            if (product.Price > 50000000) // > 50 triệu
            {
                score -= 20; // Trừ điểm vì có thể quá đắt
            }
            else if (product.Price < 10000) // < 10,000đ
            {
                score -= 10; // Trừ điểm vì có thể là phụ kiện nhỏ
            }
            
            // 5. Ưu tiên sản phẩm có hình ảnh (+5 điểm)
            if (!string.IsNullOrEmpty(product.ImagePath))
            {
                score += 5;
            }
            
            return score;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _indexService?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #region Request/Response Models

    public class ChatRequest
    {
        public string Question { get; set; }
        public int? UserId { get; set; }
        public string SessionId { get; set; }
    }

    public class ChatResponse
    {
        public string Answer { get; set; }
        public List<ProductSuggestion> RelatedProducts { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ProductSuggestion
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public decimal? PromotionPrice { get; set; }
        public string ImageUrl { get; set; }
        public string Url { get; set; }
    }

    public class UserIntent
    {
        public string Category { get; set; }
        public decimal? PriceMin { get; set; }
        public decimal? PriceMax { get; set; }
    }

    #endregion
}


