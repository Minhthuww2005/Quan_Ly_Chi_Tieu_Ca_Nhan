using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;

namespace WalletFunding.Api;

public abstract class WalletServiceBase(IWalletRepository repository, IConfiguration configuration, IDataProtectionProvider dataProtection, IHttpClientFactory httpFactory, IHostEnvironment hostEnvironment) : IWalletService
{
    protected readonly IWalletRepository store = repository;
    protected readonly IConfiguration config = configuration;
    protected readonly IDataProtectionProvider protection = dataProtection;
    protected readonly IHttpClientFactory http = httpFactory;
    protected readonly IHostEnvironment environment = hostEnvironment;
    public abstract Task<ServiceResponse> ExecuteAsync(WalletRequest context);
    // Services whose money operations are protected by a PIN verify it in PrefetchAsync.
    protected virtual bool VerifiesPin => false;
    private (int UserId, string Hash, string Pin)? verifiedPin;

    // Runs before the controller opens the write transaction. BCrypt (~190 ms) is checked here so it no longer
    // runs while every other write waits on the global advisory lock. Pin() re-checks inside the transaction
    // that the stored hash is unchanged, so a PIN changed in between still has to verify again.
    public virtual async Task PrefetchAsync(WalletRequest context)
    {
        if (!VerifiesPin || context.Method == "GET" || context.Principal.Identity?.IsAuthenticated != true) return;
        var data = context.Body;
        var pin = J.Str(data, "pin", J.Str(data, "pinCode", J.Str(data, "pin_code")));
        if (pin.Length == 0 || !int.TryParse(context.Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var principalId)) return;
        var id = J.Int(data, "user_id", J.Int(data, "userId", J.Int(data, "senderId", J.Int(data, "from_user_id", principalId))));
        var user = await store.Peek("users", "user_id", id.ToString());
        var hash = user == null ? "" : J.Str(user, "pinHash");
        if (hash.Length > 0 && Verify(pin, hash)) verifiedPin = (id, hash, pin);
    }
    protected WalletRequest request = null!;
    protected int StatusCode = 200;
    protected JsonObject body = [];
    protected string[] parts = [];
    protected JsonObject? current;
    protected string Method => request.Method;
    protected string Area => parts[0];
    protected string Action => parts.Length > 1 ? parts[1] : "";
    protected int UserId => J.Int(current!, "user_id");
    protected int RequestedId => J.Int(body, "user_id", J.Int(body, "userId", J.Int(body, "senderId", J.Int(body, "from_user_id", UserId))));
    protected bool Admin => J.Str(current!, "role") == "admin";
    protected static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");
    protected static string Now => DateTimeOffset.UtcNow.ToString("O");
    protected static JsonObject Ok(string message = "Thành công") => J.Obj(new { success = true, message });
    protected static void Require(bool condition, string message, int status = 400, string code = "VALIDATION_ERROR") { if (!condition) throw new ApiError(status, message, code); }
    protected void Required(params string[] names) { foreach (var name in names) Require(!string.IsNullOrWhiteSpace(J.Str(body, name)), "Thiếu thông tin: " + name); }
    protected static bool Blocked(JsonObject user) => new[] { "blocked", "closed", "frozen", "suspicious", "inactive", "suspended" }.Contains(J.Str(user, "accountStatus", J.Str(user, "status", "active")).ToLowerInvariant());
    protected static decimal Amount(JsonObject data, string name = "amount", decimal minimum = 1)
    {
        var value = J.Money(data, name); Require(value >= minimum && value <= 1_000_000_000_000 && decimal.Truncate(value) == value, "Số tiền không hợp lệ"); return value;
    }
    private static readonly JsonSerializerOptions BindOptions = new(JsonSerializerDefaults.Web) { NumberHandling = JsonNumberHandling.AllowReadingFromString };

    // Binds the request body to a typed contract and runs DataAnnotations + IValidatableObject.
    // Failures become 400 VALIDATION_ERROR with field errors (ValidationProblemDetails shape).
    protected T Bind<T>() where T : class, new()
    {
        T model;
        try { model = body.Deserialize<T>(BindOptions) ?? new T(); }
        catch (JsonException e)
        {
            var field = string.IsNullOrEmpty(e.Path) ? "body" : e.Path.TrimStart('$', '.');
            throw ValidationFailed(new Dictionary<string, string[]> { [field] = ["Giá trị không đúng kiểu dữ liệu"] });
        }
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true))
        {
            var errors = results
                .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : ["body"]).Select(m => (Field: JsonName<T>(m), Message: r.ErrorMessage ?? "Không hợp lệ")))
                .GroupBy(x => x.Field).ToDictionary(g => g.Key, g => g.Select(x => x.Message).Distinct().ToArray());
            throw ValidationFailed(errors);
        }
        return model;
    }
    private static ApiError ValidationFailed(Dictionary<string, string[]> errors) =>
        new(400, errors.Values.First()[0], "VALIDATION_ERROR") { Errors = errors };
    private static string JsonName<T>(string member) =>
        typeof(T).GetProperty(member)?.GetCustomAttributes(typeof(JsonPropertyNameAttribute), false).OfType<JsonPropertyNameAttribute>().FirstOrDefault()?.Name ?? member;

    protected async Task<JsonObject> Find(string table, string field, string value) => (await store.All(table)).FirstOrDefault(x => J.Str(x, field) == value) ?? throw new ApiError(404, "Không tìm thấy dữ liệu");
    protected async Task<JsonObject> User(int id) => await Find("users", "user_id", id.ToString());
    protected async Task<JsonObject> Wallet(int id) => await Find("wallets", "user_id", id.ToString());
    protected void Own(int id) => Require(id == UserId || Admin, "Không có quyền truy cập", 403);
    protected async Task<JsonObject> Owned(string table, string field, string value)
    {
        var d = await Find(table, field, value); Own(J.Int(d, "user_id", J.Int(d, "ownerId"))); return d;
    }
    protected static bool Verify(string plain, string hash)
    {
        if (string.IsNullOrEmpty(plain) || string.IsNullOrEmpty(hash)) return false;
        try { return BCrypt.Net.BCrypt.Verify(plain, hash); }
        catch (BCrypt.Net.SaltParseException) { return false; }
        catch (ArgumentException) { return false; }
    }
    protected string Token(JsonObject user) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        issuer: "WalletFunding", audience: "WalletFunding.Web", claims: [new Claim(ClaimTypes.NameIdentifier, J.Str(user, "user_id")), new Claim(ClaimTypes.Role, J.Str(user, "role", "user"))],
        expires: DateTime.UtcNow.AddHours(8), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256)));
    protected string Encrypt(string value) => protection.CreateProtector("SensitiveFields.v1").Protect(value);

    protected async Task Prepare(WalletRequest context)
    {
        request = context; body = context.Body; parts = context.Path[5..].Split('/', StringSplitOptions.RemoveEmptyEntries);
        Require(parts.Length > 0, "Endpoint không tồn tại", 404);
        var anonymous = Area == "auth" && new[] { "login", "register", "forgot-password", "reset-password", "google", "config" }.Contains(Action)
            || Area == "admin" && Action == "login"
            || Area == "banks" && Action == "supported" && Method == "GET" && parts.Length == 2
            || Area == "webhook";
        anonymous |= Area == "payments" && Action == "vnpay" && parts.ElementAtOrDefault(2) is "ipn" or "verify";
        if (!anonymous)
        {
            Require(request.Principal.Identity?.IsAuthenticated == true, "Vui lòng đăng nhập", 401);
            Require(int.TryParse(request.Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id), "Token không hợp lệ", 401);
            current = await User(id); Require(!Blocked(current), "Tài khoản đã bị khóa", 403);
            Own(RequestedId);
            if (Area == "admin" || Area == "seed" || Area == "banks" && Method != "GET") Require(Admin, "Yêu cầu quyền quản trị", 403);
        }
    }
    protected async Task Pin(JsonObject user, string pin)
    {
        Require(!Blocked(user), "Tài khoản đã bị khóa", 403);
        Require(!string.IsNullOrEmpty(J.Str(user, "pinHash")), "Bạn chưa thiết lập mã PIN");
        var hash = J.Str(user, "pinHash");
        var preVerified = verifiedPin is { } v && v.UserId == J.Int(user, "user_id") && v.Hash == hash && v.Pin == pin;
        Require(preVerified || Verify(pin, hash), "Mã PIN không đúng", 401, "INVALID_PIN");
        await Task.CompletedTask;
    }
    protected async Task<ServiceResponse> ExecuteWithReceipt(WalletRequest context, Func<Task<JsonObject>> operation)
    {
        await Prepare(context);
        var key = context.Headers["Idempotency-Key"];
        var financial = Method != "GET" && (Area is "transactions" or "transfer" or "savings" or "investments" or "loans" or "insurance" or "payments" || Area == "bank" && Action == "topup");
        string? fingerprint = null;
        string? receiptKey = null;
        if (financial)
        {
            Require(key.Length > 0, "Thiếu Idempotency-Key cho thao tác tài chính", 400, "IDEMPOTENCY_KEY_REQUIRED");
            Require(key.Length is >= 8 and <= 128, "Idempotency-Key không hợp lệ");
            receiptKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(UserId + ":" + Method + ":" + context.Path + ":" + key)));
            fingerprint = Fingerprint(body);
            var receipt = (await store.All("payments")).FirstOrDefault(x => J.Str(x, "kind") == "request_receipt" && J.Str(x, "request_key") == receiptKey);
            if (receipt != null)
            {
                Require(J.Str(receipt, "fingerprint") == fingerprint, "Mã yêu cầu đã dùng cho dữ liệu khác", 409);
                return new ServiceResponse(receipt["response"]!.DeepClone().AsObject(), J.Int(receipt, "status_code", 200));
            }
        }
        var response = await operation();
        if (receiptKey != null)
            await store.Add("payments", J.Obj(new { kind = "request_receipt", request_key = receiptKey, fingerprint, user_id = UserId, status_code = StatusCode, response, created_at = Now }));
        return new ServiceResponse(response, StatusCode);
    }

    // Secrets never enter the stored receipt: a 6-digit PIN hashed alone is trivially reversible.
    private static readonly string[] SecretFields = ["pin", "pinCode", "pin_code", "old_pin", "oldPinCode", "new_pin", "password"];
    protected static string Fingerprint(JsonObject data)
    {
        var copy = data.DeepClone().AsObject();
        foreach (var field in SecretFields) copy.Remove(field);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(copy.ToJsonString())));
    }

    protected async Task<JsonObject> Record(JsonObject wallet, decimal amount, string type, string title, string description, string? reference = null)
    {
        var d = J.Obj(new { trans_id = await store.Next("transactions", "trans_id"), wallet_id = J.Int(wallet, "wallet_id"), timestamp = Now, amount, type, status = "success", title, description, icon = amount >= 0 ? "💰" : "💸", color = amount >= 0 ? "#10B981" : "#3B82F6", reference = reference ?? Guid.NewGuid().ToString("N") });
        await store.Add("transactions", d); return d;
    }
    protected static void Debit(JsonObject wallet, decimal amount)
    {
        new WalletAccount(wallet).Debit(amount);
    }
    protected static void Credit(JsonObject wallet, decimal amount)
    {
        new WalletAccount(wallet).Credit(amount);
    }
    protected JsonObject Paginate(JsonObject[] list, string field)
    {
        var limit = Math.Clamp(int.TryParse(request.Query["limit"], out var l) ? l : 100, 1, 200);
        var page = Math.Max(1, int.TryParse(request.Query["page"], out var p) ? p : 1);
        var result = J.Obj(new { success = true, pagination = new { total = list.Length, page, limit, pages = (int)Math.Ceiling((double)list.Length / limit) } });
        result[field] = System.Text.Json.JsonSerializer.SerializeToNode(list.Skip((page - 1) * limit).Take(limit).ToArray());
        return result;
    }
    // Money that LEFT the wallet today for this type (amount < 0). Incoming transfers must not
    // consume the sender limit of the person who received them.
    protected async Task<decimal> SentToday(JsonObject wallet, string type) =>
        (await TodayRecords(wallet, type)).Where(x => J.Money(x, "amount") < 0).Sum(x => -J.Money(x, "amount"));
    // Money that ENTERED the wallet today for this type (amount > 0), e.g. deposit limits.
    protected async Task<decimal> ReceivedToday(JsonObject wallet, string type) =>
        (await TodayRecords(wallet, type)).Where(x => J.Money(x, "amount") > 0).Sum(x => J.Money(x, "amount"));
    private async Task<IEnumerable<JsonObject>> TodayRecords(JsonObject wallet, string type) =>
        (await store.Where("transactions", "wallet_id", J.Str(wallet, "wallet_id"))).Where(x => J.Str(x, "type") == type && J.Str(x, "status") == "success" && J.Str(x, "timestamp").StartsWith(Today));
    protected async Task RequireTransferLimit(JsonObject user, JsonObject wallet, decimal amount)
    {
        var sent = await SentToday(wallet, "transfer");
        Require(sent + amount <= DailyLimit(user), "Vượt hạn mức chuyển tiền trong ngày", 403, "DAILY_LIMIT_EXCEEDED");
    }
    protected static decimal DailyLimit(JsonObject user) => J.Str(user, "kycTier") == "TIER_2" ? 50_000_000 : 5_000_000;
    protected async Task Notify(int userId, string title, string message)
    {
        var user = await User(userId);
        if (user["settings"] is JsonObject settings && settings.ContainsKey("notificationsEnabled") && !J.Bool(settings, "notificationsEnabled")) return;
        await store.Add("notifications", J.Obj(new { notification_id = await store.Next("notifications", "notification_id"), user_id = userId, type = "transaction", title, message, createdAt = Now, isSeen = false }));
    }
    protected static string NormalizeName(string value) => string.Concat(value.Normalize(System.Text.NormalizationForm.FormD).Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))).Replace('đ', 'd').Replace('Đ', 'D').ToUpperInvariant();
}
