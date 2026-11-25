using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using MdViewer.Library;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace MdViewer.Client.Pages;

public partial class Home : IAsyncDisposable
{
    private const int MinTextWidth = 50;
    private const int MaxTextWidth = 100;
    private static readonly string AppVersion = "v" + typeof(Home).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+', 2)[0];

    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;

    [SupplyParameterFromQuery] public string? Source { get; set; }
    [SupplyParameterFromQuery] public string? File { get; set; }
    [SupplyParameterFromQuery] public string? Wiki { get; set; }
    [SupplyParameterFromQuery] public string? Missing { get; set; }
    [SupplyParameterFromQuery] public string? From { get; set; }

    private bool IsLocal => Source == "local";
    private string Fragment => Uri.UnescapeDataString(new Uri(Navigation.Uri).Fragment.TrimStart('#'));
    private ElementReference folderInput;
    private ElementReference drawerClose;
    private ElementReference drawerToggle;
    private IJSObjectReference? browser;
    private LibraryCatalog? localCatalog;
    private Task<LibraryCatalog>? mountedCatalog;
    private LibraryCatalog? catalog;
    private LibraryTree? tree;
    private CancellationTokenSource? load;
    private RenderedDocument? rendered;
    private string? activePath;
    private string? returnUrl;
    private string? notice;
    private string? pendingScroll;
    private string[] matches = [];
    private string statusTitle = "";
    private string statusText = "";
    private string theme = "system";
    private int textWidth = MaxTextWidth;
    private string paragraphAlignment = "left";
    private bool loading = true;
    private bool drawerOpen;
    private bool focusDrawer;

    protected override Task OnParametersSetAsync() => browser is null ? Task.CompletedTask : LoadAsync();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                browser = await JS.InvokeAsync<IJSObjectReference>("import", "./reader.js");
                theme = await JS.InvokeAsync<string>("viewerTheme.read");
                await LoadReadingPreferencesAsync(browser);
                await LoadAsync();
            }
            catch (JSException exception)
            {
                loading = false;
                SetStatus("Couldn’t start the viewer", exception.Message);
            }

            StateHasChanged();
        }

        if (!firstRender && pendingScroll is { } fragment)
        {
            pendingScroll = null;
            try
            {
                await browser!.InvokeVoidAsync("scrollDocument", fragment);
            }
            catch (JSException)
            {
                // The document remains available if scrolling fails.
            }
        }

        if (focusDrawer)
        {
            focusDrawer = false;
            await (drawerOpen ? drawerClose : drawerToggle).FocusAsync(preventScroll: true);
        }
    }

    private async Task LoadAsync()
    {
        CancelLoad();
        load = new CancellationTokenSource();
        var token = load.Token;
        var local = IsLocal;
        var requested = File;
        var wiki = Wiki;
        var missing = Missing;
        var from = From;
        var fragment = Fragment;
        loading = true;
        rendered = null;
        activePath = null;
        returnUrl = null;
        matches = [];
        pendingScroll = null;

        try
        {
            var next = local ? localCatalog : await GetMountedCatalogAsync(token);
            token.ThrowIfCancellationRequested();
            if (next != catalog)
            {
                catalog = next;
                tree = next is null ? null : LibraryTree.Build(next.Documents);
            }

            if (catalog is null)
            {
                SetStatus("Open your folder", "Choose your Markdown folder to start reading. Your files stay on this device.");
                return;
            }

            if (wiki is not null || missing is not null)
            {
                var origin = from is null ? null : catalog.Find(from);
                returnUrl = ViewerUrl.Document(local, origin);
                matches = wiki is not null && origin is not null ? catalog.ResolveWiki(origin, wiki) : [];
                if (matches.Length != 1)
                {
                    SetStatus(matches.Length > 1 ? "Choose a document" : "Document unavailable",
                        matches.Length > 1 ? $"Several documents match “{wiki}”." : $"Couldn’t find “{wiki ?? missing}” in this library.");
                    return;
                }

                requested = matches[0];
            }

            var path = requested is null ? catalog.DefaultDocument : catalog.Find(requested);
            if (path is null)
            {
                returnUrl = requested is null ? null : ViewerUrl.Document(local);
                SetStatus(requested is null ? "No Markdown files" : "Document unavailable",
                    requested is null ? "Add Markdown files to this library or open another folder."
                        : $"Couldn’t find “{requested}” in this library.");
                return;
            }

            activePath = path;
            var markdown = local ? await browser!.InvokeAsync<string?>("readDocument", token, path)
                : await ReadMountedAsync(path, token);
            token.ThrowIfCancellationRequested();
            if (markdown is null)
            {
                returnUrl = ViewerUrl.Document(local);
                SetStatus("Document unavailable", $"Couldn’t open “{path}”.");
                return;
            }

            var document = await MarkdownReader.RenderAsync(markdown, path, catalog, local, ImageUrlAsync);
            token.ThrowIfCancellationRequested();
            rendered = document;
            pendingScroll = fragment;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException or JSException or InvalidOperationException)
        {
            if (!token.IsCancellationRequested)
            {
                SetStatus("Couldn’t read the library", exception.Message);
            }
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                loading = false;
            }
        }

        async Task<string?> ImageUrlAsync(string image)
        {
            if (!local)
            {
                return ViewerUrl.FileApi("image", image);
            }

            try
            {
                return await browser!.InvokeAsync<string?>("imageUrl", token, image, LibraryPath.ImageType(image));
            }
            catch (JSException)
            {
                return null;
            }
        }
    }

    private async Task<LibraryCatalog> GetMountedCatalogAsync(CancellationToken token)
    {
        if (mountedCatalog is null || mountedCatalog.IsFaulted || mountedCatalog.IsCanceled)
        {
            mountedCatalog = FetchCatalogAsync();
        }

        return await mountedCatalog.WaitAsync(token);
    }

    private async Task<LibraryCatalog> FetchCatalogAsync() => new(await Http.GetFromJsonAsync(
        "api/library/documents", CatalogJson.Default.StringArray) ?? []);

    private async Task<string?> ReadMountedAsync(string path, CancellationToken token)
    {
        using var response = await Http.GetAsync(ViewerUrl.FileApi("file", path), token);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
        {
            throw new InvalidOperationException("This document exceeds 8 MiB.");
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(token);
    }

    private async Task OpenFolderAsync()
    {
        if (browser is null)
        {
            return;
        }

        try
        {
            notice = null;
            var selected = await browser.InvokeAsync<string[]?>("selectFolder", folderInput);
            if (selected is null)
            {
                return;
            }

            var reopenRequested = IsLocal && localCatalog is null;
            localCatalog = new LibraryCatalog(selected.Where(path => path.Split('/').All(LibraryPath.IsSegment)));
            CloseDrawer();
            if (reopenRequested || Navigation.ToBaseRelativePath(Navigation.Uri) == "?source=local")
            {
                await LoadAsync();
            }
            else
            {
                Navigation.NavigateTo(ViewerUrl.Document(true));
            }
        }
        catch (JSException exception)
        {
            notice = $"Couldn’t open the folder. {exception.Message}";
        }
    }

    private async Task ChangeThemeAsync(ChangeEventArgs args)
    {
        var selection = args.Value?.ToString() ?? "system";
        try
        {
            await JS.InvokeVoidAsync("viewerTheme.set", selection);
            theme = selection;
            notice = null;
        }
        catch (JSException)
        {
            notice = "Couldn’t change the theme.";
        }
    }

    private async Task LoadReadingPreferencesAsync(IJSObjectReference reader)
    {
        try
        {
            var savedWidth = await reader.InvokeAsync<string?>("readPreference", "text-width");
            if (int.TryParse(savedWidth, out var width) && width is >= MinTextWidth and <= MaxTextWidth)
            {
                textWidth = width;
            }

            var savedAlignment = await reader.InvokeAsync<string?>("readPreference", "paragraph-alignment");
            if (savedAlignment is "left" or "justify")
            {
                paragraphAlignment = savedAlignment;
            }
        }
        catch (JSException)
        {
            // The reader still opens if preferences are unavailable.
        }
    }

    private Task SaveTextWidthAsync() => SaveReadingPreferenceAsync("text-width", textWidth);

    private Task SaveParagraphAlignmentAsync() => SaveReadingPreferenceAsync("paragraph-alignment", paragraphAlignment);

    private async Task SaveReadingPreferenceAsync(string name, object value)
    {
        if (browser is null)
        {
            return;
        }

        try
        {
            await browser.InvokeVoidAsync("savePreference", name, value);
        }
        catch (JSException)
        {
            // The reader still uses the selected preference for this page.
        }
    }

    private void SetStatus(string title, string text) => (statusTitle, statusText) = (title, text);
    private void ToggleDrawer()
    {
        drawerOpen = !drawerOpen;
        focusDrawer = true;
    }

    private void CloseDrawer()
    {
        if (drawerOpen)
        {
            ToggleDrawer();
        }
    }
    private void HandleDrawerKey(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
        {
            CloseDrawer();
        }
    }

    private void CancelLoad()
    {
        load?.Cancel();
        load?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        CancelLoad();
        if (browser is not null)
        {
            await browser.DisposeAsync();
        }
    }
}

[JsonSerializable(typeof(string[]))]
internal sealed partial class CatalogJson : JsonSerializerContext;
