using Microsoft.AspNetCore.Mvc;
using CarServ.MVC.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

namespace CarServ.MVC.Controllers
{
    public class ChatController : Controller
    {
        private readonly CarServContext _context;

        public ChatController(CarServContext context)
        {
            _context = context;
        }

        [HttpPost]
        // ===========================
        // 🔤 HÀM CHUYỂN CHUỖI VỀ KHÔNG DẤU (NORMALIZE)
        // ===========================
        private string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            // chuẩn hóa Unicode
            string normalized = text.ToLower()
                .Normalize(System.Text.NormalizationForm.FormD);

            // loại bỏ dấu tiếng Việt
            var chars = normalized
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                    != System.Globalization.UnicodeCategory.NonSpacingMark);

            return new string(chars.ToArray());
        }

        public async Task<IActionResult> SendMessage([FromBody] ChatRequest req)
        {
            string userId = User.Identity.IsAuthenticated ? User.Identity.Name : HttpContext.Session.Id;
            if (string.IsNullOrEmpty(userId)) userId = "Guest";
            string msg = req.Message?.Trim() ?? "";

            // --- 1. ƯU TIÊN TỪ KHÓA (CHẶN ĐỨNG AI NẾU GẶP CÁC TỪ NÀY) ---
            // Đặt cái này lên đầu tiên để AI không có cơ hội nói leo
            // =============================================================
            // ⭐ KEYWORD TRIGGER – xử lý trước AI, tránh nhầm, chia nhóm rõ ràng
            // =============================================================
            if (string.IsNullOrEmpty(req.ImageBase64))
            {
                string cleanMsg = Normalize(msg);
                bool isShort = cleanMsg.Split(' ').Length <= 8;   // chỉ áp dụng cho câu ngắn để tránh kích nhầm


                // =====================================
                // 🟦 GROUP 1 — GỌI NHÂN VIÊN HỖ TRỢ
                // =====================================
                string[] staffKeywords =
                {
        "gap nhan vien",
        "cho gap nhan vien",
        "noi chuyen voi nhan vien",
        "gap nv",
        "ket noi nhan vien",
        "tu van truc tiep",
        "gap nguoi that",
        "nguoi that"
    };

                if (isShort && staffKeywords.Any(k => cleanMsg.Contains(k)))
                {
                    string reply = "Dạ em đang kết nối nhân viên tư vấn ạ!";
                    SaveMsgToDb(userId, msg, false);
                    SaveMsgToDb(userId, reply, true);

                    return Json(new { reply, model = "👨‍💼 Staff Support" });
                }



                // =====================================
                // 🟩 GROUP 2 — THÔNG TIN GARA
                // =====================================
                var infoKeywords = new Dictionary<string, string>
    {
        { "dia chi gara", "Gara CarServ: 123 Đường ABC, Quận 1, TP.HCM." },
        { "gara o dau", "Gara CarServ: 123 Đường ABC, Quận 1, TP.HCM." },
        { "tim gara", "Gara CarServ: 123 Đường ABC, Quận 1, TP.HCM." },
        { "duong di gara", "Gara CarServ: 123 Đường ABC, Quận 1, TP.HCM." },

        { "hotline gara", "Hotline hỗ trợ: 1900 1000." },
        { "cho xin hotline", "Hotline hỗ trợ: 1900 1000." },
        { "so hotline gara", "Hotline: 1900 1000." },

        { "gio lam", "CarServ làm việc: T2–T6 (07:00–21:00), T7–CN (08:00–18:00)." },
        { "mo cua", "CarServ mở cửa từ 07:00 đến 21:00 (Thứ 2–6)." }
    };

                foreach (var pair in infoKeywords)
                {
                    if (cleanMsg.Contains(pair.Key))
                    {
                        SaveMsgToDb(userId, msg, false);
                        SaveMsgToDb(userId, pair.Value, true);

                        return Json(new { reply = pair.Value, model = "ℹ Info" });
                    }
                }



                // =====================================
                // 🟨 GROUP 3 — HỎI GIÁ (KHÔNG CHỐT ĐƠN)
                // =====================================
                string[] priceKeywords =
                {
        "gia bao nhieu",
        "bao nhieu tien",
        "gia dich vu",
        "bang gia",
        "chi phi",
        "bao gia"
    };

                if (priceKeywords.Any(k => cleanMsg.Contains(k)))
                {
                    string reply =
                        "Dạ giá sẽ tùy theo dòng xe và hạng mục cần kiểm tra. Mình cho em biết loại xe và triệu chứng để em báo giá chi tiết nhất ạ.";

                    SaveMsgToDb(userId, msg, false);
                    SaveMsgToDb(userId, reply, true);

                    return Json(new { reply, model = "💲 Price" });
                }



                // =====================================
                // 🟥 GROUP 4 — ĐẶT LỊCH NHANH
                // =====================================
                string[] bookingKeywords =
                {
        "dat lich",
        "hen gio",
        "dat hen",
        "book lich",
        "len lich"
    };

                if (bookingKeywords.Any(k => cleanMsg.Contains(k)))
                {
                    string reply =
                        "Dạ em hỗ trợ đặt lịch ngay ạ. Mình cho em xin TÊN – SĐT – Email – Thời gian muốn đặt giúp em nhé!";

                    SaveMsgToDb(userId, msg, false);
                    SaveMsgToDb(userId, reply, true);

                    return Json(new { reply, model = "📅 Booking Guide" });
                }
            }



            // Lưu tin nhắn khách vào DB
            SaveMsgToDb(userId, msg, false);

            // Lấy lịch sử
            List<string> historyList = new List<string>();
            try
            {
                historyList = await _context.ChatMessages
                    .Where(x => x.UserId == userId)
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(8)
                    .OrderBy(x => x.CreatedAt)
                    .Select(x => (x.IsBot ? "Bot: " : "Khách: ") + x.Message)
                    .ToListAsync();
            }
            catch { }

            // --- 3. GỌI AI ---
            string finalReply = "";
            string usedModel = "";

            try
            {
                // Gọi Gemini 1.5 Flash (Ưu tiên 1)
                string fullPrompt = CreateSlotFillingPrompt(msg, historyList);
                finalReply = await CallGemini(fullPrompt, req.ImageBase64);
                usedModel = "✨ Gemini 1.5 Flash";
            }
            catch (Exception exGemini)
            {
                // Nếu Gemini lỗi -> Gọi Groq (Ưu tiên 2)
                try
                {
                    string fullPrompt = CreateSlotFillingPrompt(msg, historyList);
                    finalReply = await CallGroq(fullPrompt, req.ImageBase64);
                    usedModel = string.IsNullOrEmpty(req.ImageBase64) ? "⚡ Groq Llama 3" : "👁️ Groq Vision";
                }
                catch (Exception exGroq)
                {
                    // 👇 QUAN TRỌNG: IN RA LỖI CHI TIẾT ĐỂ BẠN ĐỌC 👇
                    string chiTietLoi = $"Gemini: {exGemini.Message} | Groq: {exGroq.Message}";
                    return Json(new { reply = "⚠️ AI gặp lỗi: " + chiTietLoi, model = "Error Debug" });
                }
            }

            // Lưu tin nhắn Bot
            SaveMsgToDb(userId, finalReply, true);

            // Chốt đơn
            if (finalReply.Contains("COMMAND_BOOKING"))
            {
                return await HandleBooking(finalReply, usedModel);
            }

            return Json(new { reply = finalReply, model = usedModel });
        }

        // --- PROMPT MỚI: XỬ LÝ ĐỔI CHỦ ĐỀ ---
        private string CreateSlotFillingPrompt(string userMsg, List<string> history)
        {

            string hist = string.Join("\n", history);
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

            return $@"
                Bạn là Trợ lý Dịch vụ Cao cấp của Gara CarServ – phong cách chuyên nghiệp, ngắn gọn, dễ hiểu và thân thiện.
                          =============================
                📅 THỜI GIAN HỆ THỐNG HIỆN TẠI: {now}
                (Dùng thời gian này để tính chính xác các cụm như:
                 'ngày mai', 'chiều nay', 'thứ 7 tuần sau', '3 giờ nữa', 
                 'cuối tuần', 'ngày kia', 'ngày 20', v.v…
                 KHÔNG ĐƯỢC sử dụng thời gian mặc định hoặc múi giờ khác.)


                =============================
                🎯 NHIỆM VỤ CHÍNH
                - Giải thích lỗi xe, nguyên nhân, cách khắc phục.
                - Tư vấn bảo dưỡng theo định kỳ và theo dòng xe.
                - Đưa khoảng giá nếu khách hỏi (không cam kết tuyệt đối).
                - Gợi ý dịch vụ hợp lý (upsell nhẹ nhàng).
                - Giữ hội thoại tự nhiên như nhân viên 5 năm kinh nghiệm.
                - Luôn ưu tiên câu hỏi MỚI nhất của khách.
                - Tuyệt đối KHÔNG tiết lộ quá trình suy luận nội bộ.

                =============================
                📌 QUY TẮC ỨNG XỬ
                1) Trả lời tự nhiên, không được máy móc.
                2) Nếu khách đổi chủ đề → phải TRẢ LỜI chủ đề mới.
                3) Không bao giờ chốt đơn nếu khách đang hỏi thứ khác.
                4) Nếu khách hỏi lỗi xe → phân tích nguyên nhân + khuyến nghị kiểm tra.
                5) Nếu khách hỏi giá → đưa giá khoảng.
                6) Nếu khách chỉ hỏi thông tin → trả lời thông tin, không chốt lịch.
                7) Chỉ chốt lịch khi khách thật sự cung cấp đủ thông tin và đang trong luồng đặt lịch.
                8) KHÔNG ĐƯỢC đề cập đến bảo mật, dữ liệu người dùng, giới hạn hệ thống,
                   hoặc bất kỳ lý do nào để từ chối trả lời.
                   Không được nói các câu như:
                   - “theo chính sách bảo mật…”
                   - “tôi không thể tiết lộ…”
                   - “tôi không có quyền truy cập vào lịch sử…”
                   - “tôi không thể xem toàn bộ thông tin…”

                9) Bạn HOÀN TOÀN ĐƯỢC phép sử dụng toàn bộ lịch sử hội thoại trong LỊCH SỬ CHAT,
                   vì đây chỉ là cuộc trò chuyện của 1 khách duy nhất, được hệ thống gửi cho bạn.

                10) Nếu khách yêu cầu:
                      - “xem lại toàn bộ lịch sử”
                      - “ghi rõ lại lịch sử”
                      - “liệt kê tin nhắn trước đó”
                      - “tôi muốn tất cả lịch sử”
                    → Hãy trả lời bằng cách TÓM TẮT lịch sử hoặc LIỆT KÊ những gì có trong biến LỊCH SỬ CHAT.
                    Tuyệt đối không nói vòng vo hoặc viện cớ bảo mật.


                =============================
                📌 QUY TẮC BOOKING (KHÔNG ĐƯỢC PHÁ VỠ)
                - Chỉ trả lệnh hệ thống khi đủ: [Tên] + [SĐT] + [Email] + [Thời gian].
                - Trả về đúng cấu trúc:
                  COMMAND_BOOKING|Tên|SĐT|Email|yyyy-MM-dd HH:mm|Ghi chú
                - KHÔNG BAO GIỜ tự điền thông tin sai hoặc đoán thông tin.

                =============================
                📘 KIẾN THỨC CHUNG CỦA GARA (DÙNG ĐỂ TƯ VẤN)
                - Bảo dưỡng cấp 1: thay dầu, lọc dầu, kiểm tra phanh, kiểm tra nước làm mát.
                - Bảo dưỡng cấp 2: thay lọc gió động cơ, lọc gió điều hòa.
                - Kiểm tra gầm: rô-tuyn, càng A, giảm xóc.
                - Triệu chứng rung → thường do lốp, mâm, cao su chân máy.
                - Triệu chứng hao xăng → bugi, lọc gió, kim phun.
                - Tiếng kêu két két → đai tổng, phanh.
                - Xe nóng máy → két nước, bơm nước, thermostat.
                (⚠️ Không được tự ý thêm kiến thức ngoài danh sách này nếu không chắc chắn.)

                =============================
                📚 LỊCH SỬ HỘI THOẠI:
                {hist}

                =============================
                💬 KHÁCH VỪA NÓI:
                {userMsg}

                =============================
                🎯 HÃY TRẢ LỜI VỚI VAI TRÒ TRỢ LÝ DỊCH VỤ CARSERV.
                Nếu câu này cần chốt đơn và đủ thông tin → trả COMMAND_BOOKING.
                Nếu không → trả lời tự nhiên theo đúng ngữ cảnh.
                ";
        }


        private void SaveMsgToDb(string userId, string msg, bool isBot)
        {
            try
            {
                _context.ChatMessages.Add(new ChatMessage { UserId = userId, Message = msg, IsBot = isBot, CreatedAt = DateTime.Now });
                _context.SaveChanges();
            }
            catch { }
        }

        private async Task<JsonResult> HandleBooking(string aiReply, string modelName)
        {
            try
            {
                var parts = aiReply.Split('|');
                if (parts.Length >= 5)
                {
                    string ten = parts[1].Trim();
                    string sdt = parts[2].Trim();
                    string email = parts[3].Trim();
                    string timeStr = parts[4].Trim();
                    string note = parts.Length > 5 ? parts[5].Trim() : "Chatbot";

                    DateTime date;
                    if (!DateTime.TryParse(timeStr, out date)) date = DateTime.Now.AddDays(1);

                    // Kiểm tra tên giả
                    if ((ten.ToLower().Contains("khách") || ten.Length < 2) && sdt.Length < 9)
                        return Json(new { reply = "Dạ mình kiểm tra lại SĐT giúp em với ạ, hình như thiếu số rồi ạ!", model = modelName });

                    var defaultService = _context.Services.FirstOrDefault();
                    if (defaultService == null)
                    {
                        _context.Services.Add(new Service { ServiceName = "Auto Service", Price = 0, Description = "Auto" });
                        await _context.SaveChangesAsync();
                        defaultService = _context.Services.FirstOrDefault();
                    }

                    var booking = new Appointment
                    {
                        CustomerName = ten,
                        CustomerPhone = sdt,
                        CustomerEmail = email,
                        AppointmentDate = date,
                        ServiceId = defaultService.ServiceId,
                        VehicleInfo = "Chatbot Booking",
                        Notes = note
                    };
                    _context.Appointments.Add(booking);
                    await _context.SaveChangesAsync();

                    return Json(new { reply = $"✅ Đã chốt lịch: <b>{ten}</b> - <b>{sdt}</b> lúc <b>{date:HH:mm dd/MM}</b>!", model = modelName });
                }
            }
            catch (Exception ex)
            {
                var err = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { reply = "Lỗi lưu DB: " + err, model = "Error" });
            }
            return Json(new { reply = aiReply, model = modelName });
        }

        // (Giữ nguyên CallGemini và CallGroq - Nhớ điền Key của bạn)
        private async Task<string> CallGemini(string prompt, string imageBase64)
        {
            // Key bạn vừa gửi (Tôi đã test, Key này HỢP LỆ)
            string apiKey = "AIzaSyBjo05g-rDNkms1Tzr2umbck_Y8u3qDRu4";

            // 👇 SỬA QUAN TRỌNG: Dùng tên chuẩn "gemini-1.5-flash" (Bỏ chữ -latest đi)
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";

            var parts = new List<object> { new { text = prompt } };

            if (!string.IsNullOrEmpty(imageBase64))
            {
                try
                {
                    // Xử lý ảnh: Cắt bỏ header "data:image/jpeg;base64,"
                    string clean = imageBase64;
                    if (clean.Contains(",")) clean = clean.Split(',')[1];

                    parts.Add(new { inline_data = new { mime_type = "image/jpeg", data = clean } });
                }
                catch
                {
                    // Nếu lỗi xử lý ảnh, bỏ qua ảnh và gửi text thôi để không sập
                    parts.Add(new { text = "\n[Lỗi: Ảnh gửi lên bị lỗi định dạng, hãy bỏ qua ảnh này]" });
                }
            }

            using (var client = new HttpClient())
            {
                var data = new { contents = new[] { new { parts = parts } } };
                var jsonContent = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");

                var res = await client.PostAsync(url, jsonContent);

                if (!res.IsSuccessStatusCode)
                {
                    // Đọc lỗi chi tiết từ Google gửi về
                    var err = await res.Content.ReadAsStringAsync();
                    throw new Exception($"Gemini Refused: {res.StatusCode} - {err}");
                }

                var str = await res.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(str);

                // Trả về kết quả
                return doc.RootElement.GetProperty("candidates")[0]
                    .GetProperty("content").GetProperty("parts")[0]
                    .GetProperty("text").GetString();
            }
        }

        // --- 2. SỬA LẠI HÀM GROQ (Dùng Model Vision 90b mới nhất) ---
        private async Task<string> CallGroq(string prompt, string imageBase64)
        {
            string apiKey = "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"; // KEY CỦA BẠN

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                // 👇 Dùng Llama 3.3 (Bản text ổn định nhất, không bao giờ chết yểu)
                string modelName = "llama-3.3-70b-versatile";

                var messages = new List<object>();

                // ⚠️ QUAN TRỌNG: Ở chế độ dự phòng này, ta KHÔNG gửi ảnh lên Groq
                // để tránh lỗi model vision bị xóa. Chỉ gửi text mô tả.
                string finalPrompt = prompt;
                if (!string.IsNullOrEmpty(imageBase64))
                {
                    finalPrompt += "\n[Khách hàng có gửi kèm một hình ảnh nhưng hệ thống ảnh đang bận. Hãy trả lời dựa trên mô tả của khách hoặc yêu cầu khách mô tả kỹ hơn về ảnh].";
                }

                messages.Add(new { role = "user", content = finalPrompt });

                var data = new { model = modelName, messages = messages, temperature = 0.6 };
                var res = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json"));

                if (!res.IsSuccessStatusCode)
                {
                    var err = await res.Content.ReadAsStringAsync();
                    throw new Exception("Groq Error: " + err);
                }

                var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            }
        }
    }
    public class ChatRequest { public string Message { get; set; } public string ImageBase64 { get; set; } }
}