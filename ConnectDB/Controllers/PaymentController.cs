using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ConnectDB.Model;

namespace ConnectDB.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public PaymentController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost("create-vnpay-url")]
        public IActionResult CreateVnPayUrl([FromBody] PaymentRequestModel model)
        {
            // 1. Lấy thông tin cấu hình
            string vnp_TmnCode = _configuration["VnPay:TmnCode"];
            string vnp_HashSecret = _configuration["VnPay:HashSecret"];
            string vnp_Url = _configuration["VnPay:BaseUrl"];
            string vnp_Returnurl = _configuration["VnPay:ReturnUrl"];

            // 2. QUAN TRỌNG: Lấy giờ Việt Nam (UTC+7) để tránh lỗi Timeout trên Somee
            DateTime timeNow = DateTime.UtcNow.AddHours(7);
            string vnp_CreateDate = timeNow.ToString("yyyyMMddHHmmss");
            string vnp_ExpireDate = timeNow.AddMinutes(15).ToString("yyyyMMddHHmmss");

            // Tạo mã đơn hàng duy nhất dựa trên Ticks
            string orderId = timeNow.Ticks.ToString();

            // 3. Khởi tạo danh sách tham số (SortedList tự động sắp xếp A-Z theo key)
            var vnpayData = new SortedList<string, string>(new VnPayCompare())
            {
                { "vnp_Version", "2.1.0" },
                { "vnp_Command", "pay" },
                { "vnp_TmnCode", vnp_TmnCode },
                { "vnp_Amount", (model.Amount * 100).ToString() }, // Số tiền nhân 100 theo quy định VNPAY
                { "vnp_CreateDate", vnp_CreateDate },
                { "vnp_CurrCode", "VND" },
                { "vnp_IpAddr", GetIpAddress() },
                { "vnp_Locale", "vn" },
                { "vnp_OrderInfo", "Thanh toan don hang: " + orderId }, // Không nên để dấu tiếng Việt ở đây
                { "vnp_OrderType", "other" },
                { "vnp_ReturnUrl", vnp_Returnurl },
                { "vnp_ExpireDate", vnp_ExpireDate },
                { "vnp_TxnRef", orderId }
            };

            // 4. Xây dựng chuỗi dữ liệu để băm Hash và chuỗi Query link
            StringBuilder data = new StringBuilder();
            StringBuilder query = new StringBuilder();

            foreach (KeyValuePair<string, string> kv in vnpayData)
            {
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    // VNPAY yêu cầu UrlEncode các tham số
                    data.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
                    query.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
                }
            }

            string queryString = query.ToString();
            string signData = data.ToString();

            // Xóa ký tự '&' cuối cùng
            if (queryString.Length > 0)
            {
                queryString = queryString.Remove(queryString.Length - 1, 1);
                signData = signData.Remove(signData.Length - 1, 1);
            }

            // 5. Tạo chữ ký bảo mật SecureHash (HMACSHA512)
            string vnp_SecureHash = HmacSHA512(vnp_HashSecret, signData);

            // 6. Tạo URL thanh toán hoàn chỉnh
            string paymentUrl = vnp_Url + "?" + queryString + "&vnp_SecureHash=" + vnp_SecureHash;

            return Ok(new { paymentUrl = paymentUrl });
        }

        // --- HÀM TIỆN ÍCH ---

        private string GetIpAddress()
        {
            // Lấy IP người dùng, nếu chạy local thì mặc định là 127.0.0.1
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(ipAddress) || ipAddress == "::1")
            {
                ipAddress = "127.0.0.1";
            }
            return ipAddress;
        }

        private string HmacSHA512(string key, string inputData)
        {
            var hash = new StringBuilder();
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] inputBytes = Encoding.UTF8.GetBytes(inputData);
            using (var hmac = new HMACSHA512(keyBytes))
            {
                byte[] hashValue = hmac.ComputeHash(inputBytes);
                foreach (var theByte in hashValue)
                {
                    hash.Append(theByte.ToString("x2"));
                }
            }
            return hash.ToString();
        }
    }

    public class VnPayCompare : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == y) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            return string.Compare(x, y, StringComparison.Ordinal);
        }
    }
}