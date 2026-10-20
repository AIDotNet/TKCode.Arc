# Gallery WebAssembly host: publish in a container, serve the static output with nginx.
# Built by .github/workflows/docker-gallery.yml, not locally. The project is not in TKCode.Arc.slnx
# because WasmBuildNative needs the wasm-tools-net10 workload.

ARG BUILDER_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG RUNTIME_IMAGE=nginx:1.27-alpine

FROM ${BUILDER_IMAGE} AS build
# Passed to the publish when set; empty keeps the version pinned in Directory.Packages.props.
ARG STREAMMARK_VERSION=
WORKDIR /src
# The SDK is the one global.json pins (11.0.100-rc.2), installed from it so the image and the repo agree.
COPY global.json .
RUN curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --jsonfile global.json --install-dir /opt/dotnet \
    && rm /tmp/dotnet-install.sh
ENV DOTNET_ROOT=/opt/dotnet \
    PATH=/opt/dotnet:$PATH \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
RUN dotnet --info && dotnet workload install wasm-tools-net10
COPY . .
RUN dotnet publish gallery/TKCode.Arc.Gallery.Browser/TKCode.Arc.Gallery.Browser.csproj \
        -c Release -o /app \
        ${STREAMMARK_VERSION:+-p:StreamMarkVersion=${STREAMMARK_VERSION}}

FROM ${RUNTIME_IMAGE}
COPY --from=build /app/wwwroot /usr/share/nginx/html
EXPOSE 80
