# Stage 1: Base image with Bun
FROM oven/bun:1-alpine AS base
WORKDIR /app

COPY package.json .
COPY src ./src
COPY tsconfig.json .

RUN bun install

FROM oven/bun:1-alpine AS build
WORKDIR /app

COPY --from=base /app/node_modules ./node_modules
COPY --from=base /app/src ./src
COPY --from=base /app/package.json .
COPY --from=base /app/tsconfig.json .
RUN bun run build

FROM oven/bun:1-alpine AS production
WORKDIR /app

COPY --from=build /app/dist ./dist
COPY --from=base /app/node_modules ./node_modules
COPY --from=base /app/package.json .

ENV NODE_ENV=production
ENV PORT=2995

EXPOSE 2995

CMD ["bun", "run", "start"]
