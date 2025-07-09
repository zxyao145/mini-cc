# 请参阅 https://aka.ms/customizecontainer 以了解如何自定义调试容器，以及 Visual Studio 如何使用此 Dockerfile 生成映像以更快地进行调试。

# 此阶段用于在快速模式(默认为调试配置)下从 VS 运行时
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base

# 重建 apt 源列表为阿里云
RUN rm -rf /etc/apt/sources.list.d/* && \
    echo "deb https://mirrors.aliyun.com/debian/ bookworm main contrib non-free non-free-firmware\n\
deb-src https://mirrors.aliyun.com/debian/ bookworm main contrib non-free non-free-firmware\n\
deb https://mirrors.aliyun.com/debian-security bookworm-security main contrib non-free non-free-firmware\n\
deb-src https://mirrors.aliyun.com/debian-security bookworm-security main contrib non-free non-free-firmware\n\
deb https://mirrors.aliyun.com/debian/ bookworm-updates main contrib non-free non-free-firmware\n\
deb-src https://mirrors.aliyun.com/debian/ bookworm-updates main contrib non-free non-free-firmware\n\
deb https://mirrors.aliyun.com/debian/ bookworm-backports main contrib non-free non-free-firmware\n\
deb-src https://mirrors.aliyun.com/debian/ bookworm-backports main contrib non-free non-free-firmware" \
> /etc/apt/sources.list && \
    apt-get update && apt-get clean && \
    apt install curl wget -y


USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# 此阶段用于生成服务项目
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/backend/MiniCc.Api/MiniCc.Api.csproj", "src/backend/MiniCc.Api/"]
COPY ["src/backend/ContentHandler/ContentHandler.csproj", "src/backend/ContentHandler/"]
RUN dotnet restore "./src/backend/MiniCc.Api/MiniCc.Api.csproj"
COPY . .
WORKDIR "/src/src/backend/MiniCc.Api"
RUN dotnet build "./MiniCc.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 此阶段用于发布要复制到最终阶段的服务项目
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MiniCc.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 此阶段在生产中使用，或在常规模式下从 VS 运行时使用(在不使用调试配置时为默认值)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MiniCc.Api.dll"]