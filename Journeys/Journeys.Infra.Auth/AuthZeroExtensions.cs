using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Journeys.Infra.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;

namespace Journeys.Infra.Auth
{
    public static class AuthZeroExtensions
    {
        private static bool IsCampaignAgentPath(PathString path)
        {
            var segments = (path.Value ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length >= 4
                   && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase)
                   && segments[1].Equals("v1", StringComparison.OrdinalIgnoreCase)
                   && segments[3].Equals("campaign-agent", StringComparison.OrdinalIgnoreCase);
        }

        // SAMPLE CONFIGURATION OBJECT
        //
        //{
        //  "Auth0": {
        //    "Tenant1": {
        //      "Authority": "https://tenant1.auth0.com/",
        //      "Audience": "Tenant1-API",
        //      "RolesClaimType": "https://your-app/roles",
        //      "AllowedRoles": [ "Loyalty_Admin", "Client_Support" ]
        //    },
        //    "Tenant2": {
        //      "Authority": "https://tenant2.auth0.com/",
        //      "Audience": "Tenant2-API",
        //      "RolesClaimType": "https://your-app/roles",
        //      "AllowedRoles": [ "Loyalty_Admin" ]
        //    }
        //  },
        //  "ApiKeys": [
        //    "key1",
        //    "key2"
        //  ]
        //}

        /// <summary>
        /// Configures authentication for the application using JWT Bearer tokens.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="environment"></param>
        public static void ConfigureAuth(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment, ILogger logger)
        {
            services.AddHttpContextAccessor();
            services.AddSingleton<IAuthorizationHandler, HasAllowedRolesHandler>();

            services
              .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
              .AddJwtBearer(opts =>
              {
                  opts.RequireHttpsMetadata = true;

                  opts.Events = new JwtBearerEvents
                  {
                      OnMessageReceived = ctx =>
                      {
                          try
                          {
                              // Retrive auth0 configuration
                              // TODO: this needs to be configured per tenant
                              //var tenant = http.Request.RouteValues["tenantId"]?.ToString()
                              //  ?? throw new Exception("tenantId route value missing");
                              var tenant = "hayward";

                              var audience =
                                  configuration[$"Auth0:{tenant}:Audience"]
                                  ?? throw new Exception($"Missing Auth0:{tenant}:Audience");

                              var authority =
                                  configuration[$"Auth0:{tenant}:Authority"]
                                  ?? throw new Exception($"Missing Auth0:{tenant}:Authority");

                              var metadata = $"{authority.TrimEnd('/')}/.well-known/openid-configuration";

                              // Assign per-tenant settings
                              ctx.Options.Audience = audience;
                              ctx.Options.Authority = authority;

                              // Set where to pull the OpenID Connect config (and keys) for this tenant on this request
                              ctx.Options.MetadataAddress = metadata;
                              ctx.Options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                                  metadata,
                                  new OpenIdConnectConfigurationRetriever(),
                                  new HttpDocumentRetriever { RequireHttps = opts.RequireHttpsMetadata }
                              );

                              // Set validation parameters
                              var tvp = ctx.Options.TokenValidationParameters;
                              tvp.ValidIssuer = authority;
                              tvp.ValidAudience = audience;
                              tvp.NameClaimType = ClaimTypes.NameIdentifier;
                              tvp.RoleClaimType = "permissions";
                              logger.LogDebug("Configured JWT for tenant {Tenant} with authority {Authority} and audience {Audience}", tenant, authority, audience);
                          }
                          catch (Exception ex) {
                              logger.LogError(ex, "Error, auth failed {StatusCode}", 401);
                          }
                          return Task.CompletedTask;
                      },
                      OnAuthenticationFailed = ctx =>
                      {
                          var log = ctx.HttpContext.RequestServices
                                       .GetRequiredService<ILogger<JwtBearerEvents>>();
                          log.LogError(ctx.Exception, "JWT authentication failed");
                          logger.LogError(ctx.Exception, "JWT authentication failed in ConfigureAuth");
                          return Task.CompletedTask;
                      }
                  };

              });

            services
               .AddAuthorization(
               opts =>
               {
                   opts.AddPolicy("RequireLoyaltyAccount", policy =>
                   {
                       policy.RequireAssertion(context =>
                       {
                           // get the MVC context so we can read headers & services
                           var mvcCtx = context.Resource as AuthorizationFilterContext;
                           var http = mvcCtx?.HttpContext;
                           if (http == null) {
                               logger.LogDebug("Authorization context missing.");
                               return false;
                           }
                           
                           // Allow Swagger endpoints without authorization
                           if (http.Request.Path.StartsWithSegments("/swagger"))
                           {
                               logger.LogDebug("Authorization bypassed for Swagger endpoint.");
                               return true;
                           }

                           // Campaign agent (Next.js chat): JWT, API key, or dev � no Auth0 scope requirement
                           // Route: /api/v1/{tenantId}/campaign-agent/...
                           if (IsCampaignAgentPath(http.Request.Path))
                           {
                               var tenantAgent = "hayward";
                               if (configuration.GetValue<bool>($"Auth0:{tenantAgent}:isDevelopment"))
                               {
                                   logger.LogDebug("Campaign-agent path allowed (development).");
                                   return true;
                               }

                               var keysAgent = configuration.GetSection("ApiKeys").Get<string[]>() ?? Array.Empty<string>();
                               var keyHeader = http.Request.Headers["Journeys-API-KEY"].ToString();
                               if (!string.IsNullOrWhiteSpace(keyHeader) && keysAgent.Contains(keyHeader))
                               {
                                   logger.LogDebug("Campaign-agent path allowed (API key).");
                                   return true;
                               }

                               if (context.User.Identity?.IsAuthenticated == true)
                               {
                                   logger.LogDebug("Campaign-agent path allowed (JWT).");
                                   return true;
                               }

                               logger.LogDebug("Campaign-agent path denied.");
                               return false;
                           }

                           // TODO: this needs to be configured per tenant
                           //var tenant = http.Request.RouteValues["tenantId"]?.ToString()
                           //  ?? throw new Exception("tenantId route value missing");
                           var tenant = "hayward";

                           // TODO - Adams: this really should come from launchSettings
                           // a.) Development always allowed
                           var isDevelopment = configuration.GetValue<bool>($"Auth0:{tenant}:isDevelopment");
                           if (isDevelopment)
                           {
                               logger.LogDebug("Authorization allowed in development environment.");
                               return true;
                           }

                           // b.) Valid API-Key always allowed
                           var validKeys = configuration
                                          .GetSection("ApiKeys")
                                          .Get<string[]>()
                                          ?? Array.Empty<string>();
                           var apiKey = http.Request.Headers["Journeys-API-KEY"].ToString();
                           if (!string.IsNullOrWhiteSpace(apiKey) && validKeys.Contains(apiKey))
                           {
                               logger.LogDebug("Authorization allowed via valid API key.");
                               return true;
                           }

                           // c.) Otherwise validate the JWT includes a role from Auth0:{tenant}:AllowedRoles (RolesClaimType)

                           var rolesClaimType = configuration[$"Auth0:{tenant}:RolesClaimType"]
                             ?? throw new Exception($"Missing Auth0:{tenant}:RolesClaimType");

                           var allowedRoles = configuration
                               .GetSection($"Auth0:{tenant}:AllowedRoles")
                               .Get<string[]>()
                               ?? Array.Empty<string>();

                           if (allowedRoles.Length == 0)
                               throw new Exception($"Missing or empty Auth0:{tenant}:AllowedRoles");

                           var requirement = new HasAllowedRolesRequirement(rolesClaimType, allowedRoles);

                           var handler = http.RequestServices.GetRequiredService<IAuthorizationHandler>();
                           var authContext = new AuthorizationHandlerContext(
                                                 [requirement],
                                                 context.User,
                                                 context.Resource);

                           handler.HandleAsync(authContext).GetAwaiter().GetResult();
                           logger.LogDebug(
                               "Authorization assertion for tenant {Tenant} allowed roles {Roles}: {Result}",
                               tenant,
                               string.Join(',', allowedRoles),
                               authContext.HasSucceeded);
                           return authContext.HasSucceeded;
                       });
                   });
               });
        }
    }
}
