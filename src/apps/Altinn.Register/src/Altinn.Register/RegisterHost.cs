using System.Text.RegularExpressions;
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
using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.PartyImport.A2;
using Altinn.Register.Extensions;
using Altinn.Register.ModelBinding;
using Altinn.Register.PartyImport.A2;
using Altinn.Register.Services;
using Altinn.Register.Services.Implementation;
using Altinn.Register.Services.Interfaces;
using Altinn.Register.Utils;
using AltinnCore.Authentication.JwtCookie;
using Asp.Versioning.ApiExplorer;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Register;

/// <summary>
/// Configures the register host.
/// </summary>
internal static partial class RegisterHost
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

        services
            .AddControllers()
            .AddControllersAsServices()
            .AddMvcOptions(opt =>
            {
                opt.ModelBinderProviders.InsertSingleton<PartyComponentOptionModelBinder>(0);
                opt.ModelBinderProviders.InsertSingleton<PartyFieldIncludesModelBinder>(0);
            })
            .AddJsonOptions(opt =>
            {
                if (builder.Environment.IsDevelopment())
                {
                    opt.JsonSerializerOptions.WriteIndented = true;
                }
            });

        services.AddSingleton<ConditionalControllerConvention>();
        services.AddOptions<MvcOptions>()
            .Configure((MvcOptions opts, IServiceProvider services) =>
            {
                opts.Conventions.Add(services.GetRequiredService<ConditionalControllerConvention>());
            });

        services
            .AddApiVersioning(opt =>
            {
                opt.ReportApiVersions = true;
            })
            .AddMvc()
            .AddApiExplorer(opt =>
            {
                // add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
                // note: the specified format code will format the version as "'v'major[.minor][-status]"
                opt.GroupNameFormat = "'v'VVV";

                // note: this option is only necessary when versioning by url segment. the SubstitutionFormat
                // can also be used to control the format of the API version in route templates
                opt.SubstituteApiVersionInUrl = true;
            });

        services.Configure<GeneralSettings>(config.GetSection("GeneralSettings"));
        services.Configure<KeyVaultSettings>(config.GetSection("kvSetting"));
        services.Configure<AccessTokenSettings>(config.GetSection("AccessTokenSettings"));
        services.Configure<PlatformSettings>(config.GetSection("PlatformSettings"));
        services.Configure<PersonLookupSettings>(config.GetSection("PersonLookupSettings"));

        services.AddOptions<A2PartyImportSettings>()
            .Configure((A2PartyImportSettings settings, IConfiguration config) =>
            {
                var ep = config.GetSection("Platform:SblBridge:Endpoint");
                if (!string.IsNullOrEmpty(ep.Value))
                {
                    settings.BridgeApiEndpoint = ep.Get<Uri>();
                }
            })
            .ValidateDataAnnotations();

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
                var config = s.GetRequiredService<IOptions<A2PartyImportSettings>>();

                c.BaseAddress = config.Value.BridgeApiEndpoint;
            })
            .ReplaceResilienceHandler(c =>
            {
                // Do not retry in the IA2PartyImportService, it's handled by MassTransit
                c.Retry.ShouldHandle = static _ => ValueTask.FromResult(false);
            });

        services.AddUnitOfWorkManager();
        if (config.GetValue<bool>("Altinn:Npgsql:register:Enable"))
        {
            builder.AddRegisterPersistence();
            builder.Services.AddLeaseManager();
        }
        else
        {
            services.AddSingleton<IImportJobTracker>(s => throw new InvalidOperationException("Npgsql is not enabled"));
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

        if (config.GetValue<bool>("Altinn:register:PartyImport:A2:PartyUserId:Enable"))
        {
            services.AddRecurringJob<A2PartyUserIdImportJob>(settings =>
            {
                settings.LeaseName = LeaseNames.A2PartyUserIdImport;
                settings.Interval = TimeSpan.FromMinutes(1);
                settings.WaitForReady = static (s, ct) => new ValueTask(s.GetRequiredService<IBusLifetime>().WaitForBus(ct));
            });
        }

        services.AddOpenApiExampleProvider();
        services.AddSwaggerFilterAttributeSupport();
        services.AddUrnSwaggerSupport();
        services.AddSwaggerAutoXmlDoc();
        services.AddAuthorizationModelUtilsSwaggerSupport();
        services.AddOptions<SwaggerGenOptions>()
            .Configure((SwaggerGenOptions opt, IApiVersionDescriptionProvider provider) =>
            {
                // add a swagger document for each discovered API version
                foreach (var desc in provider.ApiVersionDescriptions)
                {
                    opt.SwaggerDoc(desc.GroupName, new OpenApiInfo { Title = "Altinn Platform Register", Version = desc.ApiVersion.ToString() });
                }
            });

        services.AddSwaggerGen(c =>
        {
            var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var xmlFile = Path.ChangeExtension(assemblyPath, ".xml");
            if (File.Exists(xmlFile))
            {
                c.IncludeXmlComments(xmlFile);
            }

            c.EnableAnnotations();
            c.SupportNonNullableReferenceTypes();
            c.SchemaFilter<PartyComponentOptionSchemaFilter>();
            c.SchemaFilter<PartyFieldIncludesSchemaFilter>();

            var originalIdSelector = c.SchemaGeneratorOptions.SchemaIdSelector;
            c.SchemaGeneratorOptions.SchemaIdSelector = (Type t) =>
            {
                if (!t.IsNested)
                {
                    var orig = originalIdSelector(t);

                    if (t.Assembly == typeof(Contracts.PartyType).Assembly)
                    {
                        if (GetVersionedNamespaceRegex().Match(t.Namespace) is { Success: true, Groups: var groups })
                        {
                            orig = $"Contracts.{groups[1].ValueSpan}.{orig}";
                        }
                        else 
                        {
                            orig = $"Contracts.{orig}";
                        }
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

    [GeneratedRegex(@"\.(?<version>V\d+)(?:\.|$)", RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex GetVersionedNamespaceRegex();
}
