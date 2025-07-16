FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine@sha256:33be1326b4a2602d08e145cf7e4a8db4b243db3cac3bdec42e91aef930656080 AS build
WORKDIR /app

# Copy everything and build
COPY . .
RUN cd ./src/apps/Altinn.Register/src/Altinn.Register \
  && dotnet build Altinn.Register.csproj -c Release -o /app_output \
  && dotnet publish Altinn.Register.csproj -c Release -o /app_output

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine@sha256:89a7a398c5acaa773642cfabd6456f33e29687c1529abfaf068929ff9991cb66 AS final
EXPOSE 5020
WORKDIR /app
COPY --from=build /app_output .

# setup the user and group
# the user will have no password, using shell /bin/false and using the group dotnet
RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet
# update permissions of files if neccessary before becoming dotnet user
USER dotnet
RUN mkdir /tmp/logtelemetry

ENTRYPOINT ["dotnet", "Altinn.Register.dll"]
