# ==========================================
# ЭТАП 1: Сборка (используем Alpine для совместимости)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH=x64

WORKDIR /src

# Копируем .csproj для кэширования NuGet-зависимостей
COPY ["ActivityCountBot/ActivityCountBot.csproj", "ActivityCountBot/"]
RUN dotnet restore "ActivityCountBot/ActivityCountBot.csproj" \
    -r linux-musl-${TARGETARCH}

# Копируем весь код
COPY . .
WORKDIR "/src/ActivityCountBot"

# Self-contained publish с trimming и single-file
# linux-musl-x64 = Alpine (musl libc), linux-x64 = Debian/Ubuntu (glibc)
RUN dotnet publish "ActivityCountBot.csproj" \
    -c $BUILD_CONFIGURATION \
    -r linux-musl-${TARGETARCH} \
    --self-contained true \
    -o /app/publish \
    /p:PublishSingleFile=true \
    /p:InvariantGlobalization=false \
    /p:IncludeNativeLibrariesForSelfExtract=true

# ==========================================
# ЭТАП 2: Финальный образ (runtime-deps = только нативные библиотеки)
# ==========================================
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine AS final

# Устанавливаем ICU для корректной работы с эмодзи и культурами
# (критично для Telegram: имена пользователей, текст сообщений)
RUN apk add --no-cache \
    icu-libs \
    krb5-libs \
    libgcc \
    libssl3 \
    libstdc++ \
    zlib

# Non-root пользователь
RUN adduser --disabled-password --gecos "" appuser
USER appuser

WORKDIR /app
COPY --from=build /app/publish .

# Если PublishSingleFile=true, бинарник называется по имени проекта
RUN ls -la /app

# Healthcheck: проверяем, что процесс жив
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s \
    CMD pgrep -f "ActivityCountBot" || exit 1

# Для single-file запускаем напрямую бинарник, не через dotnet
ENTRYPOINT ["./ActivityCountBot"]