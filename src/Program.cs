using System;
using System.IO;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Altinn.Common.AccessToken;
using Altinn.Common.AccessToken.Configuration;
using Altinn.Common.AccessToken.Services;
using Altinn.Register.Configuration;
using Altinn.Register.Core;
using Altinn.Register.Filters;
using Altinn.Register.Health;
using Altinn.Register.Services;
using Altinn.Register.Services.Implementation;
using Altinn.Register.Services.Interfaces;

using AltinnCore.Authentication.JwtCookie;

using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureKeyVault;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

ILogger logger;

string vaultApplicationInsightsKey = "ApplicationInsights--InstrumentationKey";

string applicationInsightsConnectionString = string.Empty;

var builder = WebApplication.CreateBuilder(args);

ConfigureSetupLogging();

await SetConfigurationProviders(builder.Configuration);

ConfigureLogging(builder.Logging);

ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

Configure();

app.Run();

async Task SetConfigurationProviders(ConfigurationManager config)
{
    string basePath = Directory.GetParent(Directory.GetCurrentDirectory()).FullName;
    config.SetBasePath(basePath);
    config.AddJsonFile(basePath + "altinn-appsettings/altinn-dbsettings-secret.json", optional: true, reloadOnChange: true);
    if (basePath == "/")
    {
        config.AddJsonFile(basePath + "app/appsettings.json", optional: false, reloadOnChange: true);
    }
    else
    {
        config.AddJsonFile(Directory.GetCurrentDirectory() + "/appsettings.json", optional: false, reloadOnChange: true);
    }

    config.AddEnvironmentVariables();

    await ConnectToKeyVaultAndSetApplicationInsights(config);

    config.AddCommandLine(args);
}

async Task ConnectToKeyVaultAndSetApplicationInsights(ConfigurationManager config)
{
    KeyVaultSettings keyVaultSettings = new();
    config.GetSection("kvSetting").Bind(keyVaultSettings);
    if (!string.IsNullOrEmpty(keyVaultSettings.ClientId) &&
        !string.IsNullOrEmpty(keyVaultSettings.TenantId) &&
        !string.IsNullOrEmpty(keyVaultSettings.ClientSecret) &&
        !string.IsNullOrEmpty(keyVaultSettings.SecretUri))
    {
        logger.LogInformation("Program // Configure key vault client // App");

        string connectionString = $"RunAs=App;AppId={keyVaultSettings.ClientId};" +
                                  $"TenantId={keyVaultSettings.TenantId};" +
                                  $"AppKey={keyVaultSettings.ClientSecret}";
        AzureServiceTokenProvider azureServiceTokenProvider = new(connectionString);
        KeyVaultClient keyVaultClient = new(
            new KeyVaultClient.AuthenticationCallback(
                azureServiceTokenProvider.KeyVaultTokenCallback));
        config.AddAzureKeyVault(
            keyVaultSettings.SecretUri, keyVaultClient, new DefaultKeyVaultSecretManager());
        try
        {
            SecretBundle secretBundle = await keyVaultClient
                .GetSecretAsync(keyVaultSettings.SecretUri, vaultApplicationInsightsKey);

            applicationInsightsConnectionString = string.Format("InstrumentationKey={0}", secretBundle.Value);
        }
        catch (Exception vaultException)
        {
            logger.LogError(vaultException, $"Unable to read application insights key.");
        }
    }
}

void ConfigureSetupLogging()
{
    var logFactory = LoggerFactory.Create(builder =>
    {
        builder
            .AddFilter("Altinn.Register.Program", LogLevel.Debug)
            .AddConsole();
    });

    logger = logFactory.CreateLogger<Program>();
}

void ConfigureLogging(ILoggingBuilder logging)
{
    // The default ASP.NET Core project templates call CreateDefaultBuilder, which adds the following logging providers:
    // Console, Debug, EventSource
    // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-3.1

    // Clear log providers
    logging.ClearProviders();

    // Setup up application insight if applicationInsightsConnectionString is available
    if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
    {
        // Add application insights https://docs.microsoft.com/en-us/azure/azure-monitor/app/ilogger
        logging.AddApplicationInsights(
            configureTelemetryConfiguration: (config) => config.ConnectionString = applicationInsightsConnectionString,
            configureApplicationInsightsLoggerOptions: (options) => { });

        // Optional: Apply filters to control what logs are sent to Application Insights.
        // The following configures LogLevel Information or above to be sent to
        // Application Insights for all categories.
        logging.AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>(string.Empty, LogLevel.Warning);

        // Adding the filter below to ensure logs of all severity from Program.cs
        // is sent to ApplicationInsights.
        logging.AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>(typeof(Program).FullName, LogLevel.Trace);
    }
    else
    {
        // If not application insight is available log to console
        logging.AddFilter("Microsoft", LogLevel.Warning);
        logging.AddFilter("System", LogLevel.Warning);
        logging.AddConsole();
    }
}

void ConfigureServices(IServiceCollection services, IConfiguration config)
{
    logger.LogInformation("Program // ConfigureServices");

    services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

    services.AddMemoryCache();
    services.AddHealthChecks().AddCheck<HealthCheck>("register_health_check");

    services.AddSingleton(config);
    services.Configure<GeneralSettings>(config.GetSection("GeneralSettings"));
    services.Configure<KeyVaultSettings>(config.GetSection("kvSetting"));
    services.Configure<AccessTokenSettings>(config.GetSection("AccessTokenSettings"));
    services.Configure<PlatformSettings>(config.GetSection("PlatformSettings"));
    services.Configure<PersonLookupSettings>(config.GetSection("PersonLookupSettings"));

    services.AddSingleton<IAuthorizationHandler, AccessTokenHandler>();
    services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    services.AddSingleton<ISigningKeysResolver, SigningKeysResolver>();

    services.AddTransient<IPersonLookup, PersonLookupService>();
    services.Decorate<IPersonLookup, PersonLookupCacheDecorator>();

    services.AddAuthentication(JwtCookieDefaults.AuthenticationScheme)
          .AddJwtCookie(JwtCookieDefaults.AuthenticationScheme, options =>
          {
              GeneralSettings generalSettings = config.GetSection("GeneralSettings").Get<GeneralSettings>();
              options.JwtCookieName = generalSettings.JwtCookieName;
              options.MetadataAddress = generalSettings.OpenIdWellKnownEndpoint;
              options.TokenValidationParameters = new TokenValidationParameters
              {
                  ValidateIssuerSigningKey = true,
                  ValidateIssuer = false,
                  ValidateAudience = false,
                  RequireExpirationTime = true,
                  ValidateLifetime = true,
                  ClockSkew = TimeSpan.Zero
              };

              if (builder.Environment.IsDevelopment())
              {
                  options.RequireHttpsMetadata = false;
              }
          });

    services.AddAuthorization(options =>
    {
        options.AddPolicy("PlatformAccess", policy => policy.Requirements.Add(new AccessTokenRequirement()));
        options.AddPolicy("AuthorizationLevel2", policy =>
            policy.RequireClaim(AltinnCore.Authentication.Constants.AltinnCoreClaimTypes.AuthenticationLevel, "2", "3", "4"));
    });

    services.AddHttpClient<IOrganizations, OrganizationsWrapper>();
    services.AddHttpClient<IParties, PartiesWrapper>();
    services.AddHttpClient<IAuthorization, AuthorizationWrapper>();

    if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
    {
        services.AddSingleton(typeof(ITelemetryChannel), new ServerTelemetryChannel() { StorageFolder = "/tmp/logtelemetry" });
        services.AddApplicationInsightsTelemetry(new ApplicationInsightsServiceOptions
        {
            ConnectionString = applicationInsightsConnectionString
        });

        services.AddApplicationInsightsTelemetryProcessor<HealthTelemetryFilter>();
        services.AddApplicationInsightsTelemetryProcessor<IdentityTelemetryFilter>();
        services.AddSingleton<ITelemetryInitializer, CustomTelemetryInitializer>();

        logger.LogInformation("Startup // ApplicationInsightsConnectionString = {applicationInsightsConnectionString}", applicationInsightsConnectionString);
    }

    // Add Swagger support (Swashbuckle)
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Altinn Platform Register", Version = "v1" });
        IncludeXmlComments(c);
    });
}

void IncludeXmlComments(SwaggerGenOptions swaggerGenOptions)
{
    try
    {
        string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        swaggerGenOptions.IncludeXmlComments(xmlPath);
    }
    catch (Exception e)
    {
        logger.LogWarning(e, "Prorgam // Exception when attempting to include the XML comments file(s).");
    }
}

void Configure()
{
    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
    {
        // Enable higher level of detail in exceptions related to JWT validation
        IdentityModelEventSource.ShowPII = true;
    }
    else
    {
        app.UseExceptionHandler("/register/api/v1/error");
    }

    app.UseSwagger(o => o.RouteTemplate = "register/swagger/{documentName}/swagger.json");

    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/register/swagger/v1/swagger.json", "Altinn Platform Register API");
        c.RoutePrefix = "register/swagger";
    });

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");
}
