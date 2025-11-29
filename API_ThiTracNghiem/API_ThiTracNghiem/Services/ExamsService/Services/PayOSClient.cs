using Net.payOS;
using Net.payOS.Types;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ExamsService.Services
{
    public class PayOSClient
    {
        private readonly Net.payOS.PayOS? _payOS;
        public bool IsConfigured { get; }

        public PayOSClient(IConfiguration config)
        {
            var clientId = config["PAYOS_CLIENT_ID"];
            var apiKey = config["PAYOS_API_KEY"];
            var checksumKey = config["PAYOS_CHECKSUM_KEY"];
            var partnerCode = config["PAYOS_PARTNER_CODE"];
            if (string.IsNullOrWhiteSpace(clientId)) clientId = Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID");
            if (string.IsNullOrWhiteSpace(apiKey)) apiKey = Environment.GetEnvironmentVariable("PAYOS_API_KEY");
            if (string.IsNullOrWhiteSpace(checksumKey)) checksumKey = Environment.GetEnvironmentVariable("PAYOS_CHECKSUM_KEY");
            if (string.IsNullOrWhiteSpace(partnerCode)) partnerCode = Environment.GetEnvironmentVariable("PAYOS_PARTNER_CODE");
            IsConfigured = !(string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(checksumKey));
            if (IsConfigured)
            {
                _payOS = string.IsNullOrEmpty(partnerCode)
                    ? new Net.payOS.PayOS(clientId!, apiKey!, checksumKey!)
                    : new Net.payOS.PayOS(clientId!, apiKey!, checksumKey!, partnerCode);
            }
        }

        public Task<CreatePaymentResult> CreatePaymentLink(long orderCode, int amount, string description, string returnUrl, string cancelUrl)
        {
            var data = new PaymentData(orderCode, amount, description.Length > 25 ? description.Substring(0, 25) : description, new List<ItemData>(), cancelUrl, returnUrl);
            return _payOS.createPaymentLink(data);
        }

        public Task<CreatePaymentResult> CreatePaymentLink(long orderCode, int amount, string description, string returnUrl, string cancelUrl, List<ItemData> items)
        {
            var desc = description.Length > 25 ? description.Substring(0, 25) : description;
            var data = new PaymentData(orderCode, amount, desc, items ?? new List<ItemData>(), cancelUrl, returnUrl);
            return _payOS.createPaymentLink(data);
        }

        public Task<PaymentLinkInformation> GetPaymentLinkInformation(long id) => _payOS.getPaymentLinkInformation(id);

        public Task<PaymentLinkInformation> CancelPaymentLink(long orderCode, string reason) => _payOS.cancelPaymentLink(orderCode, reason);

        public WebhookData VerifyWebhook(Net.payOS.Types.WebhookType body) => _payOS.verifyPaymentWebhookData(body);
    }
}