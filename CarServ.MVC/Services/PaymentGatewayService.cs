using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace CarServ.MVC.Services
{
    public interface IPaymentGatewayService
    {
        string CreateVNPayPaymentUrl(int orderId, decimal amount, string orderInfo, string returnUrl, string ipAddress);
        bool VerifyVNPayCallback(Dictionary<string, string> vnpParams, string vnpSecureHash);
        string CreateMomoPaymentUrl(int orderId, decimal amount, string orderInfo, string returnUrl, string ipAddress);
        bool VerifyMomoCallback(Dictionary<string, string> momoParams);
    }

    public class PaymentGatewayService : IPaymentGatewayService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentGatewayService> _logger;

        public PaymentGatewayService(IConfiguration configuration, ILogger<PaymentGatewayService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        #region VNPay

        public string CreateVNPayPaymentUrl(int orderId, decimal amount, string orderInfo, string returnUrl, string ipAddress)
        {
            // BẮT BUỘC đọc từ IConfiguration, throw Exception nếu thiếu
            var vnpTmnCode = _configuration["PaymentGateway:VNPay:TmnCode"];
            var vnpHashSecret = _configuration["PaymentGateway:VNPay:HashSecret"];
            var vnpUrl = _configuration["PaymentGateway:VNPay:Url"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

            if (string.IsNullOrWhiteSpace(vnpTmnCode))
            {
                throw new InvalidOperationException("VNPay TmnCode is not configured. Please set PaymentGateway:VNPay:TmnCode in appsettings.json");
            }

            if (string.IsNullOrWhiteSpace(vnpHashSecret))
            {
                throw new InvalidOperationException("VNPay HashSecret is not configured. Please set PaymentGateway:VNPay:HashSecret in appsettings.json");
            }

            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                throw new ArgumentException("Return URL cannot be empty", nameof(returnUrl));
            }

            // Xử lý orderInfo: mặc định "Thanh toan don hang", max 255 ký tự
            var cleanOrderInfo = string.IsNullOrWhiteSpace(orderInfo) ? "Thanh toan don hang" : orderInfo;
            if (cleanOrderInfo.Length > 255)
            {
                cleanOrderInfo = cleanOrderInfo[..255];
            }

            // Xử lý ipAddress: nếu null/::1/127.0.0.1 thì gán "113.161.1.1"
            var cleanIpAddress = ipAddress;
            if (string.IsNullOrWhiteSpace(cleanIpAddress) || 
                cleanIpAddress == "::1" || 
                cleanIpAddress == "127.0.0.1")
            {
                cleanIpAddress = "113.161.1.1";
            }

            // Build parameters dictionary - chỉ các tham số bắt đầu bằng "vnp_"
            // KHÔNG bao gồm vnp_SecureHash và vnp_SecureHashType
            var vnpParams = new Dictionary<string, string>
            {
                { "vnp_Version", "2.1.0" },
                { "vnp_Command", "pay" },
                { "vnp_TmnCode", vnpTmnCode },
                { "vnp_Amount", (amount * 100).ToString("0") }, // KHÔNG cast long, dùng ToString("0")
                { "vnp_CurrCode", "VND" },
                { "vnp_TxnRef", orderId.ToString() },
                { "vnp_OrderInfo", cleanOrderInfo },
                { "vnp_OrderType", "other" },
                { "vnp_Locale", "vn" },
                { "vnp_ReturnUrl", returnUrl },
                { "vnp_IpAddr", cleanIpAddress },
                { "vnp_CreateDate", DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss") } // UTC+7 (Vietnam time)
            };

            _logger.LogInformation("VNPay params created for order {OrderId}: Amount={Amount}, ReturnUrl={ReturnUrl}", 
                orderId, vnpParams["vnp_Amount"], returnUrl);

            // Tạo chữ ký VNPay
            // Bước 1: Chỉ lấy các tham số bắt đầu bằng "vnp_"
            // Bước 2: Loại bỏ vnp_SecureHash và vnp_SecureHashType
            var paramsToSign = vnpParams
                .Where(p => p.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase))
                .Where(p => !p.Key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase))
                .Where(p => !p.Key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
                .Where(p => !string.IsNullOrEmpty(p.Value))
                .ToDictionary(p => p.Key, p => p.Value);

            // Bước 3: Sort tham số theo alphabet (A-Z)
            var sortedParams = paramsToSign.OrderBy(p => p.Key, StringComparer.Ordinal).ToList();

            // Bước 4: Tạo signData dạng key=value&key=value (KHÔNG URL encode)
            var signData = string.Join("&",
            sortedParams.Select(p =>
            $"{WebUtility.UrlEncode(p.Key)}={WebUtility.UrlEncode(p.Value)}"
            ));

            _logger.LogInformation("VNPay sign data created for order {OrderId}", orderId);

            // Bước 5: Dùng HMAC SHA512, output lowercase hex
            var vnpSecureHash = HmacSHA512(vnpHashSecret, signData);

            // Bảo mật: KHÔNG log full SecureHash, chỉ log một phần
            _logger.LogInformation("VNPay secure hash created for order {OrderId} (length: {Length})", 
                orderId, vnpSecureHash.Length);

            // Build URL redirect
            // Giá trị param phải URL encode
            var queryString = string.Join("&",
            sortedParams.Select(p => $"{p.Key}={HttpUtility.UrlEncode(p.Value)}")
);
            queryString += "&vnp_SecureHashType=SHA512";
            // vnp_SecureHash KHÔNG encode lại
            queryString += $"&vnp_SecureHash={vnpSecureHash}";

            var finalUrl = $"{vnpUrl}?{queryString}";
            _logger.LogInformation("VNPay payment URL created for order {OrderId}", orderId);

            return finalUrl;
        }

        public bool VerifyVNPayCallback(Dictionary<string, string> vnpParams, string vnpSecureHash)
        {
            try
            {
                // BẮT BUỘC đọc từ IConfiguration, throw Exception nếu thiếu
                var vnpHashSecret = _configuration["PaymentGateway:VNPay:HashSecret"];

                if (string.IsNullOrWhiteSpace(vnpHashSecret))
                {
                    throw new InvalidOperationException("VNPay HashSecret is not configured. Please set PaymentGateway:VNPay:HashSecret in appsettings.json");
                }

                // Tạo bản sao để không modify original
                var paramsToSign = new Dictionary<string, string>(vnpParams);

                // Loại bỏ vnp_SecureHash và vnp_SecureHashType
                paramsToSign.Remove("vnp_SecureHash");
                paramsToSign.Remove("vnp_SecureHashType");

                // Chỉ lấy các tham số bắt đầu bằng "vnp_"
                var filteredParams = paramsToSign
                    .Where(p => p.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase))
                    .Where(p => !string.IsNullOrEmpty(p.Value))
                    .ToDictionary(p => p.Key, p => p.Value);

                // Sort tham số theo alphabet (A-Z)
                var sortedParams = filteredParams.OrderBy(p => p.Key, StringComparer.Ordinal).ToList();

                var signData = string.Join("&",
                sortedParams.Select(p =>$"{WebUtility.UrlEncode(p.Key)}={WebUtility.UrlEncode(p.Value)}"));

                // Dùng HMAC SHA512, output lowercase hex
                var calculatedHash = HmacSHA512(vnpHashSecret, signData);

                // So sánh hash (case-insensitive)
                return calculatedHash.Equals(vnpSecureHash, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying VNPay callback");
                return false;
            }
        }

        #endregion

        #region Momo

        public string CreateMomoPaymentUrl(int orderId, decimal amount, string orderInfo, string returnUrl, string ipAddress)
        {
            try
            {
                var partnerCode = _configuration["PaymentGateway:Momo:PartnerCode"] ?? "";
                var accessKey = _configuration["PaymentGateway:Momo:AccessKey"] ?? "";
                var secretKey = _configuration["PaymentGateway:Momo:SecretKey"] ?? "";
                var momoApiUrl = _configuration["PaymentGateway:Momo:ApiUrl"] ?? "https://test-payment.momo.vn/v2/gateway/api/create";

                if (string.IsNullOrEmpty(partnerCode) || string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
                {
                    _logger.LogWarning("Momo configuration is missing. Payment URL cannot be created.");
                    throw new Exception("Momo payment gateway is not configured.");
                }

                var requestId = Guid.NewGuid().ToString();
                var orderIdStr = orderId.ToString();
                var orderGroupId = "";
                var autoCapture = true;
                var lang = "vi";
                var requestType = "captureWallet";

                var rawHash = $"accessKey={accessKey}&amount={(long)(amount * 100)}&extraData=&ipnUrl={returnUrl}&orderId={orderIdStr}&orderInfo={orderInfo}&partnerCode={partnerCode}&redirectUrl={returnUrl}&requestId={requestId}&requestType={requestType}";
                var signature = HmacSHA256(secretKey, rawHash);

                var requestData = new
                {
                    partnerCode = partnerCode,
                    partnerName = "CarServ",
                    storeId = "CarServ",
                    requestId = requestId,
                    amount = (long)(amount * 100),
                    orderId = orderIdStr,
                    orderInfo = orderInfo,
                    redirectUrl = returnUrl,
                    ipnUrl = returnUrl,
                    lang = lang,
                    extraData = "",
                    requestType = requestType,
                    autoCapture = autoCapture,
                    orderGroupId = orderGroupId,
                    signature = signature
                };

                // In a real implementation, you would make an HTTP POST request to Momo API
                // For now, return a placeholder URL
                _logger.LogInformation("Momo payment URL created for order {OrderId}", orderId);
                
                // Note: Momo requires server-to-server API call to get payment URL
                // This is a simplified version. You need to implement HTTP POST to Momo API
                return $"/payment/momo/process?orderId={orderId}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Momo payment URL");
                throw;
            }
        }

        public bool VerifyMomoCallback(Dictionary<string, string> momoParams)
        {
            try
            {
                var secretKey = _configuration["PaymentGateway:Momo:SecretKey"] ?? "";

                if (string.IsNullOrEmpty(secretKey))
                {
                    _logger.LogWarning("Momo SecretKey is missing.");
                    return false;
                }

                // Momo callback verification logic
                // This is a simplified version - actual implementation depends on Momo's API
                return momoParams.ContainsKey("resultCode") && momoParams["resultCode"] == "0";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying Momo callback");
                return false;
            }
        }

        #endregion

        #region Helper Methods

        private string HmacSHA512(string key, string inputData)
        {
            try
            {
                // UTF-8 encoding cho cả key và input data
                byte[] keyBytes = Encoding.UTF8.GetBytes(key);
                byte[] inputBytes = Encoding.UTF8.GetBytes(inputData);
                
                using (var hmac = new HMACSHA512(keyBytes))
                {
                    byte[] hashValue = hmac.ComputeHash(inputBytes);
                    // Convert to lowercase hexadecimal string
                    var hash = new StringBuilder();
                    foreach (var theByte in hashValue)
                    {
                        hash.Append(theByte.ToString("x2"));
                    }
                    return hash.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error computing HMAC SHA512 hash");
                throw;
            }
        }

        private string HmacSHA256(string key, string inputData)
        {
            var hash = new StringBuilder();
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] inputBytes = Encoding.UTF8.GetBytes(inputData);
            using (var hmac = new HMACSHA256(keyBytes))
            {
                byte[] hashValue = hmac.ComputeHash(inputBytes);
                foreach (var theByte in hashValue)
                {
                    hash.Append(theByte.ToString("x2"));
                }
            }
            return hash.ToString();
        }

        #endregion
    }
}
