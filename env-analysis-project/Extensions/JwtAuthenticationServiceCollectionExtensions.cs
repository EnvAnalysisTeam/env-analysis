using System;
using System.Threading.Tasks;
using System.Text;
using env_analysis_project.Options;
using env_analysis_project.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace env_analysis_project.Extensions
{
    public static class JwtAuthenticationServiceCollectionExtensions
    {
        public static IServiceCollection AddJwtAuthenticationConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
            var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>()
                ?? throw new InvalidOperationException("Jwt configuration is missing.");

            if (string.IsNullOrWhiteSpace(jwtOptions.Key))
            {
                throw new InvalidOperationException("Jwt:Key is required.");
            }

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key));

            services
                .AddAuthorization()
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = true;
                    options.SaveToken = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = signingKey,
                        ValidateIssuer = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwtOptions.Audience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            if (context.Request.Cookies.TryGetValue(JwtDefaults.AccessTokenCookieName, out var token))
                            {
                                context.Token = token;
                            }
                            return Task.CompletedTask;
                        },
                        OnChallenge = context =>
                        {
                            context.HandleResponse();

                            var isAjax = string.Equals(
                                context.Request.Headers["X-Requested-With"],
                                "XMLHttpRequest",
                                StringComparison.OrdinalIgnoreCase);

                            if (isAjax)
                            {
                                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                                return context.Response.WriteAsync("Unauthorized");
                            }

                            context.Response.Redirect("/Identity/Account/Login");
                            return Task.CompletedTask;
                        }
                    };
                });

            services.AddSingleton<IAuthorizationMiddlewareResultHandler, FriendlyAuthorizationMiddlewareResultHandler>();
            return services;
        }
    }
}
