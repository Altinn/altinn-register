FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:0191ff386e93923edf795d363ea0ae0669ce467ada4010b370644b670fa495c1 AS build
WORKDIR /app

# Copy everything and build
COPY . .
RUN cd ./src/apps/Altinn.Register/src/Altinn.Register \
    && dotnet build Altinn.Register.csproj -c Release -o /app_output \
    && dotnet publish Altinn.Register.csproj -c Release -o /app_output

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine@sha256:99a749b0dadd9e11d30d3804d94c8f1edb06db00148df52814219d5ff838f551 AS final
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
