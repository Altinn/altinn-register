FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:5c559aa5d99337e400d39ab4fa1f6979d126c29b20939d53658ed38300571e74 AS build
WORKDIR /app

# Copy everything and build
COPY . .
RUN cd ./src/apps/Altinn.Register/src/Altinn.Register \
    && dotnet build Altinn.Register.csproj -c Release -o /app_output \
    && dotnet publish Altinn.Register.csproj -c Release -o /app_output

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine@sha256:284e214f985b52d8a7e35d1b319109bc4ba76fdf50e58e8cabefba4bd9cd4dc0 AS final
EXPOSE 5020
WORKDIR /app
COPY --from=build /app_output .

# Install tzdata for timezone support, which is needed for correct handling of dates and times in the application
RUN apk add --no-cache tzdata

# setup the user and group
# the user will have no password, using shell /bin/false and using the group dotnet
RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet
# update permissions of files if neccessary before becoming dotnet user
USER dotnet
RUN mkdir /tmp/logtelemetry

ENTRYPOINT ["dotnet", "Altinn.Register.dll"]
