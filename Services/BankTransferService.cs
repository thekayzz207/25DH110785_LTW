using System;
using System.Configuration;
using System.IO;
using System.Web;

namespace LTW_25DH110785.Services
{
    public static class BankTransferService
    {
        public static string QrImagePath
        {
            get
            {
                var path = (ConfigurationManager.AppSettings["BankTransferQrImagePath"] ?? "").Trim();
                return PaymentRules.IsValidLocalQrPath(path) ? path : null;
            }
        }

        public static bool IsAvailable(HttpServerUtilityBase server)
        {
            var path = QrImagePath;
            return path != null && File.Exists(server.MapPath(path));
        }
    }
}
