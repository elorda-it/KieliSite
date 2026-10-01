using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using COMMON;
using DBHelper;
using Dapper;
using Hangfire;
using Hangfire.MemoryStorage;
using MODEL;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.WebEncoders;
using KieliWeb.Caches;
using KieliWeb.Filters;
using KieliWeb.Hangfire;
using KieliWeb.Routing;
using Serilog;
using Serilog.Events;

string defaultTheme = "Kieli";
Log.Logger = new LoggerConfiguration().MinimumLevel.Error().MinimumLevel.Override("Microsoft", LogEventLevel.Error).Enrich.FromLogContext().WriteTo.File("logs/log.txt", LogEventLevel.Verbose, "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}", null, 1073741824L, null, buffered: false, shared: false, null, RollingInterval.Day, rollOnFileSizeLimit: false, null).CreateLogger();
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
IConfigurationSection siteSection = builder.Configuration.GetSection("Site");
string siteTheme = siteSection["Theme"] ?? defaultTheme;
QarSingleton.GetInstance().SetSiteTheme(siteTheme);
QarSingleton.GetInstance().SetSiteUrl(siteSection["SiteUrl"] ?? string.Empty);
QarSingleton.GetInstance().SetConnectionString(siteSection["ConnectionString"] ?? string.Empty);
QarSingleton.GetInstance().SetMasterConnectionString(siteSection["MasterConnection"] ?? string.Empty);
QarSingleton.GetInstance().SetReplicaConnectionString(siteSection["ReplicaConnection"] ?? string.Empty);
int port = builder.Configuration.GetValue<int?>("Site:Port").GetValueOrDefault();
if (port > 0)
{
    builder.WebHost.ConfigureKestrel(delegate(KestrelServerOptions options)
    {
        options.ListenAnyIP(port);
    });
}
builder.Services.Configure(delegate(RouteOptions options)
{
    options.ConstraintMap.Add("culture", typeof(CultureRouteConstraint));
});
builder.Services.AddAuthentication(delegate(AuthenticationOptions options)
{
    options.DefaultSignInScheme = "Cookies";
    options.DefaultAuthenticateScheme = "Cookies";
    options.DefaultChallengeScheme = "Cookies";
}).AddCookie(delegate(CookieAuthenticationOptions options)
{
    options.LoginPath = new PathString("/kz/admin/login/");
    options.AccessDeniedPath = new PathString("/kz/admin/login/");
    options.LogoutPath = new PathString("/kz/admin/signout/");
    options.Cookie.Path = "/";
    options.SlidingExpiration = true;
    options.Cookie.Name = "qar_cookie";
    options.Cookie.HttpOnly = true;
});
builder.Services.AddControllersWithViews(delegate(MvcOptions configure)
{
    configure.Filters.Add(typeof(PermissionFilter));
    configure.Filters.Add(typeof(QarFilter));
    configure.Filters.Add(typeof(QarApiFilter));
}).ConfigureApiBehaviorOptions(delegate(ApiBehaviorOptions options)
{
    options.SuppressConsumesConstraintForFormFileParameters = true;
    options.SuppressInferBindingSourcesForParameters = true;
    options.SuppressModelStateInvalidFilter = true;
});
builder.Services.AddSession(delegate(SessionOptions options)
{
    options.IdleTimeout = TimeSpan.FromMinutes(20L);
    options.Cookie.HttpOnly = true;
    options.Cookie.Path = "/";
    options.Cookie.Name = "qarSession";
});
builder.Services.Configure(delegate(FormOptions o)
{
    o.ValueLengthLimit = int.MaxValue;
    o.ValueCountLimit = int.MaxValue;
    o.MultipartBodyLengthLimit = 2147483647L;
    o.MemoryBufferThreshold = int.MaxValue;
    o.KeyLengthLimit = int.MaxValue;
});
builder.Services.Configure(delegate(WebEncoderOptions options)
{
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All);
});
builder.Services.AddHangfire(delegate(IGlobalConfiguration x)
{
    x.UseMemoryStorage();
});
builder.Services.AddTransient<QarJob>();
builder.Services.AddHangfireServer();
builder.Services.AddHttpContextAccessor();
WebApplication app = builder.Build();
// Culture + clean-URL middleware.
//
// The homepage lives at the bare domain. "/home/index" and the default culture are
// implied defaults, so they are kept out of the address bar:
//
//   /                     -> homepage, default language   (rewritten internally, no redirect)
//   /ru                   -> homepage, Russian            (rewritten internally, no redirect)
//   /kz, /kz/home/index   -> 301 /                        (kz is the default; one URL per page)
//   /ru/home/index        -> 301 /ru
//   /home/index           -> 301 /
//   /kz/admin/login       -> served as-is (only Home/Index has its defaults folded away)
//   /brand/product/list   -> 301 /kz/brand/product/list   (culture prefix added)
//
// Anything with a file extension, /api and /hangfire bypass all of this.
// Canonical public host, taken from Site:SiteUrl. Anything arriving on another
// public hostname is sent here permanently, so the site has one address.
// Loopback is exempt so local runs and the deploy health check are unaffected.
string canonicalHost = (Uri.TryCreate(QarSingleton.GetInstance().GetSiteUrl(), UriKind.Absolute, out Uri siteUri) ? siteUri.Host : string.Empty);
app.Use(async delegate(HttpContext context, Func<Task> next)
{
    string requestHost = context.Request.Host.Host;
    bool isLoopback = requestHost.Equals("localhost", StringComparison.OrdinalIgnoreCase) || requestHost.StartsWith("127.") || requestHost == "::1" || requestHost == "[::1]";
    bool redirectable = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
    if (!string.IsNullOrEmpty(canonicalHost) && redirectable && !isLoopback && !requestHost.Equals(canonicalHost, StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("https://" + canonicalHost + context.Request.Path.Value + context.Request.QueryString.Value, permanent: true);
        return;
    }
    await next();
});
app.Use(async delegate(HttpContext context, Func<Task> next)
{
    string path = context.Request.Path.Value ?? string.Empty;
    string extension = Path.GetExtension(path);
    if (!string.IsNullOrEmpty(extension) || path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/hangfire", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }
    IMemoryCache memoryCache = context.RequestServices.GetService<IMemoryCache>();
    List<Language> languageList = QarCache.GetLanguageList(memoryCache);
    string defaultLang = languageList.FirstOrDefault((Language x) => x.IsDefault == 1)?.LanguageCulture ?? "kz";
    string query = context.Request.QueryString.Value ?? string.Empty;
    string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    bool hasCulture = segments.Length != 0 && languageList.Any((Language x) => x.LanguageCulture.Equals(segments[0], StringComparison.OrdinalIgnoreCase));
    // Only fold away the defaults for GET; a POST must never be answered with a redirect.
    bool isGet = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
    bool isHomeIndex = segments.Length == 2 && segments[0].Equals("home", StringComparison.OrdinalIgnoreCase) && segments[1].Equals("index", StringComparison.OrdinalIgnoreCase);
    bool isCultureHomeIndex = hasCulture && segments.Length == 3 && segments[1].Equals("home", StringComparison.OrdinalIgnoreCase) && segments[2].Equals("index", StringComparison.OrdinalIgnoreCase);
    bool isDefaultCultureOnly = hasCulture && segments.Length == 1 && segments[0].Equals(defaultLang, StringComparison.OrdinalIgnoreCase);

    if (isGet && (isHomeIndex || isCultureHomeIndex || isDefaultCultureOnly))
    {
        // Collapse every spelling of the homepage onto its one canonical URL.
        string culture = (hasCulture ? segments[0] : defaultLang);
        string canonical = (culture.Equals(defaultLang, StringComparison.OrdinalIgnoreCase) ? "/" : "/" + culture.ToLower());
        context.Response.Redirect(canonical + query, permanent: true);
        return;
    }
    if (hasCulture)
    {
        if (segments.Length == 1)
        {
            // "/ru" -> serve Home/Index without changing the address bar.
            context.Request.Path = "/" + segments[0] + "/home/index";
        }
        await next();
        return;
    }
    if (segments.Length == 0)
    {
        // "/" -> serve the default-language homepage in place. A rewrite, not a
        // redirect, so the bare domain stays in the address bar.
        context.Request.Path = "/" + defaultLang + "/home/index";
        await next();
        return;
    }
    // Keep the query string (e.g. /kz/news?cat=news) when adding the culture prefix.
    context.Response.Redirect("/" + defaultLang + path + query);
});
FileExtensionContentTypeProvider provider = new FileExtensionContentTypeProvider();
provider.Mappings.Remove(".xml");
provider.Mappings.Add(".xml", "application/xml");
provider.Mappings.Remove(".txt");
provider.Mappings.Add(".txt", "text/plain");
provider.Mappings.Remove(".xsl");
provider.Mappings.Add(".xsl", "text/xsl");
provider.Mappings.Remove(".exe");
provider.Mappings.Add(".exe", "application/exe");
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider,
    OnPrepareResponse = delegate(StaticFileResponseContext ctx)
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000");
    }
});
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
// Public pages with readable addresses; everything else keeps {culture}/{controller}/{action}.
foreach ((string name, string pattern, string action) in new[] {
    ("author", "{culture}/author", "Author"),
    ("services", "{culture}/services", "Services"),
    ("service", "{culture}/service/{slug}", "Service"),
    ("news", "{culture}/news", "Articles"),
    ("article", "{culture}/news/{slug}", "Article"),
    ("kazakhstan", "{culture}/kazakhstan", "Kazakhstan"),
    ("place", "{culture}/place/{slug}", "Place"),
    ("kaztest", "{culture}/kaztest", "Kaztest"),
    // addresses of the old kieli.kz, still in search engines and shared links: 301 to the new pages
    ("old-place", "{culture}/attraction/{query?}", "OldPlace"),
    ("old-place-home", "{culture}/home/attraction/{query?}", "OldPlace"),
    ("old-region", "{culture}/regionattraction", "OldPlace"),
    ("old-heritage", "{culture}/tradition/{query?}", "OldHeritage"),
    ("old-heritage-home", "{culture}/home/tradition/{query?}", "OldHeritage"),
    ("old-about", "{culture}/about/{query?}", "OldAbout"),
    ("old-about-home", "{culture}/home/about/{query?}", "OldAbout") })
{
    app.MapControllerRoute(name, pattern, new { controller = "Home", action }, new { culture = new CultureRouteConstraint() });
}
app.MapControllerRoute("default", "{culture}/{controller=Home}/{action=Index}/{query?}", null, new
{
    culture = new CultureRouteConstraint()
});
// An address no page answers: the site's «Бет табылмады» page with status 404 and noindex (a 200 page there
// would be indexed as a "soft 404"). The static 404.html stays for addresses without a language.
app.MapFallbackToController("{culture:culture}/{**path:nonfile}", "Missing", "Home");
app.MapFallbackToFile("404.html");
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new HangfireAuthorizationFilter[1]
    {
        new HangfireAuthorizationFilter()
    }
});
SimpleCRUD.SetDialect(SimpleCRUD.Dialect.MySQL);
SimpleCRUD.SetTableNameResolver(new QarTableNameResolver());
// db/ (schema + first content) is copied next to the binaries on build and publish.
string dbDirectory = Path.Combine(AppContext.BaseDirectory, "db");
KieliWeb.Setup.ContentStore.Init(dbDirectory);
// First start on an empty database (e.g. kieli_db): create the tables, the admin menu,
// roles, the first administrator and the initial content. Existing rows are never touched.
if (builder.Configuration.GetValue("Site:AutoInitDatabase", false))
{
    KieliWeb.Setup.DatabaseBootstrap.Run(dbDirectory, builder.Configuration, app.Environment.ContentRootPath);
}
// dotnet KieliWeb.dll --export-translations file.json : every translatable Kazakh text, for translators
int exportAt = Array.IndexOf(args, "--export-translations");
if (exportAt >= 0 && exportAt + 1 < args.Length)
{
    using (System.Data.IDbConnection exportConnection = Utilities.GetOpenConnection())
    {
        File.WriteAllText(args[exportAt + 1], KieliWeb.Setup.TranslationFiles.Export(exportConnection).ToString(Newtonsoft.Json.Formatting.Indented));
    }
    Console.WriteLine("translations exported to " + args[exportAt + 1]);
    return;
}
if (!app.Environment.IsDevelopment())
{
    BackgroundJob.Schedule((QarJob q) => QarJob.JobSaveReloginAdminIds(), TimeSpan.FromMinutes(1L));
    RecurringJob.AddOrUpdate("JobDeleteOldLogFiles", (QarJob q) => q.JobDeleteOldLogFiles(), Cron.Daily);
    // automatic news: every hour at :17, and once two minutes after a start
    RecurringJob.AddOrUpdate("JobCollectNews", (QarJob q) => q.JobCollectNews(), "17 * * * *");
    BackgroundJob.Schedule((QarJob q) => q.JobCollectNews(), TimeSpan.FromMinutes(2L));
}
app.Run();
