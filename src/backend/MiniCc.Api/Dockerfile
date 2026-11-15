# 请参阅 https://aka.ms/customizecontainer 以了解如何自定义调试容器，以及 Visual Studio 如何使用此 Dockerfile 生成映像以更快地进行调试。

# 此阶段用于在快速模式(默认为调试配置)下从 VS 运行时
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

# 使用阿里云 Ubuntu 源
RUN sed -i -e 's|http://archive.ubuntu.com|http://mirrors.aliyun.com|g' \
           -e 's|http://security.ubuntu.com|http://mirrors.aliyun.com|g' \
           -e 's|http://ports.ubuntu.com|http://mirrors.aliyun.com|g' \
        /etc/apt/sources.list.d/ubuntu.sources && \
    apt-get update && apt-get install -y curl wget && apt-get clean


RUN mkdir -p /app/data && chown $APP_UID /app/data
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# 此阶段用于生成服务项目
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/backend/MiniCc.Api/MiniCc.Api.csproj", "src/backend/MiniCc.Api/"]
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