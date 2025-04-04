using AuthNetCore.Data.Models.EModels;
using Microsoft.Identity.Client;

namespace AuthNetCore.Services
{
    internal class Startup
    {
        public IConfiguration Configuration { get; set; }
        
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
        }
    }
}
