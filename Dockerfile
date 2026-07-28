FROM node:20-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# Excludes backend/ and other non-frontend content via .dockerignore.
# Vite bakes VITE_* env vars into the bundle at build time, not runtime — so these have to be real
# env vars during `npm run build`, not just passed to the running container (which is why they're
# ARGs promoted to ENV here, rather than left as plain docker-compose `environment:` on a runtime
# service — this is a static file build, there is no running Node process to read them later).
ARG VITE_AAD_CLIENT_ID
ARG VITE_AAD_TENANT_ID
ARG VITE_AAD_API_CLIENT_ID
ENV VITE_AAD_CLIENT_ID=$VITE_AAD_CLIENT_ID
ENV VITE_AAD_TENANT_ID=$VITE_AAD_TENANT_ID
ENV VITE_AAD_API_CLIENT_ID=$VITE_AAD_API_CLIENT_ID
RUN npm run build

FROM nginx:alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
