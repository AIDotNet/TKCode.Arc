# Gallery WebAssembly host: publish in a container, pre-compress the output, serve it with nginx (brotli + gzip).
# Built by .github/workflows/docker-gallery.yml, not locally. The project is not in TKCode.Arc.slnx
# because WasmBuildNative needs the wasm-tools-net10 workload.

ARG BUILDER_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG RUNTIME_BASE=alpine:3.21
ARG NGINX_VERSION=1.26.3

FROM ${BUILDER_IMAGE} AS build
# Passed to the publish when set; empty keeps the version pinned in Directory.Packages.props.
ARG STREAMMARK_VERSION=
# python3 is needed by WasmBuildNative (Emscripten); brotli and gzip compress the output below.
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 brotli gzip \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src
# The SDK is the one global.json pins (11.0.100-rc.2). The .NET 10 runtime is needed too: gallery/TKCode.Arc.Gallery.Llms (run below) targets net10.0.
COPY global.json .
RUN curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --jsonfile global.json --install-dir /opt/dotnet \
    && bash /tmp/dotnet-install.sh --runtime dotnet --channel 10.0 --install-dir /opt/dotnet \
    && rm /tmp/dotnet-install.sh
ENV DOTNET_ROOT=/opt/dotnet \
    PATH=/opt/dotnet:$PATH \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
RUN dotnet --info && dotnet workload install wasm-tools-net10
COPY . .
# Trimmed (compiling ahead of time to WebAssembly is off: -p:ArcAot=true, see the Browser project file).
RUN dotnet publish gallery/TKCode.Arc.Gallery.Browser/TKCode.Arc.Gallery.Browser.csproj \
        -c Release -o /app \
        ${STREAMMARK_VERSION:+-p:StreamMarkVersion=${STREAMMARK_VERSION}}
# The files for AI assistants next to the app: llms.txt, llms-full.txt, every docs page as Markdown (English, zh-CN, zh-TW)
# and the agent skill, with links on the site's fixed address (gallery/TKCode.Arc.Gallery.Llms).
RUN dotnet run --project gallery/TKCode.Arc.Gallery.Llms -c Release -- /app/wwwroot --base-url https://docs.tkcode.si
# Pre-compress the text-like files: nginx serves *.br (brotli_static) and *.gz (gzip_static) without compressing per request.
# The *.dll files are the assemblies the publish loads on demand (wwwroot/_lazy, see the Browser project file); the *.otf file is the Chinese font;
# the *.md and *.txt files are the pages for AI assistants.
RUN cd /app/wwwroot \
    && find . -type f \( -name '*.js' -o -name '*.wasm' -o -name '*.dat' -o -name '*.json' -o -name '*.html' \
                         -o -name '*.css' -o -name '*.map' -o -name '*.svg' -o -name '*.xml' -o -name '*.dll' -o -name '*.otf' \
                         -o -name '*.md' -o -name '*.txt' \) -print0 \
       | xargs -0 -P "$(nproc)" -I{} sh -c 'brotli -q 11 -c "{}" > "{}.br" && gzip -9 -c "{}" > "{}.gz"'

# nginx with the brotli module, built from source: Alpine ships no nginx-mod-http-brotli package.
FROM ${RUNTIME_BASE} AS nginx-build
ARG NGINX_VERSION
RUN apk add --no-cache build-base pcre2-dev zlib-dev openssl-dev linux-headers brotli-dev git curl
WORKDIR /src
RUN curl -sSL https://nginx.org/download/nginx-${NGINX_VERSION}.tar.gz | tar -xz \
    && git clone --depth 1 --recurse-submodules --shallow-submodules https://github.com/google/ngx_brotli.git
WORKDIR /src/nginx-${NGINX_VERSION}
RUN ./configure \
        --prefix=/etc/nginx \
        --sbin-path=/usr/sbin/nginx \
        --conf-path=/etc/nginx/nginx.conf \
        --error-log-path=/var/log/nginx/error.log \
        --http-log-path=/var/log/nginx/access.log \
        --pid-path=/var/run/nginx.pid \
        --lock-path=/var/run/nginx.lock \
        --http-client-body-temp-path=/var/cache/nginx/client_temp \
        --http-proxy-temp-path=/var/cache/nginx/proxy_temp \
        --http-fastcgi-temp-path=/var/cache/nginx/fastcgi_temp \
        --http-uwsgi-temp-path=/var/cache/nginx/uwsgi_temp \
        --http-scgi-temp-path=/var/cache/nginx/scgi_temp \
        --user=nginx --group=nginx \
        --with-http_gzip_static_module --with-http_stub_status_module --with-threads \
        --add-module=/src/ngx_brotli \
    && make -j"$(nproc)" \
    && make install

FROM ${RUNTIME_BASE}
RUN apk add --no-cache pcre2 libssl3 zlib brotli-libs ca-certificates \
    && addgroup -S nginx \
    && adduser -S -D -H -G nginx nginx \
    && mkdir -p /var/cache/nginx /var/log/nginx \
    && ln -sf /dev/stdout /var/log/nginx/access.log \
    && ln -sf /dev/stderr /var/log/nginx/error.log
COPY --from=nginx-build /usr/sbin/nginx /usr/sbin/nginx
COPY --from=nginx-build /etc/nginx/mime.types /etc/nginx/mime.types
COPY docker/nginx.conf /etc/nginx/nginx.conf
COPY --from=build /app/wwwroot /usr/share/nginx/html
EXPOSE 80
STOPSIGNAL SIGQUIT
CMD ["nginx", "-g", "daemon off;"]
