using AuthNetCore.Data.Access;
using AuthNetCore.Utilities.Globals;
using Microsoft.EntityFrameworkCore;

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
                logger.Error("Error, missing configuration 'MarketProductDB'");
        }
    }
}
