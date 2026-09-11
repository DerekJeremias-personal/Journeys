FROM alpine:3 AS base
WORKDIR /app
EXPOSE 80
ENV \
    # Configure web servers to bind to port 8080 when present
    ASPNETCORE_HTTP_PORTS=80 \
    # Enable detection of running in a container
    DOTNET_RUNNING_IN_CONTAINER=true \
    # Set the invariant mode since ICU package isn't included (see https://github.com/dotnet/announcements/issues/20)
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

FROM base AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN apk add --no-cache dotnet8-sdk
RUN dotnet publish Journeys/Journeys.API/Journeys.API.csproj -c $BUILD_CONFIGURATION -o /app/publish --self-contained true

FROM base AS final
# .NET dependencies for self-contained apps
COPY --from=build /app/publish .
RUN apk add --no-cache libgcc libssl3 libstdc++ zlib
RUN echo "#!/bin/sh" > ./entrypoint.sh && echo "./Journeys.API" >> ./entrypoint.sh && chmod +x ./entrypoint.sh
ENTRYPOINT ["./entrypoint.sh"]
