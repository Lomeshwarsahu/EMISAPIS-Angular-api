using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace EMISAPIS.Helpers
{
    /// <summary>
    /// Sends OTP SMS via mGov DLT gateway (ported from EMSRole SMSHttpPostClient.sendOTPMSG).
    /// Credentials come from <see cref="OtpSmsOptions"/> / appsettings / env.
    /// </summary>
    public class OtpSmsService
    {
        private readonly OtpSmsOptions _options;
        private readonly ILogger<OtpSmsService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public OtpSmsService(
            IOptions<OtpSmsOptions> options,
            ILogger<OtpSmsService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _options = options.Value;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public OtpSmsOptions Options => _options;

        public string BuildMessage(string otp) =>
            (_options.MessageTemplate ?? string.Empty)
                .Replace("{otp}", otp ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{portal}", _options.PortalName ?? "EMIS", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Sends OTP SMS when Enabled and credentials are configured.
        /// Returns true if gateway accepted the request (or SMS disabled — DB-only mode).
        /// </summary>
        public async Task<(bool Sent, string Detail)> TrySendOtpAsync(
            string mobile,
            string otp,
            string templateId,
            CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
                return (true, "OTP SMS gateway disabled — OTP stored in database only.");

            if (string.IsNullOrWhiteSpace(mobile) || mobile.Trim() == "0")
                return (false, "Mobile number is missing.");

            if (string.IsNullOrWhiteSpace(_options.Username)
                || string.IsNullOrWhiteSpace(_options.Password)
                || string.IsNullOrWhiteSpace(_options.SecureKey)
                || string.IsNullOrWhiteSpace(_options.SenderId))
            {
                return (false, "OtpSms credentials are incomplete in configuration.");
            }

            if (string.IsNullOrWhiteSpace(templateId))
                return (false, "OTP SMS template id is missing.");

            var message = BuildMessage(otp);
            try
            {
                var responseBody = await PostOtpMessageAsync(
                    mobile.Trim(),
                    message,
                    templateId.Trim(),
                    cancellationToken);
                _logger.LogInformation("OTP SMS gateway response for {MobileMask}: {Response}",
                    MaskMobile(mobile),
                    Truncate(responseBody, 200));
                return (true, responseBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OTP SMS gateway failed for {MobileMask}", MaskMobile(mobile));
                return (false, ex.Message);
            }
        }

        private async Task<string> PostOtpMessageAsync(
            string mobile,
            string message,
            string templateId,
            CancellationToken cancellationToken)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var encryptedPassword = EncryptPasswordSha1(_options.Password);
            var key = HashGenerator(
                _options.Username.Trim(),
                _options.SenderId.Trim(),
                message.Trim(),
                _options.SecureKey.Trim());

            var form = new Dictionary<string, string>
            {
                ["username"] = _options.Username.Trim(),
                ["password"] = encryptedPassword,
                ["smsservicetype"] = string.IsNullOrWhiteSpace(_options.ServiceType) ? "otpmsg" : _options.ServiceType.Trim(),
                ["content"] = message.Trim(),
                ["mobileno"] = mobile,
                ["senderid"] = _options.SenderId.Trim(),
                ["key"] = key,
                ["templateid"] = templateId,
            };

            var client = _httpClientFactory.CreateClient(nameof(OtpSmsService));
            using var content = new FormUrlEncodedContent(form);
            using var response = await client.PostAsync(_options.GatewayUrl, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"SMS gateway HTTP {(int)response.StatusCode}: {Truncate(body, 300)}");
            return body;
        }

        private static string EncryptPasswordSha1(string password)
        {
            var encPwd = Encoding.UTF8.GetBytes(password);
            var hash = SHA1.HashData(encPwd);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static string HashGenerator(string username, string senderId, string message, string secureKey)
        {
            var raw = Encoding.UTF8.GetBytes(username + senderId + message + secureKey);
            var hash = SHA512.HashData(raw);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static string MaskMobile(string mobile)
        {
            var m = mobile?.Trim() ?? string.Empty;
            return m.Length >= 4 ? "xxxxxx" + m[^4..] : m;
        }

        private static string Truncate(string value, int max) =>
            string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max] + "…";
    }
}
