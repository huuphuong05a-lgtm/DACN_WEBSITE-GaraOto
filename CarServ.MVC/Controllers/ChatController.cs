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
using System.Collections.Concurrent;
using CarServ.MVC.Services;

namespace CarServ.MVC.Controllers
{
    public class ChatController : Controller
    {
        private readonly CarServContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ChatController> _logger;
        private readonly IAppointmentAvailabilityService _availabilityService;
        private static readonly ConcurrentDictionary<string, Queue<DateTime>> RequestLog = new();
        private const int MaxImageBase64Length = 2_800_000;
        private const int MaxRequestsPerMinute = 12;

        public ChatController(CarServContext context, IConfiguration configuration, ILogger<ChatController> logger, IAppointmentAvailabilityService availabilityService)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _availabilityService = availabilityService;
        }

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

        [HttpGet]
        public IActionResult GetHistory()
        {
            var history = GetSessionHistory();
            return Json(history.Select(x => new { message = x.Message, isBot = x.IsBot, createdAt = x.CreatedAt }));
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequest req)
        {
            if (req == null)
            {
                return Json(new { reply = "Dạ tin nhắn chưa hợp lệ, mình thử gửi lại giúp em nhé.", model = "System" });
            }

            string userId = GetChatUserId();
            if (!AllowRequest(userId))
            {
                return Json(new { reply = "Dạ mình gửi hơi nhanh rồi ạ. Vui lòng chờ một chút rồi nhắn tiếp giúp em nhé.", model = "Rate limit" });
            }

            string msg = req.Message?.Trim() ?? "";
            if (msg.Length > 2000)
            {
                return Json(new { reply = "Dạ tin nhắn hơi dài. Mình rút gọn nội dung chính rồi gửi lại giúp em nhé.", model = "System" });
            }

            if (!string.IsNullOrEmpty(req.ImageBase64) && req.ImageBase64.Length > MaxImageBase64Length)
            {
                return Json(new { reply = "Dạ ảnh đang quá lớn. Mình gửi ảnh dưới 2MB hoặc mô tả triệu chứng xe bằng chữ giúp em nhé.", model = "System" });
            }

            string cleanMsg = Normalize(msg);

            // Local helper function to save to DB and Session
            void SaveMsg(string msgText, bool isBot)
            {
                SaveMsgToDb(userId, msgText, isBot);
                var sessionHistory = GetSessionHistory();
                sessionHistory.Add(new ChatMessageDto
                {
                    Message = msgText,
                    IsBot = isBot,
                    CreatedAt = DateTime.Now
                });
                if (sessionHistory.Count > 15)
                {
                    sessionHistory = sessionHistory.Skip(sessionHistory.Count - 15).ToList();
                }
                SaveSessionHistory(sessionHistory);
            }

            // --- 1. ƯU TIÊN TỪ KHÓA (CHẶN ĐỨNG AI NẾU GẶP CÁC TỪ NÀY) ---
            // Đặt cái này lên đầu tiên để AI không có cơ hội nói leo
            // =============================================================
            // ⭐ KEYWORD TRIGGER – xử lý trước AI, tránh nhầm, chia nhóm rõ ràng
            // =============================================================
            if (string.IsNullOrEmpty(req.ImageBase64))
            {
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
                    SaveMsg(msg, false);
                    SaveMsg(reply, true);

                    return Json(new { reply, model = "👨‍💼 Staff Support" });
                }



                // =====================================
                // 🟩 GROUP 2 — THÔNG TIN GARA
                // =====================================
                var infoKeywords = new Dictionary<string, string>
    {
        { "dia chi gara", "Gara NHP-AUTO: 123 Đường ABC, Quận 1, TP.HCM." },
        { "gara o dau", "Gara NHP-AUTO: 123 Đường ABC, Quận 1, TP.HCM." },
        { "tim gara", "Gara NHP-AUTO: 123 Đường ABC, Quận 1, TP.HCM." },
        { "duong di gara", "Gara NHP-AUTO: 123 Đường ABC, Quận 1, TP.HCM." },

        { "hotline gara", "Hotline hỗ trợ: 1900 1000." },
        { "cho xin hotline", "Hotline hỗ trợ: 1900 1000." },
        { "so hotline gara", "Hotline: 1900 1000." },

        { "gio lam", "NHP-AUTO làm việc: T2–T6 (07:00–21:00), T7–CN (08:00–18:00)." },
        { "mo cua", "NHP-AUTO mở cửa từ 07:00 đến 21:00 (Thứ 2–6)." }
    };

                foreach (var pair in infoKeywords)
                {
                    if (cleanMsg.Contains(pair.Key))
                    {
                        SaveMsg(msg, false);
                        SaveMsg(pair.Value, true);

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

                    SaveMsg(msg, false);
                    SaveMsg(reply, true);

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
                        "Dạ anh/chị có thể đặt lịch trực tiếp tại trang Đặt Lịch trên website hoặc liên hệ hotline để được hỗ trợ nhanh nhất ạ.";

                    SaveMsg(msg, false);
                    SaveMsg(reply, true);

                    return Json(new { reply, model = "📅 Booking Guide" });
                }
            }



            // Lưu tin nhắn khách vào DB và Session
            SaveMsg(msg, false);

            // Lấy lịch sử từ Session
            List<string> historyList = GetSessionHistory()
                .Select(x => (x.IsBot ? "Bot: " : "Khách: ") + x.Message)
                .ToList();

            // --- 3. KIỂM TRA LỊCH TRỐNG (NẾU CÓ YÊU CẦU) ---
            string availabilityInfo = "";
            string[] availabilityKeywords = {
                "lich trong", "con trong", "con slot", "slot nao", "gio nao ranh", "con gio nao",
                "con ktv", "ktv nao ranh", "ky thuat vien nao ranh", "co lich khong", "dat lich vao",
                "co slot", "co gio", "co lich", "trong lich", "slot trong", "gio trong", "ktv trong", "con lich"
            };

            int? queriedHour = ParseSlotHour(cleanMsg);
            DateTime? queriedDate = ParseDateFromMessage(cleanMsg);

            if (queriedDate.HasValue && queriedDate.Value >= DateTime.Today)
            {
                // Store active date in session only if it's today or in the future
                HttpContext.Session.SetString("ChatActiveDate", queriedDate.Value.ToString("yyyy-MM-dd"));
            }

            if (availabilityKeywords.Any(k => cleanMsg.Contains(Normalize(k))) || queriedHour.HasValue || queriedDate.HasValue)
            {
                // Load active date from session or default to today
                string? storedDateStr = HttpContext.Session.GetString("ChatActiveDate");
                DateTime activeDate = DateTime.Today;
                if (queriedDate.HasValue)
                {
                    activeDate = queriedDate.Value;
                }
                else if (!string.IsNullOrEmpty(storedDateStr) && DateTime.TryParse(storedDateStr, out var parsedStoredDate))
                {
                    activeDate = parsedStoredDate;
                }

                // Chặn và báo lỗi nếu ngày truy vấn ở quá khứ
                if (activeDate < DateTime.Today)
                {
                    string reply = $"Dạ, ngày {activeDate:dd/MM/yyyy} đã qua rồi nên không thể tra cứu lịch trống hoặc đặt lịch hẹn được nữa ạ. Anh/chị vui lòng chọn ngày hôm nay ({DateTime.Today:dd/MM/yyyy}) hoặc các ngày sắp tới nhé!";
                    SaveMsg(reply, true);
                    return Json(new { reply, model = "📅 Past Date Interceptor" });
                }

                if (queriedHour.HasValue)
                {
                    // Step 2: Available Technicians for a slot
                    try
                    {
                        var freeTechs = await _availabilityService.GetAvailableTechniciansAsync(activeDate, queriedHour.Value);
                        var slotLabel = $"{queriedHour.Value:D2}:00 - {(queriedHour.Value + 1):D2}:00";
                        availabilityInfo = $"CONTEXT: Khách hàng đang hỏi về các kỹ thuật viên trống trong khung giờ {slotLabel} của ngày {activeDate:dd/MM/yyyy}.\n" +
                                           $"DANH SÁCH KỸ THUẬT VIÊN CÒN TRỐNG:\n";
                        if (freeTechs.Any())
                        {
                            foreach (var tech in freeTechs)
                            {
                                availabilityInfo += $"- {tech}\n";
                            }
                            availabilityInfo += "\nHãy liệt kê các kỹ thuật viên này cho khách hàng và hỏi xem họ có muốn đặt lịch với kỹ thuật viên nào không.";
                        }
                        else
                        {
                            availabilityInfo += "Không có kỹ thuật viên nào còn trống.\nHãy báo cho khách rằng khung giờ này đã hết kỹ thuật viên khả dụng.";
                        }
                    }
                    catch (Exception exAvail)
                    {
                        _logger.LogError(exAvail, "Error fetching available technicians.");
                    }
                }
                else
                {
                    // Step 1: Available Time Slots
                    try
                    {
                        var freeSlots = await _availabilityService.GetAvailableTimeSlotsAsync(activeDate);
                        availabilityInfo = $"CONTEXT: Khách hàng đang hỏi về các khung giờ trống của ngày {activeDate:dd/MM/yyyy}.\n" +
                                           $"DANH SÁCH KHUNG GIỜ CÒN TRỐNG:\n";
                        if (freeSlots.Any())
                        {
                            foreach (var slot in freeSlots)
                            {
                                availabilityInfo += $"- {slot}\n";
                            }
                            availabilityInfo += "\n⚠️ QUY TẮC PHẢN HỒI QUAN TRỌNG: Tuyệt đối KHÔNG ĐƯỢC liệt kê bất kỳ tên kỹ thuật viên nào ở bước này. Chỉ hiển thị các khung giờ trống trên và hỏi khách hàng muốn xem chi tiết kỹ thuật viên của khung giờ nào.";
                        }
                        else
                        {
                            availabilityInfo += "Đã hết lịch trống hoặc gara không làm việc.\nHãy thông báo ngày này đã kín lịch và gợi ý khách kiểm tra ngày khác.";
                        }
                    }
                    catch (Exception exAvail)
                    {
                        _logger.LogError(exAvail, "Error fetching available time slots.");
                    }
                }
            }

            // --- 3B. TRUY VẤN DỮ LIỆU DỊCH VỤ THỰC TẾ ---
            string servicesInfo = "";
            string[] serviceKeywords = {
                "dich vu", "bao duong", "thay dau", "loc dau", "thay loc", "phanh", "dieu hoa",
                "sua chua", "gia", "chi phi", "bao nhieu", "tien", "khac phuc", "bi keo", "bi rung",
                "hao xang", "nong may", "gia ca"
            };

            if (serviceKeywords.Any(k => cleanMsg.Contains(Normalize(k))))
            {
                try
                {
                    var activeServices = await _context.Services
                        .Where(s => s.IsActive)
                        .OrderBy(s => s.SortOrder)
                        .Select(s => new { s.ServiceName, s.Price, s.ShortDescription, s.ServiceCategory })
                        .ToListAsync();

                    if (activeServices.Any())
                    {
                        servicesInfo = "🚨 DANH SÁCH DỊCH VỤ & BẢNG GIÁ THỰC TẾ TẠI GARA (HÃY DÙNG THÔNG TIN NÀY ĐỂ TRẢ LỜI KHÁCH HÀNG):\n";
                        foreach (var s in activeServices)
                        {
                            var priceStr = s.Price.HasValue ? s.Price.Value.ToString("N0") + " VNĐ" : "Liên hệ báo giá";
                            servicesInfo += $"- Tên dịch vụ: {s.ServiceName}\n" +
                                            $"  Phân loại: {s.ServiceCategory}\n" +
                                            $"  Giá dịch vụ: {priceStr}\n" +
                                            $"  Mô tả: {s.ShortDescription ?? s.ServiceName}\n\n";
                        }
                    }
                }
                catch (Exception exService)
                {
                    _logger.LogError(exService, "Error fetching active services from database.");
                }
            }

            // --- 4. GỌI AI ---
            string finalReply = "";
            string usedModel = "";

            try
            {
                // Gọi Gemini 1.5 Flash (Ưu tiên 1)
                string fullPrompt = CreateSlotFillingPrompt(msg, historyList, availabilityInfo, servicesInfo);
                finalReply = await CallGemini(fullPrompt, req.ImageBase64);
                usedModel = "✨ Gemini 1.5 Flash";
            }
            catch (Exception exGemini)
            {
                // Nếu Gemini lỗi -> Gọi Groq (Ưu tiên 2)
                try
                {
                    string fullPrompt = CreateSlotFillingPrompt(msg, historyList, availabilityInfo, servicesInfo);
                    finalReply = await CallGroq(fullPrompt, req.ImageBase64);
                    usedModel = string.IsNullOrEmpty(req.ImageBase64) ? "⚡ Groq Llama 3" : "👁️ Groq Vision";
                }
                catch (Exception exGroq)
                {
                    _logger.LogError(exGroq, "Chat AI failed after Gemini fallback failed: {GeminiError}", exGemini.Message);
                    return Json(new { reply = "Dạ hệ thống tư vấn đang bận. Mình thử lại sau ít phút hoặc để lại số điện thoại để nhân viên hỗ trợ nhé.", model = "System" });
                }
            }

            // Lưu tin nhắn Bot
            SaveMsg(finalReply, true);

            return Json(new { reply = finalReply, model = usedModel });
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableSlots(string date)
        {
            DateTime parsedDate;
            if (!DateTime.TryParseExact(date, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out parsedDate))
            {
                if (!DateTime.TryParse(date, out parsedDate))
                {
                    parsedDate = DateTime.Today;
                }
            }

            var availability = await _availabilityService.GetAvailableSlotsAsync(parsedDate);
            return Json(new
            {
                Date = availability.DateLabel,
                AvailableSlots = availability.AvailableSlots.Select(s => new
                {
                    TimeSlot = s.TimeSlot,
                    TechnicianId = s.TechnicianId,
                    TechnicianName = s.TechnicianName
                })
            });
        }

        private int? ParseSlotHour(string cleanMsg)
        {
            string[] standardHours = { "08", "09", "10", "11", "13", "14", "15", "16", "17" };
            foreach (var hr in standardHours)
            {
                if (cleanMsg.Contains(hr + ":00") || cleanMsg.Contains(hr + "h00") || cleanMsg.Contains("khung " + hr) || cleanMsg.Contains("gio " + hr))
                {
                    return int.Parse(hr);
                }
            }

            int[] hours = { 8, 9, 10, 11, 13, 14, 15, 16, 17 };
            foreach (var h in hours)
            {
                if (cleanMsg.Contains($"{h}h") || cleanMsg.Contains($"{h} gio") || cleanMsg.Contains($"khung {h}") || cleanMsg.Contains($"khung gio {h}"))
                {
                    return h;
                }
            }

            var tokens = cleanMsg.Split(new[] { ' ', ':', 'h', '-' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                if (int.TryParse(token, out int parsedHour))
                {
                    if (hours.Contains(parsedHour))
                    {
                        return parsedHour;
                    }
                }
            }

            return null;
        }

        private DateTime? ParseDateFromMessage(string cleanMsg)
        {
            var today = DateTime.Today;

            if (cleanMsg.Contains("hom nay") || cleanMsg.Contains("ngay nay"))
                return today;
            if (cleanMsg.Contains("ngay mai") || cleanMsg.Contains("chieu mai") || cleanMsg.Contains("sang mai"))
                return today.AddDays(1);
            if (cleanMsg.Contains("ngay kia") || cleanMsg.Contains("ngay mot"))
                return today.AddDays(2);
            if (cleanMsg.Contains("hom qua"))
                return today.AddDays(-1);

            var words = cleanMsg.Split(new[] { ' ', ',', '.', '-' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                if (word.Contains("/"))
                {
                    var parts = word.Split('/');
                    if (parts.Length >= 2 && int.TryParse(parts[0], out int day) && int.TryParse(parts[1], out int month))
                    {
                        int year = today.Year;
                        if (parts.Length == 3 && int.TryParse(parts[2], out int parsedYear))
                        {
                            year = parsedYear;
                            if (year < 100) year += 2000;
                        }
                        try
                        {
                            return new DateTime(year, month, day);
                        }
                        catch { }
                    }
                }
            }

            var regex = new System.Text.RegularExpressions.Regex(@"ngay\s+(\d+)(?:\s*thang\s*(\d+))?");
            var match = regex.Match(cleanMsg);
            if (match.Success)
            {
                if (int.TryParse(match.Groups[1].Value, out int day))
                {
                    int month = today.Month;
                    if (match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out int m))
                    {
                        month = m;
                    }
                    try
                    {
                        int year = today.Year;
                        return new DateTime(year, month, day);
                    }
                    catch { }
                }
            }

            return null;
        }

        // --- PROMPT MỚI: XỬ LÝ ĐỔI CHỦ ĐỀ ---
        private string CreateSlotFillingPrompt(string userMsg, List<string> history, string availabilityInfo = "", string servicesInfo = "")
        {

            string hist = string.Join("\n", history);
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

            string availabilitySection = "";
            if (!string.IsNullOrEmpty(availabilityInfo))
            {
                availabilitySection = $@"
                =============================
                🚨 THÔNG TIN LỊCH TRỐNG THỰC TẾ (QUAN TRỌNG NHẤT):
                {availabilityInfo}
                
                Hãy dùng thông tin trên để trả lời trực tiếp câu hỏi của khách hàng về thời gian trống/kỹ thuật viên trống. 
                - Nếu khách hỏi ngày cụ thể nằm trong danh sách trên, hãy trả lời chính xác các khung giờ còn trống và tên kỹ thuật viên rảnh của ngày đó.
                - Nếu khách hỏi ngày không nằm trong khoảng thời gian trên (sau 7 ngày tới), hãy lịch sự đề nghị khách chọn một ngày gần hơn hoặc liên hệ hotline để kiểm tra thêm.
                - Tuyệt đối KHÔNG ĐƯỢC bịa ra lịch trống không có trong danh sách trên.
                ";
            }

            string servicesSection = "";
            if (!string.IsNullOrEmpty(servicesInfo))
            {
                servicesSection = $@"
                =============================
                {servicesInfo}
                
                🚨 QUY TẮC QUAN TRỌNG VỀ GIÁ & DỊCH VỤ:
                - Phải trả lời chính xác tên dịch vụ và giá cả tương ứng từ danh sách thực tế trên.
                - Tuyệt đối không tự ý bịa ra giá hoặc tên dịch vụ không có trong cơ sở dữ liệu trên.
                - Nếu khách hỏi một dịch vụ không có trong danh sách trên, hãy trả lời rằng hiện tại gara chưa có thông tin chi tiết dịch vụ này trên hệ thống và khuyên khách hàng liên hệ hotline để được tư vấn thêm.
                ";
            }

            return $@"
                Bạn là Trợ lý Dịch vụ Cao cấp của Gara NHP-AUTO – phong cách chuyên nghiệp, ngắn gọn, dễ hiểu và thân thiện.
                {availabilitySection}
                {servicesSection}
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
                📌 QUY TẮC ĐẶT LỊCH
                - Chatbot chỉ có vai trò tư vấn.
                - Chatbot không được tạo lịch hẹn.
                - Chatbot không được tạo bất kỳ lệnh hệ thống nào.
                - Nếu khách muốn đặt lịch, hãy hướng dẫn khách truy cập trang Đặt Lịch của website hoặc liên hệ hotline.
                - Không được tự động thu thập thông tin để tạo Appointment.

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
                🎯 HÃY TRẢ LỜI VỚI VAI TRÒ TRỢ LÝ DỊCH VỤ NHP-AUTO.
                Trả lời tự nhiên theo đúng ngữ cảnh.
                ";
        }

        private List<ChatMessageDto> GetSessionHistory()
        {
            var historyJson = HttpContext.Session.GetString("SessionChatHistory");
            if (string.IsNullOrEmpty(historyJson))
            {
                return new List<ChatMessageDto>();
            }
            try
            {
                return JsonSerializer.Deserialize<List<ChatMessageDto>>(historyJson) ?? new List<ChatMessageDto>();
            }
            catch
            {
                return new List<ChatMessageDto>();
            }
        }

        private void SaveSessionHistory(List<ChatMessageDto> history)
        {
            var historyJson = JsonSerializer.Serialize(history);
            HttpContext.Session.SetString("SessionChatHistory", historyJson);
        }

        private string GetChatUserId()
        {
            var userId = User.Identity?.IsAuthenticated == true ? User.Identity.Name : HttpContext.Session.Id;
            return string.IsNullOrWhiteSpace(userId) ? "Guest" : userId;
        }

        private bool AllowRequest(string userId)
        {
            var now = DateTime.UtcNow;
            var queue = RequestLog.GetOrAdd(userId, _ => new Queue<DateTime>());

            lock (queue)
            {
                while (queue.Count > 0 && (now - queue.Peek()).TotalMinutes >= 1)
                {
                    queue.Dequeue();
                }

                if (queue.Count >= MaxRequestsPerMinute)
                {
                    return false;
                }

                queue.Enqueue(now);
                return true;
            }
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



        // (Giữ nguyên CallGemini và CallGroq - Nhớ điền Key của bạn)
        private async Task<string> CallGemini(string prompt, string? imageBase64)
        {
            string apiKey = _configuration["AI:Gemini:ApiKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Gemini API key is not configured.");
            }

            // 👇 SỬA QUAN TRỌNG: Dùng tên chuẩn "gemini-1.5-flash" (Bỏ chữ -latest đi)
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

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
                    .GetProperty("text").GetString() ?? "Dạ em chưa có phản hồi phù hợp, mình mô tả rõ hơn giúp em nhé.";
            }
        }

        // --- 2. SỬA LẠI HÀM GROQ (Dùng Model Vision 90b mới nhất) ---
        private async Task<string> CallGroq(string prompt, string? imageBase64)
        {
            string apiKey = _configuration["AI:Groq:ApiKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Groq API key is not configured.");
            }

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
                return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
                    ?? "Dạ em chưa có phản hồi phù hợp, mình mô tả rõ hơn giúp em nhé.";
            }
        }
    }
    public class ChatRequest
    {
        public string? Message { get; set; }
        public string? ImageBase64 { get; set; }
    }

    public class ChatMessageDto
    {
        public string Message { get; set; } = string.Empty;
        public bool IsBot { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
