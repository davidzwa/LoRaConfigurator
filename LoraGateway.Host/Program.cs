using LoraGateway.Host.Hubs;
using Serilog;
using Serilog.Events;

static string GetUniqueLogFile(string postFix = "")
{
    return Path.Combine(Directory.GetCurrentDirectory(),
        $"../../../Logs/logs-{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}{postFix}.txt");
}

Log.Logger = new LoggerConfiguration()
#if DEBUG
    .MinimumLevel.Debug()
#else
                .MinimumLevel.Information()
#endif
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}",
        restrictedToMinimumLevel: LogEventLevel.Information
    )
    .WriteTo.Logger(lc => lc
        .WriteTo.File(GetUniqueLogFile(), LogEventLevel.Information))
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
builder.Services.AddSignalR();
builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSerilogRequestLogging();

// app.UseAuthorization();

app.MapRazorPages();
app.MapHub<FuotaHub>("/fuotaHub");

app.Run();