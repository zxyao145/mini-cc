
FROM node:22-alpine as base

RUN npm config set registry https://registry.npmmirror.com \
    && npm install -g pnpm \
    && pnpm config set registry https://registry.npmmirror.com


# --- 编译 ---
FROM base as builder

WORKDIR /app

COPY package.json ./
COPY . .
RUN ls -lah && pnpm install --prod
RUN cat ./tsconfig.json && ls -lah ./src/components && pnpm run build


# --- 生产阶段 ---
FROM base AS runner

# 启用非 root 用户（更安全）
RUN addgroup --system --gid 1001 nodejs \
  && adduser --system --uid 1001 nextjs

WORKDIR /app

# 拷贝运行所需文件
COPY --from=builder /app/.next ./.next
COPY --from=builder /app/public ./public
COPY --from=builder /app/package.json ./package.json
COPY --from=builder /app/node_modules ./node_modules
# COPY --from=builder /app/next.config.js ./next.config.js

# 环境变量（可选）
ENV NODE_ENV production
ENV PORT 3000

# 暴露端口
EXPOSE 3000

# 切换用户
USER nextjs

# 启动 Next.js
CMD ["pnpm", "start"]
