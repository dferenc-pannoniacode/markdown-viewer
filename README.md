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

### Pull the image from GitHub Container Registry

Save this example as `compose.ghcr.yaml` beside your `docs` directory, or set `DOCS_PATH` to an existing Markdown library.

```yaml
services:
  viewer:
    image: ghcr.io/dferenc-pannoniacode/markdown-viewer:${VIEWER_IMAGE_TAG:-v0.9.0-rc4}
    container_name: markdown-viewer
    ports:
      - "127.0.0.1:${VIEWER_PORT:-8080}:8080"
    environment:
      DocumentsDirectory: /docs
    volumes:
      - type: bind
        source: ${DOCS_PATH:-./docs}
        target: /docs
        read_only: true
        bind:
          create_host_path: false
```

```sh
docker compose -f compose.ghcr.yaml up -d --pull always
```

Open [http://localhost:8080](http://localhost:8080). The container mounts `./docs` read-only by default and accepts the same `DOCS_PATH` and `VIEWER_PORT` settings as the local build.

GitHub Actions publishes an image when a version tag is pushed for a commit on `main`. To use the previous release, set `VIEWER_IMAGE_TAG=v0.9.0-rc3` in your environment or `.env` file.

## Browse a local folder

Choose **Open folder** in a current browser and select the library root. The file tree preserves nested folders, and the browser reads the files on demand. If you reload or open a local document bookmark in a new tab, choose the same folder again.

## Document and link support

- Use UTF-8 `.md` files up to 8 MiB. The viewer starts at a root `README.md`, then falls back to the first document alphabetically.
- Standard links resolve relative to the current file. Links starting with `/` resolve from the library root. Directory links look for `README.md`, and heading fragments use GitHub-style IDs.
- Use **Back** in the toolbar to return to the previous document and resume reading where you left off.
- Wiki links support document names, paths, headings, and aliases, such as `[[Guide#First steps|Start here]]`. Names resolve beside the current file first, then across the library. When several files match, choose a path.
- Relative images support PNG, JPEG, GIF, WebP, AVIF, and BMP. Dotfiles and mounted symbolic links are skipped. Markdown renders with raw HTML disabled.
- Pick **System**, **Light**, or **Dark** in the toolbar. Your preference is saved in that browser.

### PlantUML diagrams

Put a self-contained diagram between `@startuml` and `@enduml` on their own lines at the top level of a Markdown document.

```markdown
@startuml
Alice -> Bob: Hello
Bob --> Alice: Hi
@enduml
```

You can also use a `plantuml` code fence, with or without the tags. The viewer renders diagrams in the browser for mounted libraries and local folders. PlantUML shows syntax errors in the diagram. If the renderer cannot produce an image, the source stays visible.

## Run locally for development

With the .NET 11 SDK and Node.js installed, run these commands from the repository root and open the address printed by the app.

```sh
npm ci --prefix src/MdViewer
dotnet run --project src/MdViewer
```

Set `DocumentsDirectory` to an absolute path to use another mounted library outside Docker.
