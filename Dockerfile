FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine@sha256:430bd56f4348f9dd400331f0d71403554ec83ae1700a7dcfe1e1519c9fd12174 AS build
WORKDIR /app

# Copy everything and build
COPY . .
RUN cd ./src/apps/Altinn.Register/src/Altinn.Register \
  && dotnet build Altinn.Register.csproj -c Release -o /app_output \
  && dotnet publish Altinn.Register.csproj -c Release -o /app_output

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine@sha256:3fce6771d84422e2396c77267865df61174a3e503c049f1fe242224c012fde65 AS final
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
