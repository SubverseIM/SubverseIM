
using Fitomad.Apns;
using Fitomad.Apns.Entities;
using Fitomad.Apns.Entities.Settings;
using Fitomad.Apns.Extensions;
using Microsoft.EntityFrameworkCore;
using SubverseIM.Bootstrapper.Models;
using SubverseIM.Bootstrapper.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("Subverse_");

// Add services to the container.
if (builder.Environment.IsProduction())
{
    builder.Services.AddDistributedSqlServerCache(options =>
    {
        options.ConnectionString = builder
            .Configuration.GetConnectionString("serviceDb");
        options.SchemaName = "dbo";
        options.TableName = "ServiceCache";
    });

    builder.Services.AddDbContext<SubverseContext>(options => 
    {
        options.UseSqlServer("serviceDb");
    }, ServiceLifetime.Singleton);
}
else
{
    builder.Services.AddDistributedMemoryCache();

    builder.Services.AddDbContext<SubverseContext>(options => 
    {
        options.UseInMemoryDatabase("serviceDb");
    }, ServiceLifetime.Singleton);
}

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

string? jwtContent, jwtKey, teamId;
jwtContent = builder.Configuration.GetValue<string>("Apns:JwtContent");
jwtKey = builder.Configuration.GetValue<string>("Apns:JwtKey");
teamId = builder.Configuration.GetValue<string>("Apns:TeamId");

if (!string.IsNullOrEmpty(jwtContent) && !string.IsNullOrEmpty(jwtKey) && !string.IsNullOrEmpty(teamId))
{
    var jwtInformation = new ApnsJsonToken
    {
        Content = jwtContent,
        KeyId = jwtKey,
        TeamId = teamId
    };

    // Set APNS connection settings
    var settings = new ApnsSettingsBuilder()
        .InEnvironment(builder.Environment.IsProduction() ? 
            ApnsEnvironment.Production : ApnsEnvironment.Development)
        .SetTopic("com.chosenfewsoftware.SubverseIM")
        .WithJsonToken(jwtInformation)
        .Build();

    builder.Services.AddApns(settings);
}

builder.Services.AddSingleton<IPushService, PushService>();

int? listenPortNum = builder.Configuration.GetValue<int?>("Hosting:ListenPortNum");
if (builder.Environment.IsDevelopment() && listenPortNum.HasValue)
{
    builder.WebHost.UseKestrel(options =>
    {
        options.ListenAnyIP(listenPortNum.Value, options =>
        {
            options.UseHttps(
                builder.Configuration.GetValue<string>("Privacy:CertFilePath") ?? "localhost.pfx", 
                builder.Configuration.GetValue<string>("Privacy:CertPassword")
                );
        });
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.UseWebSockets();

app.UseDefaultFiles(new DefaultFilesOptions() { RedirectToAppendTrailingSlash = true });

app.UseStaticFiles();

app.MapControllers();

app.Run();

public partial class Program { }