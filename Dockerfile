# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY *.sln ./
COPY src/THWTicketApp.Shared/*.csproj src/THWTicketApp.Shared/
COPY src/THWTicketApp.Web/*.csproj src/THWTicketApp.Web/
RUN dotnet restore

COPY . .
RUN dotnet publish src/THWTicketApp.Web/THWTicketApp.Web.csproj \
    --configuration Release \
    --output /app/publish

# Stage 2: Serve with Nginx
FROM nginx:alpine
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY nginx.conf /etc/nginx/nginx.conf

EXPOSE 80
