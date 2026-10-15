using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace WalletFunding.Api;

public interface IVnpayGateway
{
    bool IsConfigured { get; }
    string MerchantCode { get; }
    string CreateUrl(string reference, decimal amount, string ipAddress);
    bool Verify(IReadOnlyDictionary<string, string> parameters);
}

public sealed class VnpayGateway(IConfiguration config) : IVnpayGateway
{
    public string MerchantCode => config["Vnpay:TmnCode"] ?? "";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(MerchantCode) && !string.IsNullOrWhiteSpace(config["Vnpay:HashSecret"]);

    public string CreateUrl(string reference, decimal amount, string ipAddress)
    {
        if (!IsConfigured) throw new ApiError(503, "Chưa cấu hình VNPay sandbox: TmnCode và HashSecret");
        var endpoint = config["Vnpay:PaymentUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var gateway) || gateway.Scheme != "https" || gateway.Host != "sandbox.vnpayment.vn")
            throw new ApiError(503, "Bản đồ án chỉ cho phép cổng VNPay sandbox");
        var returnUrl = config["Vnpay:ReturnUrl"] ?? "http://127.0.0.1:5174/payments/return";
        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var returnUri) || returnUri.Scheme is not ("http" or "https"))
            throw new ApiError(503, "ReturnUrl VNPay không hợp lệ");
        var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
        var fields = new Dictionary<string, string>
        {
            ["vnp_Version"] = "2.1.0", ["vnp_Command"] = "pay", ["vnp_TmnCode"] = MerchantCode,
            ["vnp_Amount"] = (amount * 100).ToString("0", CultureInfo.InvariantCulture), ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = reference, ["vnp_OrderInfo"] = "HTD PAY " + reference, ["vnp_OrderType"] = "other",
            ["vnp_Locale"] = "vn", ["vnp_ReturnUrl"] = returnUrl, ["vnp_IpAddr"] = ipAddress,
            ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"), ["vnp_ExpireDate"] = now.AddMinutes(15).ToString("yyyyMMddHHmmss")
        };
        var canonical = Canonical(fields);
        return endpoint + "?" + canonical + "&vnp_SecureHash=" + Sign(canonical, config["Vnpay:HashSecret"]!);
    }

    public bool Verify(IReadOnlyDictionary<string, string> parameters)
    {
        if (!IsConfigured || !parameters.TryGetValue("vnp_SecureHash", out var signature)) return false;
        try
        {
            var expected = Convert.FromHexString(Sign(Canonical(parameters), config["Vnpay:HashSecret"]!));
            return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature));
        }
        catch (FormatException) { return false; }
    }

    public static string Canonical(IReadOnlyDictionary<string, string> fields) => string.Join("&", fields
        .Where(x => x.Key.StartsWith("vnp_", StringComparison.Ordinal) && x.Key is not ("vnp_SecureHash" or "vnp_SecureHashType") && !string.IsNullOrEmpty(x.Value))
        .OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => WebUtility.UrlEncode(x.Key) + "=" + WebUtility.UrlEncode(x.Value)));

    public static string Sign(string canonical, string secret) => Convert.ToHexString(
        HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
}
