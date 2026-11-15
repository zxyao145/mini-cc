FROM node:22-alpine AS workspace
WORKDIR /app

COPY package.json .npmrc ./
RUN npm install --global --userconfig /app/.npmrc "$(node -p 'require("./package.json").packageManager')"

COPY pnpm-lock.yaml pnpm-workspace.yaml ./
COPY src/content-handler/package.json ./src/content-handler/
COPY src/web-extension/package.json ./src/web-extension/

FROM workspace AS builder
ARG NEXT_PUBLIC_API_URL
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL

RUN pnpm --filter . --include-workspace-root install --frozen-lockfile
COPY src/web/ ./src/web/
RUN pnpm build:web

FROM workspace AS runner
RUN pnpm --filter . --include-workspace-root install --prod --frozen-lockfile \
    && addgroup --system --gid 1001 nodejs \
    && adduser --system --uid 1001 nextjs

COPY --from=builder --chown=nextjs:nodejs /app/src/web/.next ./src/web/.next
COPY --from=builder /app/src/web/public ./src/web/public
COPY --from=builder /app/src/web/next.config.ts /app/src/web/tsconfig.json ./src/web/

ENV NODE_ENV=production PORT=3000
EXPOSE 3000
USER nextjs
CMD ["node", "node_modules/next/dist/bin/next", "start", "src/web"]
