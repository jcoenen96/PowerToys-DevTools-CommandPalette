# Dev tools for Command Palette

> [!WARNING]
> **This project was vibe coded.** It was written with an AI coding assistant and has **not had a human
> review yet**. Read the code before you trust it with your repositories, and expect rough edges.

A [PowerToys Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview) extension
that opens your solutions and repositories in any IDE, with the branch behind every name, and runs git for you.
Inspired by [PowerToys-Run-VisualStudio](https://github.com/davidegiacometti/PowerToys-Run-VisualStudio), but for
every tool you use and with git built in.

```text
Open in Visual Studio
  Shop.slnx (feature/cart)        C:\Users\me\source\worktrees\Shop\feature-cart\Shop.slnx   [worktree]
  Shop.slnx (main)                C:\Users\me\source\repos\Shop\Shop.slnx
  Tools.sln (main)                C:\Users\me\source\repos\Shop\tools\Tools.sln
```

## What it does

- **One command per tool.** Every enabled tool gets its own top-level command, such as *Open in Visual Studio*,
  *Open in Rider* or *Open in Visual Studio Code*, so each can have **its own hotkey and alias** (see below). Tools
  you add in `tools.json` get one too; a tool you switch off has none.
- **Clones first.** A tool's page opens on your clones only; worktrees join the results as soon as you type.
- **Or everything at once.** *All dev tools* shows every tool's results on one page, grouped per tool, and gets
  its own hotkey too.
- **Solutions with their branch.** Visual Studio and Rider list the `.slnx`/`.sln` files of every repository and
  worktree as `Shop.slnx (feature/cart)`; folder tools list the repositories as `Shop (feature/cart)`. A repository
  with both `Shop.slnx` and `Shop.sln` shows the `.slnx`. Branch names come straight from `.git`, so the list is
  instant.
- **Your folders.** You set where your repositories are cloned and where your worktrees live; both are searched.
  Excluded folders are skipped, so your worktree folder can sit inside your repository folder.
- **Open with anything.** Ctrl+K on any item opens the same solution or folder in another tool, opens File
  Explorer or Windows Terminal there, copies the path or branch, or jumps to its git commands.
- **Git from the palette.** *Git repositories* lists every clone and worktree. Choose one, or press Ctrl+K on it, for
  its git commands, listed before *Open in…* and the rest:
  - switch branch (remote branches get a local tracking branch), or create a new branch;
  - create a worktree for an existing branch, or a new branch in a new worktree, in your worktree folder;
  - list and remove worktrees, prune stale ones;
  - fetch, pull (fast-forward or rebase), push (sets the upstream for new branches), stash and pop, status, log,
    and discard all changes (asks first).
- **Presets, plus any tool you like.** Ready to go: Visual Studio (any edition, found with vswhere), Visual Studio
  Code and Insiders, JetBrains Rider, IntelliJ IDEA, WebStorm, PyCharm, GoLand, CLion, RustRover and DataGrip
  (Toolbox or standalone installs), Orca, Windows Terminal and File Explorer. Add your own tools, or change a
  preset, in `tools.json`.

## Install

The extension is an MSIX package, like every Command Palette extension. Until there is a release:

1. Install [PowerToys](https://learn.microsoft.com/windows/powertoys/install) with Command Palette turned on.
2. Install the [.NET 11 SDK](https://dot.net). To debug, also Visual Studio 2026 with the workloads in
   `.vsconfig`.
3. Run the deploy script from the repository root:

   ```powershell
   ./scripts/Deploy-Extension.ps1
   ```

   It builds the extension for your machine (x64 or ARM64), registers the build with Windows and restarts Command
   Palette. Run it again after every change: it updates the installed extension in place, so your settings,
   `tools.json` and hotkeys stay. Any other installed copy (another configuration, platform or checkout) is
   removed first, so the extension is only installed once.

   - `-Configuration Release`, `-Platform ARM64`: build something else.
   - `-SkipBuild`: deploy the last build again.
   - `-NoReload`: don't restart Command Palette; run **Reload** in it yourself.
   - `-Clean`: remove every installed copy and install fresh (resets settings and `tools.json`).
   - `-Uninstall`: remove the extension (and its settings).

   It needs **Developer Mode** (Settings > System > For developers), which any unsigned extension needs. In Visual
   Studio, **Build > Deploy** on *DevTools.CommandPalette* does the same.

## Set it up

Open Command Palette settings, then **Extensions > Dev tools**:

| Setting | Default | What it does |
| --- | --- | --- |
| Repository folders | `%USERPROFILE%\source\repos` | Where your repositories are cloned; one folder per line. |
| Excluded folders | none | Skipped by the repository search; a full path or a folder name, one per line. |
| Worktree folders | `%USERPROFILE%\source\worktrees` | Where your worktrees live; new worktrees go into the first. |
| New worktree path | `{worktreeRoot}\{repo}\{branch}` | Layout for new worktrees; `feature/x` becomes `feature-x`. |
| Repository search depth | 3 | Folder levels below each folder searched for repositories. |
| Solution search depth | 3 | Folder levels inside a repository searched for solutions. |
| Show worktrees only when searching | on | Tool pages list only clones until you type. |
| Git executable | `git` | The git to run. |
| One switch per tool | on | Off removes the tool's command (and hotkey) and its Ctrl+K entries. |

### A hotkey per tool

Command Palette assigns hotkeys and aliases to top-level commands; an extension can't pick them for you. For each
tool you want on a key:

1. Open Command Palette settings, **Extensions > Dev tools**.
2. Select the command, for example *Open in Visual Studio*, and set a **global hotkey** (say `Win+Ctrl+V`) and an
   **alias** (say `vs`).
3. Do the same for *All dev tools* (say `Win+Ctrl+D`) to get every tool's results together.

The hotkey opens the palette straight on that tool's list of solutions with their branches. Command ids are stable
(`devtools.tool.<tool id>`, and `devtools.all` for *All dev tools*), so your hotkeys survive updates and changes to `tools.json`.

### Your own tools: `tools.json`

Run **Edit dev tools** in the palette. It creates and opens `tools.json` in the extension's settings folder with
every field explained. Saved changes apply right away.

```jsonc
{
  "tools": [
    // A preset installed somewhere unusual: only the fields you set change.
    { "id": "rider", "executables": ["D:\\Tools\\Rider\\bin\\rider64.exe"] },

    // A new tool. It shows up as its own "Open in Sublime Text" command.
    {
      "id": "sublime",
      "displayName": "Sublime Text",
      "executables": ["%ProgramFiles%\\Sublime Text\\sublime_text.exe", "subl.exe"],
      "targetKind": "Folder",
      "arguments": "\"{target}\""
    }
  ]
}
```

- `executables` are tried in order: a path (with `%VARIABLES%` and `*` wildcards, where the newest version
  wins), a name on PATH, or `vswhere:<arguments>`.
- `targetKind` is `Folder` (open the repository) or `Files` (open the files that match `filePatterns`, optionally
  with `fallBackToFolder`).
- `arguments` placeholders: `{target}`, `{folder}`, `{repo}`, `{branch}`, `{name}`.
- A new tool gets its own *Open in …* command (with its own hotkey and alias) and its own switch in the settings.
  When none of its `executables` is found, the command stays, with a warning, so its hotkey isn't lost.

## Architecture

A layered solution; each layer only knows the ones below it.

```text
DevTools.CommandPalette   Presentation: the Command Palette extension (pages, commands, settings page)
        │
DevTools.Infrastructure   File system scanning, .git reading, the git CLI, vswhere, processes, tools.json
        │
DevTools.Application      Use cases (launch targets, launching, git branches/worktrees) behind ports
        │
DevTools.Domain           Tools, repositories, worktrees, branches, launch targets; no I/O
```

- **Domain**, **Application** and **Infrastructure** target `net11.0` and run anywhere, so their tests run on
  Linux in CI. Only **CommandPalette** targets `net11.0-windows`.
- The application layer defines its ports in `Abstractions/` (`IRepositoryScanner`, `IGitRunner`,
  `IExecutableResolver`, …); the infrastructure implements them. `DevToolsServices` wires them together without a
  DI container, which keeps the extension trimmable and AOT-friendly.
- Repository scans are cached for 30 seconds and refreshed after every git command that changes them; Ctrl+K >
  *Refresh* rescans right away.

```text
src/
  DevTools.Domain/            Git/, Tools/, Projects/, Settings/
  DevTools.Application/       Abstractions/, Tools/, Repositories/, Git/
  DevTools.Infrastructure/    FileSystem/, Git/, Tools/, Processes/
  DevTools.CommandPalette/    Pages/, Commands/, Settings/, Package.appxmanifest
tests/
  DevTools.Application.Tests/     unit tests with NSubstitute
  DevTools.Infrastructure.Tests/  against real git repositories and worktrees in a temp folder
```

## Develop

```bash
dotnet test DevTools.Core.slnf        # the cross-platform layers and their tests; works on Linux and macOS too
```

The extension project itself needs Windows. Build, install and reload it in one go with
`./scripts/Deploy-Extension.ps1` (see [Install](#install)).

## Code style

C# is formatted with [CSharpier](https://csharpier.com) and
linted by the .NET analyzers with the rules in `.editorconfig` (warnings fail CI).

### On your machine

Everything installs itself:

- CSharpier is a repo-local dotnet tool (`dotnet-tools.json`). Your first .NET build runs `dotnet tool restore`.
- `npm install` in the repository root installs secretlint, markdownlint and lint-staged.
- Either step points git at `.githooks/`. Its pre-commit hook checks only the staged files and adds any fixes to
  the commit:
  - **secretlint** blocks secrets. If [Betterleaks](https://github.com/betterleaks/betterleaks) is installed, it
    runs too.
  - **CSharpier** formats C#.
  - **markdownlint** checks Markdown.

  The hook needs `npm install` once, even if you only change C#.

To run the tools by hand:

```bash
dotnet csharpier format .
npx markdownlint "**/*.md" --ignore node_modules
```

### In your editor

- **VS Code** recommends the extensions in `.vscode/extensions.json`, and `.vscode/settings.json` formats C# with
  CSharpier and Markdown with markdownlint on save.
- **Rider** reads `.editorconfig`, and `.idea/` asks for the CSharpier plugin with format on save.
- **Visual Studio** asks for the CSharpier extension through `.vsconfig`.

### In CI

[MegaLinter](https://megalinter.io) (`.mega-linter.yml`) runs CSharpier, the .NET analyzers, Betterleaks,
secretlint, Trivy, editorconfig-checker, markdownlint, yamllint, jsonlint, actionlint and ShellCheck on every
push. The core tests run on Linux and the extension is built for x64 and ARM64 on Windows.

Pull request titles follow [Conventional Commits](https://www.conventionalcommits.org) (`feat:`, `fix(git):`,
`feat!:`); [GitVersion](https://gitversion.net) derives the version from them when a PR is squash-merged.

## Known gaps

- Not yet tried on a real Windows machine with Command Palette; the Windows build runs in CI.
- The Orca preset assumes the default per-user install (`%LOCALAPPDATA%\Programs\Orca\Orca.exe`) and that Orca
  accepts a folder on its command line. Override it in `tools.json` if that is not the case.
- The extension uses the template's placeholder logos.

## License

[MIT](LICENSE)
