# Gallery WebAssembly host: publish in the SDK image, serve the static output with nginx.
# Built by .github/workflows/docker-gallery.yml, not locally. The project is not in TKCode.Arc.slnx
# because WasmBuildNative needs the wasm-tools-net10 workload.

ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:11.0-preview
ARG RUNTIME_IMAGE=nginx:1.27-alpine

FROM ${SDK_IMAGE} AS build
# Passed to the publish when set; empty keeps the version pinned in Directory.Packages.props.
ARG STREAMMARK_VERSION=
WORKDIR /src
RUN dotnet workload install wasm-tools-net10
COPY . .
RUN dotnet publish gallery/TKCode.Arc.Gallery.Browser/TKCode.Arc.Gallery.Browser.csproj \
        -c Release -o /app \
        ${STREAMMARK_VERSION:+-p:StreamMarkVersion=${STREAMMARK_VERSION}}

FROM ${RUNTIME_IMAGE}
COPY --from=build /app/wwwroot /usr/share/nginx/html
EXPOSE 80
