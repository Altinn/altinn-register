FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:2b0e46d490f5b53a8dc07fbf636cdf5b90796878a256e1ce5b441e8d9675c5f4 AS build
WORKDIR /app

# Copy everything and build
COPY . .
RUN cd ./src/apps/Altinn.Register/src/Altinn.Register \
    && dotnet build Altinn.Register.csproj -c Release -o /app_output \
    && dotnet publish Altinn.Register.csproj -c Release -o /app_output

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine@sha256:86883194752e8cb9c3914edea40961cc179da6804fcfe814f18f89ed6515d8cf AS final
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
