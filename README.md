# Altinn Register

## Build status
[![Register build status](https://dev.azure.com/brreg/altinn-studio/_apis/build/status/altinn-platform/register-master?label=platform/register)](https://dev.azure.com/brreg/altinn-studio/_build/latest?definitionId=35)


## Getting Started

These instructions will get you a copy of the register component up and running on your machine for development and testing purposes.

### Prerequisites

1. [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Newest [Git](https://git-scm.com/downloads)
3. A code editor - we like [Visual Studio Code](https://code.visualstudio.com/download)
   - Also install [recommended extensions](https://code.visualstudio.com/docs/editor/extension-marketplace#_workspace-recommended-extensions) (e.g. [C#](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp))
4. [Podman](https://podman.io/) or another container tool such as Docker Desktop


## Running the solution locally

Clone [Altinn Register repo](https://github.com/Altinn/altinn-register) and navigate to the folder.

```bash
git clone https://github.com/Altinn/altinn-register
cd altinn-register
```


### In a docker container

To start a Register docker container

```cmd
podman compose up -d --build
```


To stop the container running Register

```cmd
podman stop altinn-register
```

The register solution is now available locally at http://localhost:5020/.
To access swagger use http://localhost:5020/swagger.

### With .NET

The Register components can be run locally when developing/debugging. Follow the install steps above if this has not already been done.

Navigate to _src/Register_, and build and run the code from there, or run the solution using you selected code editor

```cmd
dotnet run
```

The register solution is now available locally at http://localhost:5020/.
To access swagger use http://localhost:5020/swagger.
