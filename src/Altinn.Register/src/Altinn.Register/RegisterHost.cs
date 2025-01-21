using System.Text.Json.Serialization;
using Altinn.Authorization.ServiceDefaults;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using Altinn.Common.AccessToken;
using Altinn.Common.AccessToken.Configuration;
using Altinn.Common.AccessToken.Services;
using Altinn.Common.PEP.Authorization;
using Altinn.Register.ApiDescriptions;
using Altinn.Register.Authorization;
using Altinn.Register.Clients;
using Altinn.Register.Clients.Interfaces;
using Altinn.Register.Configuration;
using Altinn.Register.Conventions;
using Altinn.Register.Core;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.PartyImport.A2;
using Altinn.Register.Extensions;
using Altinn.Register.ModelBinding;
using Altinn.Register.PartyImport.A2;
using Altinn.Register.Services;
using Altinn.Register.Services.Implementation;
using Altinn.Register.Services.Interfaces;
using AltinnCore.Authentication.JwtCookie;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Altinn.Register;

/// <summary>
/// Configures the register host.
/// </summary>
internal static class RegisterHost
{
    /// <summary>
    /// Configures the register host.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    public static WebApplication Create(string[] args)
    {
        var builder = AltinnHost.CreateWebApplicationBuilder("register", args);
        var services = builder.Services;
        var config = builder.Configuration;

        services.AddMemoryCache();

        services.AddControllers().AddMvcOptions(opt =>
        {
            opt.ModelBinderProviders.InsertSingleton<PartyComponentOptionModelBinder>(0);
            opt.ModelBinderProviders.InsertSingleton<PartyFieldIncludesModelBinder>(0);
        }).AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.WriteIndented = true;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        services.AddSingleton<ConditionalControllerConvention>();
        services.AddOptions<MvcOptions>()
            .Configure((MvcOptions opts, IServiceProvider services) =>
            {
                opts.Conventions.Add(services.GetRequiredService<ConditionalControllerConvention>());
            });

        services.Configure<GeneralSettings>(config.GetSection("GeneralSettings"));
        services.Configure<KeyVaultSettings>(config.GetSection("kvSetting"));
        services.Configure<AccessTokenSettings>(config.GetSection("AccessTokenSettings"));
        services.Configure<PlatformSettings>(config.GetSection("PlatformSettings"));
        services.Configure<PersonLookupSettings>(config.GetSection("PersonLookupSettings"));

        services.AddSingleton<IAuthorizationHandler, AccessTokenHandler>();
        services.AddScoped<IAuthorizationHandler, ScopeAccessHandler>();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddSingleton<IPublicSigningKeyProvider, PublicSigningKeyProvider>();

        services.AddTransient<IPersonLookup, PersonLookupService>();
        services.Decorate<IPersonLookup, PersonLookupCacheDecorator>();

        services.AddSingleton<IOrgContactPoint, OrgContactPointService>();

        services.TryAddSingleton<RegisterTelemetry>();
        services.ConfigureOpenTelemetryTracerProvider((builder) => builder.AddSource(RegisterTelemetry.Name));
        services.ConfigureOpenTelemetryMeterProvider((builder) => builder.AddMeter(RegisterTelemetry.Name));

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
                  ClockSkew = TimeSpan.Zero,
              };

              if (builder.Environment.IsDevelopment())
              {
                  options.RequireHttpsMetadata = false;
              }
          });

        services.AddAuthorizationBuilder()
            .AddPolicy("PlatformAccess", policy =>
                policy.Requirements.Add(new AccessTokenRequirement()))
            .AddPolicy("AuthorizationLevel2", policy =>
                policy.RequireClaim(AltinnCore.Authentication.Constants.AltinnCoreClaimTypes.AuthenticationLevel, "2", "3", "4"))
            .AddPolicy("InternalOrPlatformAccess", policy =>
                policy.Requirements.Add(new InternalScopeOrAccessTokenRequirement("altinn:register/partylookup.admin")))
            .AddPolicy("Debug", policy =>
            {
                // Note: this scope does not actually exist, and can only be generated using the test token generator.
                policy.Requirements.Add(new ScopeAccessRequirement("altinn:register/debug.internal"));
            });

        services.AddHttpClient<IOrganizationClient, OrganizationClient>();
        services.AddHttpClient<IPersonClient, PersonClient>();
        services.AddHttpClient<IV1PartyService, PartiesClient>();
        services.AddHttpClient<IAuthorizationClient, AuthorizationClient>();

        services.AddHttpClient<IA2PartyImportService, A2PartyImportService>()
            .ConfigureHttpClient((s, c) =>
            {
                var config = s.GetRequiredService<IConfiguration>();
                var baseAddress = config.GetValue<Uri>("GeneralSettings:BridgeApiEndpoint");

                c.BaseAddress = baseAddress;
            });

        if (config.GetValue<bool>("Altinn:Npgsql:register:Enable"))
        {
            builder.AddRegisterPersistence();
            builder.Services.AddLeaseManager();
        }

        if (config.GetValue<bool>("Altinn:MassTransit:register:Enable"))
        {
            builder.AddAltinnMassTransit(
                configureMassTransit: (cfg) =>
                {
                    cfg.AddConsumers(typeof(RegisterHost).Assembly);
                });
        }

        if (config.GetValue<bool>("Altinn:register:PartyImport:A2:Enable"))
        {
            services.AddRecurringJob<A2PartyImportJob>(settings =>
            {
                settings.LeaseName = LeaseNames.A2PartyImport;
                settings.Interval = TimeSpan.FromMinutes(1);
                settings.WaitForReady = static (s, ct) => new ValueTask(s.GetRequiredService<IBusLifetime>().WaitForBus(ct));
            });
        }

        services.AddOpenApiExampleProvider();
        services.AddSwaggerFilterAttributeSupport();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Altinn Platform Register", Version = "v1" });

            var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var xmlFile = Path.ChangeExtension(assemblyPath, ".xml");
            if (File.Exists(xmlFile))
            {
                c.IncludeXmlComments(xmlFile);
            }

            c.EnableAnnotations();
            c.SupportNonNullableReferenceTypes();
            c.SchemaFilter<PartyComponentOptionSchemaFilter>();
            c.SchemaFilter<FieldValueSchemaFilter>();
            c.SchemaFilter<PartyRecordSchemaFilter>();

            var originalIdSelector = c.SchemaGeneratorOptions.SchemaIdSelector;
            c.SchemaGeneratorOptions.SchemaIdSelector = (Type t) =>
            {
                if (!t.IsNested)
                {
                    var orig = originalIdSelector(t);

                    if (t.Assembly == typeof(Platform.Register.Enums.PartyType).Assembly)
                    {
                        orig = $"PlatformModels.{orig}";
                    }

                    return orig;
                }

                var chain = new List<string>();
                do
                {
                    chain.Add(originalIdSelector(t));
                    t = t.DeclaringType;
                }
                while (t != null);

                chain.Reverse();
                return string.Join(".", chain);
            };
        });

        return builder.Build();
    }
}
