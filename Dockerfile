FROM node:24-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build && npm run server:build
FROM node:24-alpine
WORKDIR /app
ENV NODE_ENV=production PORT=8788 ALLOW_TEST_ROOMS=false
COPY package*.json ./
RUN npm ci --omit=dev
COPY --from=build /app/dist ./dist
COPY --from=build /app/server-dist ./server-dist
USER node
EXPOSE 8788
CMD ["node","server-dist/server/index.js"]
