FROM node:20-alpine AS build

WORKDIR /app
COPY package*.json ./
RUN --mount=type=cache,target=/root/.npm npm ci
COPY . .

FROM node:20-alpine AS runtime

WORKDIR /app
COPY --from=build /app /app

ENV NODE_ENV=production

USER node

ENTRYPOINT ["node","app.ts"]
