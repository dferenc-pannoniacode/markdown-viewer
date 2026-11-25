# Markdown Viewer

A .NET 11 Blazor WebAssembly app for reading Markdown files from a Docker-mounted library or a browser-selected folder.

## Run with Docker

```sh
docker compose up --build
```

Open [http://localhost:8080](http://localhost:8080). The bundled sample library is mounted from `./docs`.

To mount another library, set `DOCS_PATH` in your environment or create a `.env` beside `compose.yaml`:

```dotenv
DOCS_PATH="C:/Users/me/Documents/Notes"
VIEWER_PORT=8080
```

Use an existing directory accessible to Docker Desktop. The container opens it read-only, and Compose publishes the viewer on the loopback interface. Refresh the browser after editing, adding, or removing mounted documents.

## Browse a local folder

Choose **Open folder** in a current browser and select the library root. The file tree preserves nested folders, and the browser reads the files on demand. If you reload or open a local document bookmark in a new tab, choose the same folder again.

## Document and link support

- Use UTF-8 `.md` files up to 8 MiB. The viewer starts at a root `README.md`, then falls back to the first document alphabetically.
- Standard links resolve relative to the current file. Links starting with `/` resolve from the library root. Directory links look for `README.md`, and heading fragments use GitHub-style IDs.
- Wiki links support document names, paths, headings, and aliases, such as `[[Guide#First steps|Start here]]`. Names resolve beside the current file first, then across the library. When several files match, choose a path.
- Relative images support PNG, JPEG, GIF, WebP, AVIF, and BMP. Dotfiles and mounted symbolic links are skipped. Markdown renders with raw HTML disabled.
- Pick **System**, **Light**, or **Dark** in the toolbar. Your preference is saved in that browser.

## Run locally for development

With the .NET 11 SDK installed, run `dotnet run --project src/MdViewer` and open the address printed by the app. Set `DocumentsDirectory` to an absolute path to use another mounted library outside Docker.
