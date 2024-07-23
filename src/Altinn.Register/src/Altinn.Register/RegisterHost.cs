using System.Text.Json.Serialization;
using Altinn.Authorization.ServiceDefaults;
using Altinn.Common.AccessToken;
using Altinn.Common.AccessToken.Configuration;
using Altinn.Common.AccessToken.Services;
using Altinn.Common.PEP.Authorization;
using Altinn.Register.Authorization;
using Altinn.Register.Clients;
using Altinn.Register.Clients.Interfaces;
using Altinn.Register.Configuration;
using Altinn.Register.Core;
using Altinn.Register.Core.Parties;
using Altinn.Register.Services;
using Altinn.Register.Services.Implementation;
using Altinn.Register.Services.Interfaces;
using AltinnCore.Authentication.JwtCookie;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

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

        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.WriteIndented = true;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
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
                policy.Requirements.Add(new InternalScopeOrAccessTokenRequirement("altinn:register/partylookup.admin")));

        services.AddHttpClient<IOrganizationClient, OrganizationClient>();
        services.AddHttpClient<IPersonClient, PersonClient>();
        services.AddHttpClient<IPartyPersistence, PartiesClient>();
        services.AddHttpClient<IAuthorizationClient, AuthorizationClient>();

        if (config.GetValue<bool>("Altinn:Npgsql:register:Enable"))
        {
            builder.AddRegisterPersistence();
        }

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Altinn Platform Register", Version = "v1" });

            var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var xmlFile = Path.ChangeExtension(assemblyPath, ".xml");
            if (File.Exists(xmlFile))
            {
                c.IncludeXmlComments(xmlFile);
            }

            c.SupportNonNullableReferenceTypes();

            var originalIdSelector = c.SchemaGeneratorOptions.SchemaIdSelector;
            c.SchemaGeneratorOptions.SchemaIdSelector = (Type t) =>
            {
                if (!t.IsNested)
                {
                    return originalIdSelector(t);
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
