
using Fitomad.Apns;
using Fitomad.Apns.Entities;
using Fitomad.Apns.Extensions;
using Microsoft.EntityFrameworkCore;
using SubverseIM.Bootstrapper.Models;
using SubverseIM.Bootstrapper.Services;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

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
        options.UseSqlServer(builder
            .Configuration.GetConnectionString("serviceDb"));
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

string? certFilePath, certPassword, keyFilePath;
certFilePath = builder.Configuration.GetValue<string>("Apns:CertFilePath");
certPassword = builder.Configuration.GetValue<string>("Apns:CertPassword");
keyFilePath = builder.Configuration.GetValue<string>("Apns:KeyFilePath");

if (File.Exists(certFilePath))
{
    // Get private key from key container if possible
    X509Certificate2 certificateWithKey;
    if (builder.Environment.IsProduction() && OperatingSystem.IsWindows())
    {
        if (File.Exists(keyFilePath))
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048, new CspParameters
            {
                Flags = CspProviderFlags.UseMachineKeyStore,
                KeyContainerName = "SubverseIM"
            }))
            {
                rsa.FromXmlString(File.ReadAllText(keyFilePath));
                rsa.PersistKeyInCsp = true;
            }
        }

        using (X509Certificate2 certificate = X509CertificateLoader.LoadCertificateFromFile(certFilePath))
        using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048, new CspParameters
        {
            Flags =
            CspProviderFlags.UseMachineKeyStore |
            CspProviderFlags.UseExistingKey,
            KeyContainerName = "SubverseIM"
        }))
        {
            certificateWithKey = certificate.CopyWithPrivateKey(rsa);
        }
    }
    else
    {
        certificateWithKey = X509CertificateLoader.LoadPkcs12FromFile(certFilePath, certPassword);
    }

    // Set APNS connection settings
    var settings = new ApnsSettingsBuilder()
        .InEnvironment(builder.Environment.IsProduction() ?
            ApnsEnvironment.Production : ApnsEnvironment.Development)
        .WithX509Certificate2(certificateWithKey)
        .SetTopic("com.chosenfewsoftware.SubverseIM")
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