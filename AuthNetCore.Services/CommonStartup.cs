using AuthNetCore.Data.Access;
using AuthNetCore.Utilities.Globals;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AuthNetCore.Services
{
    internal static class CommonStartup
    {
        internal static void ConfigureDatabase(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            string authNetCoreDBConn = configuration.GetConnectionString(Globals.DefaultConnection) ?? string.Empty;
            NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
            if (!string.IsNullOrEmpty(authNetCoreDBConn))
            {
                services.AddDbContext<AuthNetCoreDbContext>(options =>
                    options.UseSqlServer(authNetCoreDBConn,
                        sqlServerOptions =>
                        {
                            sqlServerOptions.CommandTimeout(Convert.ToInt32(configuration.GetValue<string>("SqlCommandTimeout") ?? "30"));
                        })
                    .LogTo(msg =>
                    {
                        string loglevel = configuration.GetValue<string>("LogLevel") ?? "Error";
                        if (loglevel.Equals("Trace", StringComparison.OrdinalIgnoreCase))
                        {
                            NLog.LogManager.GetCurrentClassLogger().Trace(msg);
                        }
                    })
                );
            }
            else
                logger.Error(Globals.LoggIssueConnection);
        }

        internal static void AddJwtAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var jwtKey = configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new ArgumentNullException(nameof(jwtKey), Globals.JwtKeyNotFounded);
            }

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };
            });
        }

        internal static void CommonCorsConfigurations(this IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("MyPoliticalCors", policy =>
                    {
                        policy.WithOrigins(Globals.DomainPhaser, Globals.DomainStable)
                              .AllowAnyHeader()
                              .AllowAnyMethod();
                    }
                );
            });
        }

        internal static void CommonSwaggerConfigurations(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = Globals.NethAuthCoreValue,
                    Version = "v1", 
                    Description = Globals.NethAuthCoreDescription
                });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter 'Bearer' followed by your JWT token. Example: 'Bearer abc123def456'."
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });
        }

        internal static void CommonConfigure(
            this IApplicationBuilder app,
            IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint(Globals.SwaggerUrlEndpointV1, Globals.SwaggerNameEndpointV1);
                });
            }
            else
            {
                app.UseExceptionHandler("/Home/Error"); // Handles errors in production
                app.UseHsts(); // Adds additional security for production
            }

            app.UseCors("MyPoliticalCors");

            //app.UseMiddleware<AuthNetHandleMiddleware>(); // Custom middleware for exception handling

            app.UseHttpsRedirection(); // Redirects HTTP requests to HTTPS
            app.UseRouting(); // Configures endpoint routing
            app.UseAuthentication(); // Adds authentication to the pipeline
            app.UseAuthorization(); // Adds authorization to the pipeline
        }
    }
}
