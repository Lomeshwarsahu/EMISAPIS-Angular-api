using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EMISAPIS.Middlewares
{
    /// <summary>
    /// Middleware that intercepts every incoming HTTP request and validates the JWT Bearer token.
    /// Attaches the validated ClaimsPrincipal and user details to HttpContext.User and HttpContext.Items.
    /// </summary>
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly ILogger<JwtMiddleware> _logger;

        public JwtMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<JwtMiddleware> logger)
        {
            _next = next;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(authHeader) &&
                authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                if (!string.IsNullOrEmpty(token))
                {
                    AttachUserToContext(context, token);
                }
            }

            await _next(context);
        }

        private void AttachUserToContext(HttpContext context, string token)
        {
            try
            {
                var jwtKey = _configuration["Jwt:Key"];
                if (string.IsNullOrWhiteSpace(jwtKey))
                {
                    _logger.LogWarning("Jwt:Key is not configured in application settings.");
                    return;
                }

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(jwtKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = !string.IsNullOrWhiteSpace(_configuration["Jwt:Issuer"]),
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidateAudience = !string.IsNullOrWhiteSpace(_configuration["Jwt:Audience"]),
                    ValidAudience = _configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                // Populate HttpContext.User
                context.User = principal;

                // Extract common claims for easy retrieval in controllers via HttpContext.Items
                var jwtToken = (JwtSecurityToken)validatedToken;
                var userId = jwtToken.Claims.FirstOrDefault(x =>
                    x.Type == ClaimTypes.NameIdentifier ||
                    x.Type == "nameid" ||
                    x.Type == "user_id" ||
                    x.Type == "UserId" ||
                    x.Type == "ConID")?.Value;

                var userName = jwtToken.Claims.FirstOrDefault(x =>
                    x.Type == ClaimTypes.Name ||
                    x.Type == "unique_name" ||
                    x.Type == "username")?.Value;

                var role = jwtToken.Claims.FirstOrDefault(x =>
                    x.Type == ClaimTypes.Role ||
                    x.Type == "role" ||
                    x.Type == "user_type")?.Value;

                if (!string.IsNullOrEmpty(userId))
                {
                    context.Items["UserId"] = userId;
                }
                if (!string.IsNullOrEmpty(userName))
                {
                    context.Items["UserName"] = userName;
                }
                if (!string.IsNullOrEmpty(role))
                {
                    context.Items["Role"] = role;
                }
                context.Items["UserClaims"] = jwtToken.Claims;
            }
            catch (SecurityTokenExpiredException)
            {
                _logger.LogInformation("JWT token has expired.");
                context.Response.Headers["Token-Expired"] = "true";
            }
            catch (Exception ex)
            {
                _logger.LogWarning("JWT token validation failed: {Message}", ex.Message);
            }
        }
    }
}
