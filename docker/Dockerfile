# 多阶段构建 - 构建阶段
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制项目文件
COPY ["src/Wms.Core.WebApi/Wms.Core.WebApi.csproj", "Wms.Core.WebApi/"]
COPY ["src/Wms.Core.Domain/Wms.Core.Domain.csproj", "Wms.Core.Domain/"]
COPY ["src/Wms.Core.Infrastructure/Wms.Core.Infrastructure.csproj", "Wms.Core.Infrastructure/"]
COPY ["src/Wms.Core.Application/Wms.Core.Application.csproj", "Wms.Core.Application/"]

# 还原依赖
RUN dotnet restore "Wms.Core.WebApi/Wms.Core.WebApi.csproj"

# 复制所有源代码
COPY . .

# 构建项目
WORKDIR "/src/Wms.Core.WebApi"
RUN dotnet build "Wms.Core.WebApi.csproj" -c Release -o /app/build

# 发布项目
FROM build AS publish
RUN dotnet publish "Wms.Core.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 运行阶段
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# 安装时区数据
RUN apt-get update && \
    apt-get install -y tzdata && \
    ln -sf /usr/share/zoneinfo/Asia/Shanghai /etc/localtime && \
    echo "Asia/Shanghai" > /etc/timezone && \
    dpkg-reconfigure -f noninteractive tzdata && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# 创建非 root 用户
RUN groupadd -r wmsuser && useradd -r -g wmsuser wmsuser

# 复制发布的文件
COPY --from=publish /app/publish .

# 创建必要的目录
RUN mkdir -p /app/logs /app/data && \
    chown -R wmsuser:wmsuser /app

# 切换到非 root 用户
USER wmsuser

# 暴露端口
EXPOSE 80
EXPOSE 443

# 健康检查
HEALTHCHECK --interval=30s --timeout=3s --start-period=40s --retries=3 \
    CMD curl -f http://localhost/healthz || exit 1

# 启动应用
ENTRYPOINT ["dotnet", "Wms.Core.WebApi.dll"]
