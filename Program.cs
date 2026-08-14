using EMISAPIS.Helpers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1) Load `.env` into process environment
EnvFileLoader.Load();

// 2) Expand `${ENV_VAR}` placeholders in appsettings*.json from environment / .env
EnvPlaceholderResolver.Expand(builder.Configuration);

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
            policy.AllowAnyOrigin() // Live aur local sabhi ke liye open rakhne ke liye (chahein toh WithOrigins use kar sakte hain)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// --- 1. JWT Authentication Config ---
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

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