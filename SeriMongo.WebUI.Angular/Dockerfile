FROM node:18-alpine AS build
WORKDIR /app
COPY . .
RUN npm install
RUN npm run build

# Serve Application using Nginx Server
FROM nginx:alpine
COPY --from=build /app/dist/seri-mongo.web-ui.angular/ /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
