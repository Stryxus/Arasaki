﻿using System.IO.Compression;
using System.Security.Authentication;

using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Identity.Web;

using Serilog;

using Arasaki.Server.Data;
using Arasaki.Server.Data.States;
using Microsoft.AspNetCore.Server.Kestrel.Core;

Logger.Initialise(new LoggerConfiguration().WriteTo.Console(outputTemplate: Logger.DefaultLogFormat).CreateLogger());

WebApplicationBuilder builder;
Services.SetConfiguration((builder = WebApplication.CreateBuilder(args)).Configuration);
if (string.IsNullOrWhiteSpace(builder.Environment.WebRootPath)) builder.Environment.WebRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

builder.WebHost.UseQuic().UseKestrel(o => 
{
#if DEBUG
    o.ListenAnyIP(7107, x =>
#else
    o.ListenAnyIP(8080, x =>
#endif
    {
        x.Protocols = HttpProtocols.Http3 | HttpProtocols.Http2;
        x.UseHttps();
    });
    o.AddServerHeader = false;
});
#if DEBUG
builder.Services.AddApplicationInsightsTelemetry(new ApplicationInsightsServiceOptions { ConnectionString = "00000000-0000-0000-0000-000000000000" });
#else
builder.Services.AddApplicationInsightsTelemetry(new ApplicationInsightsServiceOptions { ConnectionString = Services.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] });
#endif
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAdB2C"));
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddResponseCaching();
builder.Services.AddResponseCompression(options =>
{
    options.Providers.Add<BrotliCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "image/svg+xml" });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.SmallestSize);

builder.Services.AddSingleton<JSInterop>();
builder.Services.AddSingleton<JSInterop.RuntimeInterop>();
builder.Services.AddSingleton<JSInterop.UIInterop>();
builder.Services.AddSingleton<UIState>();

WebApplication app = builder.Build();
Services.SetServiceProvider(app.Services.CreateScope().ServiceProvider);

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseResponseCaching();
app.UseResponseCompression();
app.MapRazorPages();
app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
if (Runtime.IsDevelopmentMode) await app.RunAsync();
else await app.RunAsync();
