FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine@sha256:f47429a125e38d83f5231a78dde18106cb447d541f7ffdc5b8af4d227a323d95 AS build
WORKDIR /app

# Copy everything and build
COPY . .
RUN cd ./src/apps/Altinn.Register/src/Altinn.Register \
  && dotnet build Altinn.Register.csproj -c Release -o /app_output \
  && dotnet publish Altinn.Register.csproj -c Release -o /app_output

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:1c72a277a600751bfd9ad69f33ba41f574ef8237d6a03745a12266179956df0e AS final
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
