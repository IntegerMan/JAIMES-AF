# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/devcontainers/base:ubuntu-24.04

ARG DEBIAN_FRONTEND=noninteractive

# Install core build dependencies.
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        git \
        jq \
        unzip \
        build-essential \
        libicu-dev \
        zlib1g && \
    rm -rf /var/lib/apt/lists/*

ENV DOTNET_ROOT=/usr/share/dotnet

# Install the latest .NET 10 SDK via the official installer script.
RUN curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh && \
    chmod +x /tmp/dotnet-install.sh && \
    /tmp/dotnet-install.sh --channel 10.0 --install-dir "${DOTNET_ROOT}" --no-path && \
    rm /tmp/dotnet-install.sh && \
    ln -sf "${DOTNET_ROOT}/dotnet" /usr/local/bin/dotnet && \
    dotnet --info

# Ensure the SDK directory is on PATH for any additional shells.
ENV PATH="${DOTNET_ROOT}:${PATH}"

WORKDIR /workspace
