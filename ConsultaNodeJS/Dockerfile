# syntax=docker/dockerfile:1

# ---- Etapa de build ----
FROM node:20-alpine AS build
WORKDIR /src
COPY package.json .
RUN npm install --omit=dev
COPY . .

# ---- Etapa final ----
FROM node:20-alpine AS final
WORKDIR /app
COPY --from=build /src .

ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["node", "index.js"]
