FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:7d98d5883675c6bca25b1db91f393b24b85125b5b00b405e55404fd6b8d2aead AS build
WORKDIR /app

# Copy everything and build
COPY . .
RUN cd ./src/apps/Altinn.Register/src/Altinn.Register \
  && dotnet build Altinn.Register.csproj -c Release -o /app_output \
  && dotnet publish Altinn.Register.csproj -c Release -o /app_output

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine@sha256:c60d76bf35b268f7bab7d5f4e5405c89dcc45616453004e89baea303677ca635 AS final
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
