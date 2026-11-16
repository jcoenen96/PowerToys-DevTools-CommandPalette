# Everything needed to work on the extension, on Linux: the .NET 11 SDK (with PowerShell and git) from the
# .NET dev container image, and Node.js for the pre-commit tooling (package.json).
# Used by VS Code / Dev Containers (devcontainer.json) and by scripts/Deploy-FromContainer.ps1.
FROM mcr.microsoft.com/devcontainers/dotnet:11.0

ARG NODE_VERSION=24
RUN su vscode -c ". \"$NVM_DIR/nvm.sh\" && nvm install $NODE_VERSION && nvm alias default $NODE_VERSION && nvm cache clear"

# Builds the Windows-only projects on Linux too (build/ContainerBuild.props). MSBuild reads it as a property.
ENV DevToolsContainerBuild=true

# Mount points for the NuGet and npm caches (named volumes), owned by the container user.
RUN mkdir -p /home/vscode/.nuget/packages /home/vscode/.npm \
    && chown -R vscode:vscode /home/vscode/.nuget /home/vscode/.npm

USER vscode
