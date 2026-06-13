# Docker 部署指南

本文档说明如何使用 Docker 部署 WMS Core Web API。

## 前置要求

- Docker Engine 20.10+
- Docker Compose 2.0+

## 快速开始

### 1. 开发环境部署

```bash
# 进入 docker 目录
cd docker

# 启动所有服务
docker-compose up -d

# 查看日志
docker-compose logs -f wms-api

# 停止所有服务
docker-compose down
```

服务将在以下端口可用:
- **WMS API**: http://localhost:5000
- **SQL Server**: localhost:1433
- **Redis**: localhost:6379

### 2. 生产环境部署

```bash
# 使用生产配置启动
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# 查看服务状态
docker-compose ps

# 查看资源使用情况
docker stats
```

## 环境变量

生产环境需要配置以下环境变量（创建 `.env` 文件）:

```env
# JWT 密钥（必须修改）
JWT_SECRET=CHANGE_ME_USE_A_STRONG_RANDOM_SECRET_32_CHARS_MIN

# Redis 密码（必须修改）
REDIS_PASSWORD=CHANGE_ME_REDIS_PASSWORD

# 数据库密码（必须修改）
SA_PASSWORD=CHANGE_ME_SQL_PASSWORD
```

## 健康检查

### API 健康检查

```bash
# 简单健康检查（返回状态码）
curl http://localhost:5000/healthz

# 详细健康检查（返回 JSON）
curl http://localhost:5000/health
```

### 容器健康状态

```bash
docker-compose ps
```

## 数据持久化

数据存储在以下 Docker 卷中:
- `sqlserver-data`: SQL Server 数据库文件
- `redis-data`: Redis 持久化数据
- `wms-logs`: 应用程序日志
- `wms-data`: 应用程序数据

### 备份数据

```bash
# 备份 SQL Server
docker exec wms-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "BACKUP DATABASE [WmsDb] TO DISK = N'/var/opt/mssql/backup/WmsDb.bak' WITH FORMAT"

# 复制备份文件
docker cp wms-sqlserver:/var/opt/mssql/backup/WmsDb.bak ./backup/
```

### 恢复数据

```bash
# 复制备份文件到容器
docker cp ./backup/WmsDb.bak wms-sqlserver:/var/opt/mssql/backup/

# 恢复数据库
docker exec wms-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "RESTORE DATABASE [WmsDb] FROM DISK = N'/var/opt/mssql/backup/WmsDb.bak' WITH REPLACE"
```

## 日志

### 查看日志

```bash
# 查看所有服务日志
docker-compose logs -f

# 查看特定服务日志
docker-compose logs -f wms-api
docker-compose logs -f sqlserver
docker-compose logs -f redis
```

### 日志位置

- 应用日志: `wms-logs` Docker 卷，容器内路径: `/app/logs`
- SQL Server 日志: 容器内路径: `/var/opt/mssql/log`
- Redis 日志: 容器内路径: `/var/log/redis/`

## 性能优化

### 资源限制

生产环境配置了以下资源限制:
- **WMS API**: 2 CPU, 1GB RAM（最大）
- **SQL Server**: 2 CPU, 2GB RAM（最大）
- **Redis**: 1 CPU, 1GB RAM（最大）

可根据实际负载调整 `docker-compose.prod.yml` 中的资源配置。

### 扩展

```bash
# 扩展 WMS API 到 3 个实例
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d --scale wms-api=3
```

## 监控

### 查看容器资源使用

```bash
docker stats
```

### 查看容器详细信息

```bash
docker inspect wms-api
```

## 故障排除

### 容器无法启动

```bash
# 查看详细日志
docker-compose logs wms-api

# 检查容器状态
docker-compose ps -a
```

### 数据库连接失败

```bash
# 检查 SQL Server 容器
docker exec wms-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" -C -Q "SELECT @@VERSION"

# 检查网络连接
docker network inspect wms_wms-network
```

### Redis 连接失败

```bash
# 测试 Redis 连接
docker exec wms-redis redis-cli -a "$REDIS_PASSWORD" ping
```

## 更新部署

```bash
# 拉取最新代码
git pull

# 重新构建镜像
docker-compose build

# 重启服务
docker-compose up -d
```

## 清理

```bash
# 停止并删除所有容器、网络
docker-compose down

# 删除所有容器、网络、卷
docker-compose down -v

# 清理未使用的镜像
docker image prune -a
```

## 安全建议

1. **修改所有默认密码**
   - SQL Server SA 密码
   - Redis 密码
   - JWT 密钥

2. **启用 HTTPS**
   - 在生产环境中设置 `Security__RequireHttps=true`
   - 配置有效的 SSL 证书

3. **限制网络访问**
   - 使用内部网络
   - 只暴露必要的端口

4. **定期更新**
   - 及时更新 Docker 镜像
   - 定期检查安全漏洞

5. **备份策略**
   - 定期备份数据库
   - 备份应用程序配置
   - 测试恢复流程
