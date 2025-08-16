FROM node:22-alpine

# 设置环境变量
ENV NODE_ENV=production
ENV PORT=5002

# 设置工作目录
WORKDIR /app

  # 复制 package 文件
COPY ./ ./

RUN npm ci --only=production && npm cache clean --force 

# 创建非 root 用户
RUN addgroup -S appgroup && adduser -S appuser -G appgroup

# 更改文件权限
RUN chown -R appuser:appgroup /app

# 切换用户
USER appuser

# 暴露端口
EXPOSE $PORT

# 启动应用
CMD ["npm", "start"]