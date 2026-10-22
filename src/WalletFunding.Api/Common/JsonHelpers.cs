using System.Text.Json.Nodes;
namespace WalletFunding.Api;
public static class J
{
    public static string Str(JsonObject d, string key, string fallback = "") => d[key]?.ToString() ?? fallback;
    public static int Int(JsonObject d, string key, int fallback = 0) => int.TryParse(Str(d, key), out var v) ? v : fallback;
    public static decimal Money(JsonObject d, string key) => decimal.TryParse(Str(d, key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    public static bool Bool(JsonObject d, string key) => bool.TryParse(Str(d, key), out var v) && v;
    public static JsonObject Obj(object value) => System.Text.Json.JsonSerializer.SerializeToNode(value)!.AsObject();
    public static JsonObject Public(JsonObject value, params string[] fields)
    {
        var result = value.DeepClone().AsObject();
        foreach (var f in fields.Concat(new[] { "password", "pinHash", "passwordResetToken", "resetToken", "payment_token", "card_number_encrypted", "national_id_encrypted", "date_of_birth_encrypted", "address_encrypted", "phone_encrypted", "document_number_encrypted" })) result.Remove(f);
        return result;
    }
}

public sealed class ApiError(int status, string message, string code = "VALIDATION_ERROR") : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    // Field-level errors in the same shape as ValidationProblemDetails.Errors.
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
}
