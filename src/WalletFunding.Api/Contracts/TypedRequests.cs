using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WalletFunding.Api;

// Typed contracts for the money-moving endpoints. The routes keep their existing JSON shape
// (including the aliases the React client already sends); the body is bound to these classes
// and validated with DataAnnotations + IValidatableObject before the service touches a wallet.

public static class MoneyRules
{
    public const string Max = "1000000000000";

    public static IEnumerable<ValidationResult> WholeVnd(decimal? amount, string member)
    {
        if (amount is { } value && decimal.Truncate(value) != value)
            yield return new ValidationResult("Số tiền phải là số nguyên VND", [member]);
    }

    public static IEnumerable<ValidationResult> Pin(string? pin, string member)
    {
        if (string.IsNullOrEmpty(pin)) yield return new ValidationResult("Vui lòng nhập mã PIN", [member]);
        else if (pin.Length != 6 || !pin.All(char.IsAsciiDigit)) yield return new ValidationResult("PIN phải gồm 6 chữ số", [member]);
    }
}

public sealed class TransferRequest : IValidatableObject
{
    [JsonPropertyName("to_phone")] public string? ToPhone { get; init; }
    [JsonPropertyName("receiverPhone")] public string? ReceiverPhone { get; init; }
    [JsonPropertyName("recipient_id")][Range(1, int.MaxValue, ErrorMessage = "Người nhận không hợp lệ")] public int? RecipientId { get; init; }

    [JsonPropertyName("amount")]
    [Required(ErrorMessage = "Vui lòng nhập số tiền")]
    [Range(typeof(decimal), "10000", MoneyRules.Max, ErrorMessage = "Số tiền chuyển phải từ 10.000 VND đến 1.000 tỷ VND")]
    public decimal? Amount { get; init; }

    [JsonPropertyName("pin")] public string? Pin { get; init; }
    [JsonPropertyName("pinCode")] public string? PinCode { get; init; }
    [JsonPropertyName("message")][StringLength(255)] public string? Message { get; init; }
    [JsonPropertyName("description")][StringLength(255)] public string? Description { get; init; }

    [JsonIgnore] public string? Phone => string.IsNullOrWhiteSpace(ToPhone) ? ReceiverPhone : ToPhone;
    [JsonIgnore] public string PinValue => Pin ?? PinCode ?? "";
    [JsonIgnore] public string Note => !string.IsNullOrWhiteSpace(Message) ? Message : !string.IsNullOrWhiteSpace(Description) ? Description : "Chuyển tiền";

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        var hasPhone = !string.IsNullOrWhiteSpace(Phone);
        if (hasPhone == RecipientId.HasValue)
            yield return new ValidationResult("Chọn đúng một người nhận: số điện thoại hoặc recipient_id", ["to_phone"]);
        foreach (var r in MoneyRules.WholeVnd(Amount, "amount")) yield return r;
        foreach (var r in MoneyRules.Pin(PinValue, "pin")) yield return r;
    }
}

public sealed class VnpayCreateRequest : IValidatableObject
{
    [JsonPropertyName("type")][RegularExpression("^(wallet_topup|bill_payment)$", ErrorMessage = "Loại thanh toán không hợp lệ")] public string Type { get; init; } = "wallet_topup";

    [JsonPropertyName("amount")]
    [Required(ErrorMessage = "Vui lòng nhập số tiền")]
    [Range(typeof(decimal), "10000", "50000000", ErrorMessage = "Số tiền từ 10.000 đến 50.000.000 VND mỗi giao dịch")]
    public decimal? Amount { get; init; }

    [JsonPropertyName("pin")] public string? Pin { get; init; }
    [JsonPropertyName("pin_code")] public string? PinCode { get; init; }
    [JsonPropertyName("bill_type")][StringLength(50)] public string? BillType { get; init; }
    [JsonPropertyName("bill_code")][StringLength(50)] public string? BillCode { get; init; }
    [JsonPropertyName("billing_period")][RegularExpression("^[0-9]{4}-(0[1-9]|1[0-2])$", ErrorMessage = "Kỳ thanh toán có dạng yyyy-MM")] public string? BillingPeriod { get; init; }

    [JsonIgnore] public string PinValue => Pin ?? PinCode ?? "";

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        foreach (var r in MoneyRules.WholeVnd(Amount, "amount")) yield return r;
        foreach (var r in MoneyRules.Pin(PinValue, "pin")) yield return r;
        if (Type == "bill_payment" && (string.IsNullOrWhiteSpace(BillType) || string.IsNullOrWhiteSpace(BillCode)))
            yield return new ValidationResult("Thanh toán hóa đơn cần bill_type và bill_code", ["bill_code"]);
    }
}

public sealed class SavingsCreateRequest : IValidatableObject
{
    [JsonPropertyName("name")][Required(ErrorMessage = "Vui lòng nhập tên sổ")][StringLength(100)] public string? Name { get; init; }

    [JsonPropertyName("amount")]
    [Required(ErrorMessage = "Vui lòng nhập số tiền")]
    [Range(typeof(decimal), "100000", MoneyRules.Max, ErrorMessage = "Số tiền gửi phải từ 100.000 VND đến 1.000 tỷ VND")]
    public decimal? Amount { get; init; }

    [JsonPropertyName("term")]
    [Required(ErrorMessage = "Vui lòng chọn kỳ hạn")]
    [Range(0, FinancePolicy.SavingsMaxTermMonths, ErrorMessage = "Kỳ hạn từ 0 đến 60 tháng")]
    public int? Term { get; init; }

    // Accepted for compatibility with the client form but never used to compute interest.
    [JsonPropertyName("interestRate")] public decimal? RequestedRate { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context) => MoneyRules.WholeVnd(Amount, "amount");
}

public sealed class LoanCreateRequest : IValidatableObject
{
    [JsonPropertyName("type")][Required(ErrorMessage = "Vui lòng chọn loại vay")] public string? Type { get; init; }

    [JsonPropertyName("amount")]
    [Required(ErrorMessage = "Vui lòng nhập số tiền vay")]
    [Range(typeof(decimal), "10000000", MoneyRules.Max, ErrorMessage = "Số tiền vay phải từ 10.000.000 VND và không vượt hạn mức sản phẩm")]
    public decimal? Amount { get; init; }

    [JsonPropertyName("term")][Required(ErrorMessage = "Vui lòng chọn kỳ hạn")][Range(1, 360, ErrorMessage = "Kỳ hạn không hợp lệ")] public int? Term { get; init; }

    // Accepted for compatibility with the client form but never used: the product decides the rate.
    [JsonPropertyName("interestRate")] public decimal? RequestedRate { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        foreach (var r in MoneyRules.WholeVnd(Amount, "amount")) yield return r;
        if (Type is null || !FinancePolicy.Loans.TryGetValue(Type, out var product))
        {
            yield return new ValidationResult("Loại vay không được hỗ trợ: " + string.Join(", ", FinancePolicy.Loans.Keys), ["type"]);
            yield break;
        }
        if (Amount > product.MaxAmount) yield return new ValidationResult($"{Type} tối đa {product.MaxAmount:N0} VND", ["amount"]);
        if (Term > product.MaxTermMonths) yield return new ValidationResult($"{Type} tối đa {product.MaxTermMonths} tháng", ["term"]);
    }
}
