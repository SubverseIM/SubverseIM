
var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("Subverse_");

// Add services to the container.
if (builder.Environment.IsProduction())
{
    builder.Services.AddDistributedSqlServerCache(options =>
    {
        options.ConnectionString = builder
            .Configuration.GetConnectionString("cacheDb");
        options.SchemaName = "dbo";
        options.TableName = "CFSCache";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.UseDefaultFiles(new DefaultFilesOptions() { RedirectToAppendTrailingSlash = true });

app.UseStaticFiles();

app.MapControllers();

app.Run();

public partial class Program { }