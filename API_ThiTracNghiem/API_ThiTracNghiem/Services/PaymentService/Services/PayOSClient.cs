using Net.payOS;
using Net.payOS.Types;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PaymentService.Services
{
    public class PayOSClient
    {
        private readonly Net.payOS.PayOS _payOS;

        public PayOSClient(IConfiguration config)
        {
            var clientId = config["PAYOS_CLIENT_ID"];
            var apiKey = config["PAYOS_API_KEY"];
            var checksumKey = config["PAYOS_CHECKSUM_KEY"];
            var partnerCode = config["PAYOS_PARTNER_CODE"];
            clientId ??= Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID");
            apiKey ??= Environment.GetEnvironmentVariable("PAYOS_API_KEY");
            checksumKey ??= Environment.GetEnvironmentVariable("PAYOS_CHECKSUM_KEY");
            partnerCode ??= Environment.GetEnvironmentVariable("PAYOS_PARTNER_CODE");
            _payOS = string.IsNullOrEmpty(partnerCode)
                ? new Net.payOS.PayOS(clientId, apiKey, checksumKey)
                : new Net.payOS.PayOS(clientId, apiKey, checksumKey, partnerCode);
        }

        public Task<CreatePaymentResult> CreatePaymentLink(long orderCode, int amount, string description, string returnUrl, string cancelUrl)
        {
            var data = new PaymentData(orderCode, amount, description.Length > 25 ? description.Substring(0, 25) : description, new List<ItemData>(), cancelUrl, returnUrl);
            return _payOS.createPaymentLink(data);
        }

        public Task<PaymentLinkInformation> GetPaymentLinkInformation(long id) => _payOS.getPaymentLinkInformation(id);

        public Task<PaymentLinkInformation> CancelPaymentLink(long orderCode, string reason) => _payOS.cancelPaymentLink(orderCode, reason);

        public WebhookData VerifyWebhook(WebhookType body) => _payOS.verifyPaymentWebhookData(body);
    }
}