using AuthNetCore.Utilities.Globals;
using System;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace AuthNetCore.Helpers
{
    public static class EmailServiceHelper
    {
        private static readonly EmailConfiguration _config = new EmailConfiguration(
            smtpServer: Globals.SmtpServer,
            smtpPort: 456,
            smtpUser: Globals.SmtpUser,
            smtpPassword: Globals.SmtpPassword
        );

        public static bool SendPasswordRecoveryEmail(string recipientEmail)
        {
            try
            {
                var token = TokenGenerator.GenerateSecureToken();
                var link = RecoveryLinkBuilder.BuildLink(token);
                var body = EmailTemplateBuilder.BuildRecoveryEmailBody(link);

                var email = EmailFactory.CreateEmail(recipientEmail, body);
                EmailSender.SendEmail(email, _config);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    #region Stablished Criteria Templates

    internal static class TokenGenerator
    {
        private static readonly byte[] _secretKey = GenerateInternalKey();

        public static string GenerateSecureToken()
        {
            byte[] entropy = GenerateRandomBytes(64);
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            byte[] timestampBytes = BitConverter.GetBytes(timestamp);

            byte[] combined = Combine(entropy, timestampBytes);
            byte[] hashed = ComputeHmac(combined);

            return Base64UrlEncode(hashed);
        }

        private static byte[] GenerateRandomBytes(int length)
        {
            byte[] bytes = new byte[length];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return bytes;
        }

        private static byte[] ComputeHmac(byte[] data)
        {
            using var hmac = new HMACSHA256(_secretKey);
            return hmac.ComputeHash(data);
        }

        private static byte[] Combine(params byte[][] arrays)
        {
            int length = 0;
            foreach (var arr in arrays)
                length += arr.Length;

            byte[] result = new byte[length];
            int offset = 0;
            foreach (var arr in arrays)
            {
                Buffer.BlockCopy(arr, 0, result, offset, arr.Length);
                offset += arr.Length;
            }

            return result;
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        private static byte[] GenerateInternalKey()
        {
            using var rng = RandomNumberGenerator.Create();
            byte[] key = new byte[32];
            rng.GetBytes(key);
            return key;
        }
    }

    internal static class RecoveryLinkBuilder
    {
        public static string BuildLink(string token)
        {
            return $"{Globals.RecoveryBaseUrl}token={token}";
        }
    }

    internal static class EmailTemplateBuilder
    {
        public static string BuildRecoveryEmailBody(string recoveryLink)
        {
            return $@"
            <!DOCTYPE html>
            <html lang=""es"">
            <head>
                <meta charset=""UTF-8"">
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                <title>Recuperación de contraseña</title>
            </head>
            <body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
                <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f4f4f4; padding: 20px;"">
                    <tr>
                        <td align=""center"">
                            <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #ffffff; border-radius: 8px; overflow: hidden;"">
                                <tr>
                                    <td style=""background-color: #2c3e50; padding: 20px; text-align: center;"">
                                        <img src=""{Globals.LogoRecoveryEmailBody}"" alt=""AuthNethCore Logo"" style=""display: block; margin: 0 auto;"">
                                    </td>
                                </tr>
                                <tr>
                                    <td style=""padding: 30px;"">
                                        <h2 style=""color: #2c3e50;"">Hola,</h2>
                                        <p style=""font-size: 16px; color: #555;"">
                                            Recibimos una solicitud para restablecer la contraseña de tu cuenta en <strong>AuthNethCore System</strong>.
                                        </p>
                                        <p style=""font-size: 16px; color: #555;"">
                                            Para continuar con el proceso de recuperación, haz clic en el siguiente botón:
                                        </p>
                                        <p style=""text-align: center; margin: 30px 0;"">
                                            <a href=""{recoveryLink}"" 
                                               style=""background-color: #3498db; color: #fff; text-decoration: none; padding: 15px 25px; border-radius: 5px; font-weight: bold; display: inline-block;"">
                                                Recuperar contraseña
                                            </a>
                                        </p>
                                        <p style=""font-size: 15px; color: #777;"">
                                            Si no solicitaste este cambio, puedes ignorar este mensaje. Tu contraseña actual no se verá afectada.
                                        </p>
                                        <hr style=""border: none; border-top: 1px solid #ddd; margin: 30px 0;"">
                                        <p style=""font-size: 14px; color: #999;"">
                                            Este enlace estará disponible durante un tiempo limitado por razones de seguridad. Te recomendamos no compartirlo con nadie.
                                        </p>
                                        <p style=""font-size: 14px; color: #999;"">
                                            Si tienes alguna duda, contáctanos a través de nuestro correo de soporte: <a href=""{Globals.MailSupportReq}"">soporte@midominio.com</a>
                                        </p>
                                    </td>
                                </tr>
                                <tr>
                                    <td style=""background-color: #ecf0f1; text-align: center; padding: 20px; font-size: 12px; color: #777;"">
                                        © {DateTime.UtcNow.Year} AuthNethCore. Todos los derechos reservados.<br/>
                                        <a href=""{Globals.PrivacyUrl}"" style=""color: #3498db;"">Política de privacidad</a> |
                                        <a href=""{Globals.SupportUrl}"" style=""color: #3498db;"">Soporte</a>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>";
        }
    }

    internal static class EmailFactory
    {
        public static MailMessage CreateEmail(string to, string htmlBody)
        {
            return new MailMessage
            {
                From = new MailAddress(Globals.SmtpUser, Globals.SystemServiceName),
                Subject = Globals.EmailServiceHelperSubject,
                Body = htmlBody,
                IsBodyHtml = true,
                To = { to }
            };
        }
    }

    internal static class EmailSender
    {
        public static void SendEmail(MailMessage message, EmailConfiguration config)
        {
            using var client = new SmtpClient(config.Server, config.Port)
            {
                Credentials = new NetworkCredential(config.User, config.Password),
                EnableSsl = true
            };
            client.Send(message);
        }
    }

    internal class EmailConfiguration
    {
        public string Server { get; }
        public int Port { get; }
        public string User { get; }
        public string Password { get; }

        public EmailConfiguration(string smtpServer, int smtpPort, string smtpUser, string smtpPassword)
        {
            Server = smtpServer;
            Port = smtpPort;
            User = smtpUser;
            Password = smtpPassword;
        }
    }

    #endregion
}
