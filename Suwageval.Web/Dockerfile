FROM docker.io/library/node:26-alpine AS builder

ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0

WORKDIR /app

COPY Suwageval.Web/package.json Suwageval.Web/pnpm-lock.yaml Suwageval.Web/pnpm-workspace.yaml ./
RUN corepack enable && pnpm install --frozen-lockfile

COPY Suwageval.Web/ ./
RUN pnpm exec vite build

FROM docker.io/nginxinc/nginx-unprivileged:1.31-alpine

ENV NGINX_ENVSUBST_FILTER="^(API_UPSTREAM|NGINX_LOCAL_RESOLVERS)$"
ENV NGINX_ENTRYPOINT_LOCAL_RESOLVERS=1
ENV API_UPSTREAM="http://server:6230"

COPY --from=builder /app/dist /usr/share/nginx/html
COPY Suwageval.Web/nginx.conf.template /etc/nginx/templates/default.conf.template

EXPOSE 8080
