using EMISAPIS.Helpers;
using EMISAPIS.Middlewares;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// 1) Load `.env` into process environment
EnvFileLoader.Load();
// CreateBuilder already snapshotted env vars; re-add them so plain `.env` keys
// (e.g. FileStorage__LegacyRoot) reach IConfiguration without a `${...}` placeholder.
builder.Configuration.AddEnvironmentVariables();

// 2) Expand `${ENV_VAR}` placeholders in appsettings*.json from environment / .env
EnvPlaceholderResolver.Expand(builder.Configuration);

Console.WriteLine("========================================");
Console.WriteLine("🚀 [ENV VERIFICATION] Checking Environment Variables:");
Console.WriteLine($"  - Jwt:Issuer: {builder.Configuration["Jwt:Issuer"]}");
Console.WriteLine($"  - Jwt:Audience: {builder.Configuration["Jwt:Audience"]}");
Console.WriteLine($"  - Jwt:Key Loaded: {!string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"])}");
Console.WriteLine($"  - Jwt:DurationInMinutes: {builder.Configuration["Jwt:DurationInMinutes"]}");
Console.WriteLine($"  - ConnectionString Loaded: {!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection"))}");
Console.WriteLine($"  - OtpSms:GatewayUrl: {builder.Configuration["OtpSms:GatewayUrl"]}");
Console.WriteLine("========================================");

// Add services to the container.
builder.Services.Configure<OtpSmsOptions>(
    builder.Configuration.GetSection(OtpSmsOptions.SectionName));

builder.Services.AddHttpClient(nameof(OtpSmsService), client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(".NET Framework");
});
builder.Services.AddSingleton<OtpSmsService>();

builder.Services.AddControllers(options =>
{
    // 🔒 यह पॉलिसी पूरी एप्लीकेशन के लिए ग्लोबल ऑथेंटिकेशन अनिवार्य कर देगी
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.Filters.Add(new AuthorizeFilter(policy));
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// --- 1. JWT Authentication Config ---
var jwtKey = builder.Configuration["Jwt:Key"] ?? "ThisIsMyVeryStrongSuperSecretKey1234567890";
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = !string.IsNullOrWhiteSpace(jwtIssuer),
        ValidateAudience = !string.IsNullOrWhiteSpace(jwtAudience),
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromMinutes(5)
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception is SecurityTokenExpiredException)
            {
                context.Response.Headers.Append("Token-Expired", "true");
            }
            return Task.CompletedTask;
        },
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";

            var payload = JsonSerializer.Serialize(new
            {
                statusCode = 401,
                message = "Unauthorized: Access token is missing or invalid."
            });
            await context.Response.WriteAsync(payload);
        }
    };
});

// ==========================================
// 🛑 Sabhi services add hone ke BAAD app build hoti hai
// ==========================================
var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseDeveloperExceptionPage(); // Live ya local par exact error dekhne ke liye

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowAngular");

// 🔐 Custom JWT Middleware (validates token on every API request and binds user to HttpContext)
app.UseMiddleware<JwtMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();


//using EMISAPIS.Helpers;

//using Microsoft.AspNetCore.Authentication.JwtBearer;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc.Authorization;
//using Microsoft.IdentityModel.Tokens;
//using Microsoft.OpenApi.Models;
//using System.Text;

//// 1) Load `.env` into process environment
//EnvFileLoader.Load();

//var builder = WebApplication.CreateBuilder(args);
//var app = builder.Build();
//// 2) Expand `${ENV_VAR}` placeholders in appsettings*.json from environment / .env
//EnvPlaceholderResolver.Expand(builder.Configuration);
//// Program.cs ke bilkul upar ya app build hone ke turant baad yeh add karein
//app.UseDeveloperExceptionPage();
//// Add services to the container.

//builder.Services.Configure<OtpSmsOptions>(
//    builder.Configuration.GetSection(OtpSmsOptions.SectionName));
//builder.Services.AddHttpClient(nameof(OtpSmsService), client =>
//{
//    client.Timeout = TimeSpan.FromSeconds(30);
//    client.DefaultRequestHeaders.UserAgent.ParseAdd(".NET Framework");
//});
//builder.Services.AddSingleton<OtpSmsService>();

////builder.Services.AddControllers()
////    .AddJsonOptions(options =>
////    {
////        // PascalCase for legacy clients; Angular maps both casings on consignee screen
////        options.JsonSerializerOptions.PropertyNamingPolicy = null;
////        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
////    });

//builder.Services.AddControllers(options =>
//{
//    // 🔒 यह पॉलिसी पूरी एप्लीकेशन के लिए ग्लोबल ऑथेंटिकेशन अनिवार्य कर देगी
//    var policy = new AuthorizationPolicyBuilder()
//        .RequireAuthenticatedUser()
//        .Build();

//    options.Filters.Add(new AuthorizeFilter(policy));
//})
//.AddJsonOptions(options =>
//{
//    options.JsonSerializerOptions.PropertyNamingPolicy = null;
//    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
//});
//// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
//builder.Services.AddEndpointsApiExplorer();
////builder.Services.AddSwaggerGen();
//builder.Services.AddSwaggerGen(c =>
//{
//    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
//    {
//        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
//        Name = "Authorization",
//        In = ParameterLocation.Header,
//        Type = SecuritySchemeType.ApiKey,
//        Scheme = "Bearer"
//    });

//    c.AddSecurityRequirement(new OpenApiSecurityRequirement
//    {
//        {
//            new OpenApiSecurityScheme
//            {
//                Reference = new OpenApiReference
//                {
//                    Type = ReferenceType.SecurityScheme,
//                    Id = "Bearer"
//                }
//            },
//            new string[] {}
//        }
//    });
//});



//builder.Services.AddCors(options =>
//{
//    //options.AddPolicy("AllowAngular",
//    //    policy =>
//    //    {
//    //        policy.WithOrigins(
//    //                  "http://localhost:4200",
//    //                  "https://localhost:4200")
//    //              .AllowAnyHeader()
//    //              .AllowAnyMethod();
//    //    });
//});
//// --- 1. JWT Authentication Config ---
//builder.Services.AddAuthentication(options =>
//{
//    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
//    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
//})
//.AddJwtBearer(options =>
//{
//    options.TokenValidationParameters = new TokenValidationParameters
//    {
//        ValidateIssuer = true,
//        ValidateAudience = true,
//        ValidateLifetime = true,
//        ValidateIssuerSigningKey = true,
//        ValidIssuer = builder.Configuration["Jwt:Issuer"],
//        ValidAudience = builder.Configuration["Jwt:Audience"],
//        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
//    };
//});





//// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

//if (!app.Environment.IsDevelopment())
//{
//    app.UseHttpsRedirection();
//}


//app.UseCors("AllowAngular");


//app.UseAuthentication();

//app.UseAuthorization();

//app.MapControllers();

//app.Run();