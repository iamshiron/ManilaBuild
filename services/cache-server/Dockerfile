# Stage 1: Base image with Bun
FROM oven/bun:1.3.0-alpine AS base
WORKDIR /app

# Stage 2: Install dependencies (using Turborepo for pruning)
FROM base AS installer
RUN apk add --no-cache libc6-compat

# Copy workspace configuration
COPY package.json bun.lock turbo.json ./
COPY services/cache-server/package.json ./services/cache-server/

# Install dependencies
RUN bun install --frozen-lockfile

# Stage 3: Build the application
FROM base AS builder
WORKDIR /app

# Copy dependencies from installer
COPY --from=installer /app/node_modules ./node_modules
COPY --from=installer /app/services/cache-server/node_modules ./services/cache-server/node_modules

# Copy source code
COPY package.json bun.lock turbo.json ./
COPY services/cache-server ./services/cache-server

# Build using Turborepo
RUN bun run build --filter=cache-server

# Stage 4: Production runner
FROM base AS runner
WORKDIR /app

# Don't run as root
RUN addgroup --system --gid 1001 nodejs && \
    adduser --system --uid 1001 bunuser

# Copy built application
COPY --from=builder --chown=bunuser:nodejs /app/services/cache-server/dist ./dist
COPY --from=builder --chown=bunuser:nodejs /app/services/cache-server/package.json ./package.json

# Install production dependencies only
RUN bun install --production --frozen-lockfile

USER bunuser

# Expose port
EXPOSE 3000

ENV NODE_ENV=production
ENV PORT=3000

# Start the application
CMD ["bun", "run", "start"]
