# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ARG SERVICE=identity

WORKDIR /src

# Identity projects
COPY ["FFM.Identity.Access.Api/FFM.Identity.Access.Api.csproj", "FFM.Identity.Access.Api/"]
COPY ["FFM.Identity.Access.Domain/FFM.Identity.Access.Domain.csproj", "FFM.Identity.Access.Domain/"]
COPY ["FFM.Identity.Access.Infrastructure/FFM.Identity.Access.Infrastructure.csproj", "FFM.Identity.Access.Infrastructure/"]

# Workforce projects
COPY ["FFM.Workforce/FFM.Workforce.Api/FFM.Workforce.Api.csproj", "FFM.Workforce/FFM.Workforce.Api/"]
COPY ["FFM.Workforce/FFM.Workforce.Domain/FFM.Workforce.Domain.csproj", "FFM.Workforce/FFM.Workforce.Domain/"]
COPY ["FFM.Workforce/FFM.Workforce.Application/FFM.Workforce.Application.csproj", "FFM.Workforce/FFM.Workforce.Application/"]
COPY ["FFM.Workforce/FFM.Workforce.Infrastructure/FFM.Workforce.Infrastructure.csproj", "FFM.Workforce/FFM.Workforce.Infrastructure/"]

# GPS Tracking projects
COPY ["FFM.GPS.Tracking/FFM.GPS.Tracking.Api/FFM.GPS.Tracking.Api.csproj", "FFM.GPS.Tracking/FFM.GPS.Tracking.Api/"]
COPY ["FFM.GPS.Tracking/FFM.GPS.Tracking.Domain/FFM.GPS.Tracking.Domain.csproj", "FFM.GPS.Tracking/FFM.GPS.Tracking.Domain/"]
COPY ["FFM.GPS.Tracking/FFM.GPS.Tracking.Application/FFM.GPS.Tracking.Application.csproj", "FFM.GPS.Tracking/FFM.GPS.Tracking.Application/"]
COPY ["FFM.GPS.Tracking/FFM.GPS.Tracking.Infrastructure/FFM.GPS.Tracking.Infrastructure.csproj", "FFM.GPS.Tracking/FFM.GPS.Tracking.Infrastructure/"]

# Field Operations projects
COPY ["FFM.FieldOperations/FFM.FieldOperations.Api/FFM.FieldOperations.Api.csproj", "FFM.FieldOperations/FFM.FieldOperations.Api/"]
COPY ["FFM.FieldOperations/FFM.FieldOperations.Domain/FFM.FieldOperations.Domain.csproj", "FFM.FieldOperations/FFM.FieldOperations.Domain/"]
COPY ["FFM.FieldOperations/FFM.FieldOperations.Application/FFM.FieldOperations.Application.csproj", "FFM.FieldOperations/FFM.FieldOperations.Application/"]
COPY ["FFM.FieldOperations/FFM.FieldOperations.Infrastructure/FFM.FieldOperations.Infrastructure.csproj", "FFM.FieldOperations/FFM.FieldOperations.Infrastructure/"]

# Notification projects
COPY ["FFM.Notification/FFM.Notification.Api/FFM.Notification.Api.csproj", "FFM.Notification/FFM.Notification.Api/"]
COPY ["FFM.Notification/FFM.Notification.Domain/FFM.Notification.Domain.csproj", "FFM.Notification/FFM.Notification.Domain/"]
COPY ["FFM.Notification/FFM.Notification.Application/FFM.Notification.Application.csproj", "FFM.Notification/FFM.Notification.Application/"]
COPY ["FFM.Notification/FFM.Notification.Infrastructure/FFM.Notification.Infrastructure.csproj", "FFM.Notification/FFM.Notification.Infrastructure/"]

RUN if [ "$SERVICE" = "identity" ]; then \
        dotnet restore "FFM.Identity.Access.Api/FFM.Identity.Access.Api.csproj"; \
    elif [ "$SERVICE" = "gps" ]; then \
        dotnet restore "FFM.GPS.Tracking/FFM.GPS.Tracking.Api/FFM.GPS.Tracking.Api.csproj"; \
    elif [ "$SERVICE" = "fieldoperations" ]; then \
        dotnet restore "FFM.FieldOperations/FFM.FieldOperations.Api/FFM.FieldOperations.Api.csproj"; \
    elif [ "$SERVICE" = "notification" ]; then \
        dotnet restore "FFM.Notification/FFM.Notification.Api/FFM.Notification.Api.csproj"; \
    else \
        dotnet restore "FFM.Workforce/FFM.Workforce.Api/FFM.Workforce.Api.csproj"; \
    fi

COPY . .

RUN if [ "$SERVICE" = "identity" ]; then \
        dotnet publish "FFM.Identity.Access.Api/FFM.Identity.Access.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false; \
    elif [ "$SERVICE" = "gps" ]; then \
        dotnet publish "FFM.GPS.Tracking/FFM.GPS.Tracking.Api/FFM.GPS.Tracking.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false; \
        cp "FFM.GPS.Tracking/FFM.GPS.Tracking.Api/identity-public.key" /app/publish/identity-public.key; \
    elif [ "$SERVICE" = "fieldoperations" ]; then \
        dotnet publish "FFM.FieldOperations/FFM.FieldOperations.Api/FFM.FieldOperations.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false; \
        cp "FFM.FieldOperations/FFM.FieldOperations.Api/identity-public.key" /app/publish/identity-public.key; \
    elif [ "$SERVICE" = "notification" ]; then \
        dotnet publish "FFM.Notification/FFM.Notification.Api/FFM.Notification.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false; \
        cp "FFM.Notification/FFM.Notification.Api/identity-public.key" /app/publish/identity-public.key; \
        if [ -f "FFM.Notification/FFM.Notification.Api/firebase-service-account.json" ]; then cp "FFM.Notification/FFM.Notification.Api/firebase-service-account.json" /app/publish/firebase-service-account.json; fi; \
    else \
        dotnet publish "FFM.Workforce/FFM.Workforce.Api/FFM.Workforce.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false; \
        cp "FFM.Workforce/FFM.Workforce.Api/identity-public.key" /app/publish/identity-public.key; \
    fi

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

ARG SERVICE=identity
ENV SERVICE=${SERVICE}

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 80

ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["sh", "-c", "if [ \"$SERVICE\" = \"identity\" ]; then exec dotnet FFM.Identity.Access.Api.dll; elif [ \"$SERVICE\" = \"gps\" ]; then exec dotnet FFM.GPS.Tracking.Api.dll; elif [ \"$SERVICE\" = \"fieldoperations\" ]; then exec dotnet FFM.FieldOperations.Api.dll; elif [ \"$SERVICE\" = \"notification\" ]; then exec dotnet FFM.Notification.Api.dll; else exec dotnet FFM.Workforce.Api.dll; fi"]
