# Cross platform shebang:
shebang := if os() == 'windows' {
  'pwsh.exe'
} else {
  '/usr/bin/env pwsh'
}

# Set shell for non-Windows OSs:
set shell := ["pwsh", "-CommandWithArgs"]

# Set shell for Windows OSs:
set windows-shell := ["pwsh.exe", "-NoLogo", "-CommandWithArgs"]

[private]
@default:
  just --choose

# Install node packages required to run scripts - uses pnpm to install the packages
[private]
@install-script-packages:
  #!{{shebang}}
  pushd .github/scripts
  pnpm install

[private]
@install-script-packages-frozen:
  #!{{shebang}}
  pushd .github/scripts
  pnpm install --frozen-lockfile

# Run register locally
@start:
  #!{{shebang}}
  dotnet run --project src/apps/Altinn.Register/src/Altinn.Register.AppHost/

# Print all projects metadata
@get-metadata: install-script-packages-frozen
  #!{{shebang}}
  node ./.github/scripts/get-metadata.mts

# Run the script to update solution files
@update-sln-files *ARGS: install-script-packages-frozen
  #!{{shebang}}
  node ./.github/scripts/update-sln-files.mts -- {{ARGS}}

@generate-guardianship-migration FILE: install-script-packages-frozen
  #!{{shebang}}
  node ./.github/scripts/generate-guardianship-migration.mts > "{{FILE}}"

@generate-guardianship-code: install-script-packages-frozen
  #!{{shebang}}
  node ./.github/scripts/generate-guardianship-code.mts > "./src/apps/Altinn.Register/src/Altinn.Register.Integrations.Npr/Guardianships.g.cs"
  node ./.github/scripts/generate-guardianship-tests.mts > "./src/apps/Altinn.Register/test/Altinn.Register.Tests/PartyImport/Npr/GuardianshipRoleMapperTests.g.cs"

@fetch-tenor-id ID:
  #!{{shebang}}
  $null = New-Item -Type Directory -Force "src/apps/Altinn.Register/test/Altinn.Register.Tests/Testdata/Npr/Persons/valid/{{ID}}"
  $response = Invoke-WebRequest -Uri "http://localhost:5020/register/api/v0/debug/npr/person/{{ID}}"
  $response.Content | prettier --parser json | Out-File -FilePath "src/apps/Altinn.Register/test/Altinn.Register.Tests/Testdata/Npr/Persons/valid/{{ID}}/npr.json"
