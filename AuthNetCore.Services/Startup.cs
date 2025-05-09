using AuthNetCore.BL;
using AuthNetCore.BL.IBL;
using AuthNetCore.DAL;
using AuthNetCore.DAL.IDAL;
using AuthNetCore.Data.Models.Entities;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Identity.Client;

namespace AuthNetCore.Services
{
    internal class Startup
    {
        private IConfiguration Configuration { get; set; }
        
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;  
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.CommonCorsConfigurations();
            services.AddControllers();
            services.AddHttpContextAccessor();

            services.Configure<JwtSettings>(Configuration.GetSection("Jwt"));
            services.AddJwtAuthentication(Configuration);
            services.AddAuthorization();

            services.CommonSwaggerConfigurations();
            services.ConfigureDatabase(Configuration);

            services.AddScoped<IAccountServiceBL, AccountServiceBL>();
            services.AddScoped<IAccountService, AccountService>();
            services.AddScoped<IPasswordRecovery, AccountService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IApiVersionDescriptionProvider provider)
        {
            app.UseSwagger();

            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
            });

            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }


    }
}
