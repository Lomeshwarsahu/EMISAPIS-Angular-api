using EMISAPIS.Helpers;

// 1) Load `.env` into process environment
EnvFileLoader.Load();

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // PascalCase for legacy clients; Angular maps both casings on consignee screen
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy =>
        {
            policy.WithOrigins(
                      "http://localhost:4200",
                      "https://localhost:4200")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});


var app = builder.Build();

// Configure the HTTP request pipeline.
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
app.UseAuthorization();

app.MapControllers();

app.Run();
