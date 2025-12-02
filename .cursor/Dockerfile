FROM mcr.microsoft.com/dotnet/sdk:10.0-noble-amd64

ENV \
    DOTNET_GENERATE_ASPNET_CERTIFICATE=false \
    DOTNET_NOLOGO=true \
    DOTNET_SDK_VERSION=10.0.100 \
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    NUGET_XMLDOC_MODE=skip \
    POWERSHELL_DISTRIBUTION_CHANNEL=PSDocker-DotnetSDK-Ubuntu-24.04 \
    DOTNET_ROLL_FORWARD=Major

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
        git \
        jq \
        vim \
        nano \
        unzip \
        wget \
        libatomic1 \
    && rm -rf /var/lib/apt/lists/*

# Install PowerShell global tool
RUN powershell_version=7.6.0-preview.4 \
    && curl --fail --show-error --location --output PowerShell.Linux.x64.$powershell_version.nupkg https://powershellinfraartifacts-gkhedzdeaghdezhr.z01.azurefd.net/tool/$powershell_version/PowerShell.Linux.x64.$powershell_version.nupkg \
    && powershell_sha512='92ba2a8344f13d1c640f73d61488a582bae3ea82e4d00aad02efece3475f852855fb6f8ac37f72b4a14cdc1975af9f253d59ce72e36f3653e6b1ee87dc273f8f' \
    && echo "$powershell_sha512  PowerShell.Linux.x64.$powershell_version.nupkg" | sha512sum -c - \
    && mkdir --parents /usr/share/powershell \
    && dotnet tool install --add-source / --tool-path /usr/share/powershell --version $powershell_version PowerShell.Linux.x64 \
    && dotnet nuget locals all --clear \
    && rm PowerShell.Linux.x64.$powershell_version.nupkg \
    && ln -s /usr/share/powershell/pwsh /usr/bin/pwsh \
    && chmod 755 /usr/share/powershell/pwsh \
    && find /usr/share/powershell -print | grep -i '.*[.]nupkg$' | xargs rm

# Create non-root user
RUN useradd -m -s /bin/bash ubuntu

# Switch to non-root user
USER ubuntu

# Set working directory to user home
WORKDIR /home/ubuntu

# Configure git for the user
RUN git config --global user.email "agent@cursor.com" \
    && git config --global user.name "Cursor Agent" \
    && git config --global init.defaultBranch main

# Install .NET EF Core tools (needed for migrations)
RUN dotnet tool install --global dotnet-ef --version 10.0.* \
    && echo 'export PATH="$PATH:/home/ubuntu/.dotnet/tools"' >> /home/ubuntu/.bashrc

# Add .NET tools to PATH
ENV PATH="${PATH}:/home/ubuntu/.dotnet/tools"

# Verify installation
RUN dotnet --version && dotnet ef --version && git --version