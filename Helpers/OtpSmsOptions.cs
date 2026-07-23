namespace EMISAPIS.Helpers
{
    /// <summary>
    /// OTP SMS gateway settings (legacy EMSRole SMSHttpPostClient / Web.config).
    /// Override via env: OtpSms__Username, OtpSms__Password, OtpSms__SecureKey, …
    /// </summary>
    public class OtpSmsOptions
    {
        public const string SectionName = "OtpSms";

        /// <summary>When false, OTP is stored in DB only (no gateway call).</summary>
        public bool Enabled { get; set; }

        public string GatewayUrl { get; set; } = "https://msdgweb.mgov.gov.in/esms/sendsmsrequestDLT";

        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string SenderId { get; set; } = "CGMSCL";

        public string SecureKey { get; set; } = string.Empty;

        /// <summary>Gateway smsservicetype — legacy OTP uses otpmsg.</summary>
        public string ServiceType { get; set; } = "otpmsg";

        public string PortalName { get; set; } = "EMIS";

        /// <summary>Message body; use {otp} and {portal} placeholders.</summary>
        public string MessageTemplate { get; set; } =
            "OTP for submission in {portal} is {otp}. Please do not share with anyone.";

        public OtpSmsTemplateIds Templates { get; set; } = new();
    }

    public class OtpSmsTemplateIds
    {
        /// <summary>LoginEmsSUP / supplier auth DLT template.</summary>
        public string SupplierAuth { get; set; } = "1407162263151118865";

        /// <summary>DMEFACADDIndent finalize DLT template.</summary>
        public string IndentFinalize { get; set; } = "1407163911599431374";
    }
}
