using MdViewer;
using MdViewer.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
var library = new MountedLibrary(builder.Configuration["DocumentsDirectory"]
    ?? Path.Combine(AppContext.BaseDirectory, "docs"));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

var files = app.MapGroup("/api/library");
files.AddEndpointFilter(async (context, next) =>
{
    context.HttpContext.Response.Headers.CacheControl = "no-store";
    context.HttpContext.Response.Headers.XContentTypeOptions = "nosniff";
    return await next(context);
});
files.MapGet("/documents", () => library.ListDocuments());
files.MapGet("/file", library.ReadDocumentAsync);
files.MapGet("/image", library.OpenImage);

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(MdViewer.Client._Imports).Assembly);

app.Run();
