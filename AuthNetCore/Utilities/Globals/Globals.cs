using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthNetCore.Utilities.Globals
{
    public static class Globals
    {
        // DB Sets
        public const string DefaultConnection = "DefaultConnection";
        public const string LoggIssueConnection = "Error, missing configuration 'MarketProductDB'";
        // Jwt Handlings
        public const string JwtKeyNotFounded = "JWT Key is not configured properly in appsettings.";
        // API Crowns
        public const string NethAuthCoreValue = "NethAuthCoreValue Microservice";
        public const string NethAuthCoreDescription = "Manager Accounts via endpoints to set the real constrictions";
        public const string SwaggerUrlEndpointV1 = "/swagger/v1/swagger.json";
        public const string SwaggerNameEndpointV1 = "AuthNetCore v1";
        // Cors variables
        public const string DomainPhaser = "https://yourdomain1.com";
        public const string DomainStable = "https://yourdomain2.com";
        // EmailServiceHelper variables
        public const string SmtpServer = "mail.midominio.com";
        public const string SmtpUser = "noreply@midominio.com";
        public const string SmtpPassword = "tu_contraseña_segura";
        public const string SystemServiceName = "AuthNethCore System";
        public const string EmailServiceHelperSubject = "Recuperación de contraseñas - AuthNethCore System";
        public const string RecoveryBaseUrl = "https://tusitio.com/reset-password?";
        public const string LogoRecoveryEmailBody = "https://via.placeholder.com/150x50?text=AuthNethCore";
        public const string PrivacyUrl = "https://tusitio.com/privacidad";
        public const string SupportUrl = "https://tusitio.com/soporte";
        public const string MailSupportReq = "mailto:soporte@midominio.com";
    }
}
