FROM mcr.microsoft.com/dotnet/sdk:8.0.203-alpine3.18 AS build
WORKDIR Altinn.Register/

COPY src/Altinn.Register ./Altinn.Register
WORKDIR Altinn.Register/

RUN dotnet build Altinn.Register.csproj -c Release -o /app_output
RUN dotnet publish Altinn.Register.csproj -c Release -o /app_output

FROM mcr.microsoft.com/dotnet/aspnet:8.0.3-alpine3.18 AS final
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
