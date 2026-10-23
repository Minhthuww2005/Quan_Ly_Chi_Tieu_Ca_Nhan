using System.Net;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using WalletFunding.Api;

// Regression checks for the money-flow, callback and validation fixes.
// Production services run against an in-memory repository: no PostgreSQL, network or provider.
// Every money case asserts balances before/after, not only the HTTP status.

const string Secret = "QA_ONLY_VNPAY_SECRET_NOT_A_MERCHANT_KEY";
const string Pin = "123456";
var results = new List<object>();
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAILED: " + name);
    results.Add(new { name, status = "passed" });
}

var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Jwt:SigningKey"] = new string('k', 64), ["Vnpay:TmnCode"] = "TESTHTD1", ["Vnpay:HashSecret"] = Secret, ["Investment:UsdToVnd"] = "25000",
}).Build();
var protection = DataProtectionProvider.Create("WalletMoneyFlowTests");
var environment = new TestEnvironment();
var gateway = new VnpayGateway(config);
var prices = new PriceHandler();
var http = new SingleClientFactory(prices);
var store = new MemoryRepository();
var pinHash = BCrypt.Net.BCrypt.HashPassword(Pin, 4);

void Seed()
{
    store.Tables.Clear();
    store.Tables["users"] =
    [
        J.Obj(new { user_id = 1, name = "Sender", phone = "0900000001", role = "user", kycTier = "TIER_1", status = "active", pinHash }),
        J.Obj(new { user_id = 2, name = "Receiver", phone = "0900000002", role = "user", kycTier = "TIER_1", status = "active", pinHash }),
        J.Obj(new { user_id = 3, name = "Admin", phone = "0900000003", role = "admin", kycTier = "TIER_2", status = "active", pinHash }),
    ];
    store.Tables["wallets"] =
    [
        J.Obj(new { wallet_id = 1, user_id = 1, balance = 100_000_000m, status = "active" }),
        J.Obj(new { wallet_id = 2, user_id = 2, balance = 100_000_000m, status = "active" }),
        J.Obj(new { wallet_id = 3, user_id = 3, balance = 100_000_000m, status = "active" }),
    ];
    store.Tables["bankaccounts"] = [J.Obj(new { account_id = 1, bank_code = "VCB", bank_name = "Vietcombank", account_number = "0011223344", account_name = "NGUYEN VAN B", balance = 0m, status = "active", email = "b@example.test", phone = "0911111111" })];
    store.Tables["supportedbanks"] = [J.Obj(new { bank_code = "VCB", bank_name = "Vietcombank", is_active = true, display_order = 1 }), J.Obj(new { bank_code = "OLD", bank_name = "Old bank", is_active = false, display_order = 9 })];
}
decimal Balance(int user) => J.Money(store.Tables["wallets"].Single(x => J.Int(x, "user_id") == user), "balance");
decimal Total() => store.Tables["wallets"].Sum(x => J.Money(x, "balance"));

async Task<(int Status, JsonObject Body)> Call(IWalletService service, string method, string path, object? body, int user, string? key = "auto", Dictionary<string, string>? query = null)
{
    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["X-HTD-Client-IP"] = "127.0.0.1" };
    if (key == "auto") headers["Idempotency-Key"] = "qa-" + Guid.NewGuid().ToString("N");
    else if (key != null) headers["Idempotency-Key"] = key;
    var principal = user == 0 ? new ClaimsPrincipal(new ClaimsIdentity()) : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.ToString())], "Test"));
    var request = new WalletRequest(method, path, body == null ? new JsonObject() : JsonSerializer.SerializeToNode(body)!.AsObject(),
        new RequestValues(query ?? []), new RequestValues(headers), principal);
    try
    {
        await service.PrefetchAsync(request);
        var response = await service.ExecuteAsync(request);
        return (response.StatusCode, response.Data);
    }
    catch (ApiError e)
    {
        var data = J.Obj(new { success = false, message = e.Message, code = e.Code });
        if (e.Errors != null) data["errors"] = JsonSerializer.SerializeToNode(e.Errors);
        return (e.Status, data);
    }
}
TransactionService Tx() => new(store, config, protection, http, environment);
FinanceService Finance() => new(store, config, protection, http, environment);
PaymentService Payments() => new(store, config, protection, http, environment, gateway);
BankingService Banking() => new(store, config, protection, http, environment);

// ---------- 1. Investments: the client can no longer choose the credited amount ----------
Seed();
var before = Balance(1);
var created = await Call(Finance(), "POST", "/api/investments/create", new { user_id = 1, type = "Cổ phiếu mô phỏng", name = "QA Stock", amount = 5_000_000 }, 1);
var investmentId = J.Str(created.Body["investment"]!.AsObject(), "id");
var sold = await Call(Finance(), "POST", "/api/investments/sell", new { user_id = 1, id = investmentId, sellPrice = 1_000_000_000_000m }, 1);
Check(created.Status == 201 && sold.Status == 200, "Investment create/sell succeed");
Check(Balance(1) == before, "Sell ignores client sellPrice=1e12: wallet only gets the server-held value back (no money minted)");

Seed();
created = await Call(Finance(), "POST", "/api/investments/create", new { user_id = 1, type = "Cổ phiếu mô phỏng", name = "QA Stock", amount = 5_000_000 }, 1);
investmentId = J.Str(created.Body["investment"]!.AsObject(), "id");
var update = await Call(Finance(), "PUT", "/api/investments/update", new { user_id = 1, id = investmentId, currentValue = 900_000_000 }, 1);
Check(update.Status == 403, "User cannot revalue their own investment (update is back-office only)");
var adminUpdate = await Call(Finance(), "PUT", "/api/investments/update", new { user_id = 1, id = investmentId, currentValue = 5_500_000 }, 3);
var beforeSell = Balance(1);
await Call(Finance(), "POST", "/api/investments/sell", new { user_id = 1, id = investmentId, sellPrice = 99_000_000 }, 1);
Check(adminUpdate.Status == 200 && Balance(1) - beforeSell == 5_500_000, "Sell credits the server-stored value set by admin, not the client figure");

Seed();
prices.Set(("BTCUSDT", 100_000m));
created = await Call(Finance(), "POST", "/api/investments/create", new { user_id = 1, type = FinancePolicy.CryptoType, name = "bitcoin", amount = 25_000_000 }, 1);
var quantity = J.Money(created.Body["investment"]!.AsObject(), "quantity");
prices.Set(("BTCUSDT", 120_000m));
beforeSell = Balance(1);
await Call(Finance(), "POST", "/api/investments/sell", new { user_id = 1, id = J.Str(created.Body["investment"]!.AsObject(), "id"), sellPrice = 1 }, 1);
Check(quantity == 0.01m && Balance(1) - beforeSell == 30_000_000, "Crypto sell = live price x quantity computed on server (0.01 BTC x 3.0e9 VND)");

prices.Fail = true;
before = Balance(1);
var noPrice = await Call(Finance(), "POST", "/api/investments/create", new { user_id = 1, type = FinancePolicy.CryptoType, name = "bitcoin", amount = 5_000_000 }, 1);
Check(noPrice.Status == 503 && Balance(1) == before, "Price source down: 503 and wallet untouched");
prices.Fail = false;

// ---------- 2. Loans and savings: rate and size are owned by the server ----------
Seed();
var loan = await Call(Finance(), "POST", "/api/loans/create", new { user_id = 1, type = "Vay tiêu dùng", amount = 20_000_000, interestRate = 0, term = 12 }, 1);
Check(loan.Status == 201 && J.Money(loan.Body["loan"]!.AsObject(), "interestRate") == 18, "Loan ignores client interestRate=0 and uses the product rate (18%)");
var tooBig = await Call(Finance(), "POST", "/api/loans/create", new { user_id = 1, type = "Vay tiêu dùng", amount = 600_000_000, term = 12 }, 1);
Check(tooBig.Status == 400 && tooBig.Body["errors"]?["amount"] != null, "Loan above product maximum -> 400 with errors.amount");
var unknown = await Call(Finance(), "POST", "/api/loans/create", new { user_id = 1, type = "Vay mô phỏng", amount = 20_000_000, term = 12 }, 1);
Check(unknown.Status == 400 && unknown.Body["errors"]?["type"] != null, "Unknown loan product -> 400 with errors.type");
before = Balance(1);
var overCeiling = await Call(Finance(), "POST", "/api/loans/create", new { user_id = 1, type = "Vay mua xe", amount = 90_000_000, term = 24 }, 1);
Check(overCeiling.Status == 403 && Balance(1) == before, "TIER_1 outstanding ceiling (100M) blocks 20M + 90M and credits nothing");
await Call(Finance(), "POST", "/api/loans/create", new { user_id = 1, type = "Vay tiêu dùng", amount = 10_000_000, term = 12 }, 1);
await Call(Finance(), "POST", "/api/loans/create", new { user_id = 1, type = "Vay tiêu dùng", amount = 10_000_000, term = 12 }, 1);
var fourth = await Call(Finance(), "POST", "/api/loans/create", new { user_id = 1, type = "Vay tiêu dùng", amount = 10_000_000, term = 12 }, 1);
Check(fourth.Status == 403, "At most 3 active loans per user");

var savings = await Call(Finance(), "POST", "/api/savings/create", new { user_id = 1, name = "QA", amount = 1_000_000, term = 12, interestRate = 100 }, 1);
Check(savings.Status == 201 && J.Money(savings.Body["account"]!.AsObject(), "interestRate") == 7.0m, "Savings ignores client interestRate=100 and uses the term table (12 months = 7%)");
var badTerm = await Call(Finance(), "POST", "/api/savings/create", new { user_id = 1, name = "QA", amount = 1_000_000, term = 61 }, 1);
Check(badTerm.Status == 400 && badTerm.Body["errors"]?["term"] != null, "Savings term > 60 -> 400 with errors.term");

// ---------- 3. Daily transfer limit counts only outgoing money ----------
Seed();
var t1 = await Call(Tx(), "POST", "/api/transactions/transfer", new { from_user_id = 1, to_phone = "0900000002", amount = 5_000_000, pin = Pin }, 1);
var t2 = await Call(Tx(), "POST", "/api/transactions/transfer", new { from_user_id = 2, to_phone = "0900000001", amount = 10_000, pin = Pin }, 2);
Check(t1.Status == 200 && t2.Status == 200, "Receiving 5M does not consume the receiver's own 5M sending limit");
before = Balance(1);
var t3 = await Call(Tx(), "POST", "/api/transactions/transfer", new { from_user_id = 1, to_phone = "0900000002", amount = 10_000, pin = Pin }, 1);
Check(t3.Status == 403 && J.Str(t3.Body, "code") == "DAILY_LIMIT_EXCEEDED" && Balance(1) == before, "Sender over 5M/day -> 403 and balance unchanged");
var bank = await Call(Tx(), "POST", "/api/transactions/bank-transfer", new { user_id = 1, bank_code = "VCB", account_number = "0011223344", amount = 10_000, pin = Pin }, 1);
Check(bank.Status == 403 && Balance(1) == before, "Bank transfer now shares the same daily limit (was unlimited)");
var summary = await Call(Tx(), "GET", "/api/transfer/daily-summary/2", null, 2, key: null);
Check(J.Money(summary.Body["data"]!.AsObject(), "todaySent") == 10_000, "daily-summary of the receiver reports only what they sent");

// ---------- 4. Conservation and typed validation on transfer ----------
Seed();
var total = Total();
var ok = await Call(Tx(), "POST", "/api/transfer/execute", new { senderId = 1, receiverPhone = "0900000002", amount = 1_234_000, pinCode = Pin }, 1);
Check(ok.Status == 200 && Total() == total && Balance(1) == 98_766_000 && Balance(2) == 101_234_000, "Transfer conserves S+R and moves exactly A");
foreach (var (body, field, name) in new (object Body, string Field, string Name)[]
{
    (new { to_phone = "0900000002", amount = "abc", pin = Pin }, "amount", "amount not a number"),
    (new { to_phone = "0900000002", amount = 10_000.5m, pin = Pin }, "amount", "amount with decimals"),
    (new { to_phone = "0900000002", amount = 9_999, pin = Pin }, "amount", "amount below 10.000"),
    (new { amount = 10_000, pin = Pin }, "to_phone", "missing recipient"),
    (new { to_phone = "0900000002", recipient_id = 2, amount = 10_000, pin = Pin }, "to_phone", "two recipients"),
    (new { to_phone = "0900000002", amount = 10_000, pin = "12ab" }, "pin", "PIN not 6 digits"),
})
{
    var r = await Call(Tx(), "POST", "/api/transactions/transfer", body, 1);
    Check(r.Status == 400 && J.Str(r.Body, "code") == "VALIDATION_ERROR" && r.Body["errors"]?[field] != null && Total() == total && Balance(1) == 98_766_000, "Typed validation: " + name + " -> 400 errors." + field + ", no balance change");
}
var wrongPin = await Call(Tx(), "POST", "/api/transactions/transfer", new { to_phone = "0900000002", amount = 10_000, pin = "000000" }, 1);
Check(wrongPin.Status == 401 && J.Str(wrongPin.Body, "code") == "INVALID_PIN", "Well-formed wrong PIN still -> 401 INVALID_PIN (session kept by client)");

// ---------- 5. Idempotency ----------
var noKey = await Call(Tx(), "POST", "/api/transactions/transfer", new { to_phone = "0900000002", amount = 10_000, pin = Pin }, 1, key: null);
Check(noKey.Status == 400 && J.Str(noKey.Body, "code") == "IDEMPOTENCY_KEY_REQUIRED" && Balance(1) == 98_766_000, "Financial POST without Idempotency-Key -> 400, nothing debited");
var replayBody = new { to_phone = "0900000002", amount = 20_000, pin = Pin };
var first = await Call(Tx(), "POST", "/api/transactions/transfer", replayBody, 1, key: "qa-replay-0001");
var second = await Call(Tx(), "POST", "/api/transactions/transfer", replayBody, 1, key: "qa-replay-0001");
Check(first.Status == 200 && second.Body.ToJsonString() == first.Body.ToJsonString() && Balance(1) == 98_746_000, "Same key + same body replays the receipt and debits once");
var conflict = await Call(Tx(), "POST", "/api/transactions/transfer", new { to_phone = "0900000002", amount = 30_000, pin = Pin }, 1, key: "qa-replay-0001");
Check(conflict.Status == 409, "Same key + different body -> 409");
var receipt = store.Tables["payments"].Last(x => J.Str(x, "kind") == "request_receipt");
var withPin = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.SerializeToNode(replayBody)!.ToJsonString())));
Check(J.Str(receipt, "fingerprint") != withPin && !receipt.ToJsonString().Contains(Pin), "Stored receipt fingerprint no longer covers the PIN");

// ---------- 6. VNPay IPN ----------
Seed();
async Task<JsonObject> Order(int amount = 100_000)
{
    var r = await Call(Payments(), "POST", "/api/payments/vnpay/create", new { type = "wallet_topup", amount, pin = Pin }, 1);
    if (r.Status != 201) throw new Exception("order create failed: " + r.Body.ToJsonString());
    return store.Tables["payments"].Single(x => J.Str(x, "order_id") == J.Str(r.Body["order"]!.AsObject(), "order_id"));
}
Dictionary<string, string> Ipn(JsonObject order, string code = "00")
{
    var fields = new Dictionary<string, string> { ["vnp_TmnCode"] = "TESTHTD1", ["vnp_TxnRef"] = J.Str(order, "order_id"), ["vnp_Amount"] = (J.Money(order, "amount") * 100).ToString("0"), ["vnp_ResponseCode"] = code, ["vnp_TransactionStatus"] = code, ["vnp_TransactionNo"] = "1" };
    fields["vnp_SecureHash"] = VnpayGateway.Sign(VnpayGateway.Canonical(fields), Secret);
    return fields;
}
var order = await Order();
before = Balance(1);
var ipn = await Call(Payments(), "GET", "/api/payments/vnpay/ipn", null, 0, key: null, query: Ipn(order));
var again = await Call(Payments(), "GET", "/api/payments/vnpay/ipn", null, 0, key: null, query: Ipn(order));
Check(J.Str(ipn.Body, "RspCode") == "00" && J.Str(again.Body, "RspCode") == "02" && Balance(1) - before == 100_000, "Valid IPN credits once; replay -> 02");
var late = await Order();
late["expires_at"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O");
before = Balance(1);
var lateIpn = await Call(Payments(), "GET", "/api/payments/vnpay/ipn", null, 0, key: null, query: Ipn(late));
Check(J.Str(lateIpn.Body, "RspCode") == "02" && Balance(1) == before && J.Str(late, "status") == "expired" && J.Bool(late, "reconciliation_required"), "Success IPN after expires_at: no credit, order expired + flagged for reconciliation");
var tampered = Ipn(await Order()); tampered["vnp_Amount"] = "99999900";
Check(J.Str((await Call(Payments(), "GET", "/api/payments/vnpay/ipn", null, 0, key: null, query: tampered)).Body, "RspCode") == "97", "Tampered amount -> 97");
var badCreate = await Call(Payments(), "POST", "/api/payments/vnpay/create", new { type = "wallet_topup", amount = 60_000_000, pin = Pin }, 1);
Check(badCreate.Status == 400 && badCreate.Body["errors"]?["amount"] != null, "VNPay create: amount > 50M -> 400 errors.amount");
var noBill = await Call(Payments(), "POST", "/api/payments/vnpay/create", new { type = "bill_payment", amount = 100_000, pin = Pin, billing_period = "2026-13" }, 1);
Check(noBill.Status == 400 && noBill.Body["errors"]?["billing_period"] != null, "VNPay create: malformed billing_period -> 400 errors.billing_period");
noBill = await Call(Payments(), "POST", "/api/payments/vnpay/create", new { type = "bill_payment", amount = 100_000, pin = Pin }, 1);
Check(noBill.Status == 400 && noBill.Body["errors"]?["bill_code"] != null, "VNPay create: bill_payment without bill_code -> 400 errors.bill_code");

// ---------- 7. Banking ----------
Seed();
var put = await Call(Banking(), "PUT", "/api/banks/supported/VCB", new { bank_name = "Vietcombank (đổi tên)" }, 3);
Check(put.Status == 200 && J.Str(store.Tables["supportedbanks"][0], "bank_name") == "Vietcombank (đổi tên)", "Partial PUT bank updates the field (was 404 because of dangling else)");
var delete = await Call(Banking(), "DELETE", "/api/banks/supported/OLD", null, 3);
Check(delete.Status == 200 && store.Tables["supportedbanks"].All(x => J.Str(x, "bank_code") != "OLD"), "DELETE bank really removes it (was a silent no-op)");
var search = await Call(Banking(), "GET", "/api/bank-accounts/search", null, 1, key: null, query: new() { ["q"] = "NGUYEN" });
Check(search.Status == 403, "Non-admin cannot enumerate bank accounts via search");
var verify = await Call(Banking(), "POST", "/api/bank-accounts/verify", new { bank_code = "VCB", account_number = "0011223344" }, 1);
var verified = verify.Body["data"]!.AsObject();
Check(verify.Status == 200 && J.Str(verified, "account_name") == "NGUYEN VAN B" && !verified.ContainsKey("balance") && !verified.ContainsKey("email") && !verified.ContainsKey("account_id"), "Verify returns only payee confirmation fields");

// ---------- 8. External price fetched before the write lock ----------
Seed();
prices.Set(("BTCUSDT", 100_000m));
var recording = new LockOrderRepository(store, prices);
var controller = new FinanceController(new FinanceService(recording, config, protection, http, environment), recording);
var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "Test")) };
context.Request.Method = "POST"; context.Request.Path = "/api/investments/create"; context.Request.Headers["Idempotency-Key"] = "qa-lock-order-1";
context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = FinancePolicy.CryptoType, name = "btc", amount = 5_000_000 })));
context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpRequestBodyDetectionFeature>(new BodyDetection());
controller.ControllerContext = new ControllerContext { HttpContext = context };
var result = await controller.Execute();
Check(result is JsonResult { StatusCode: 201 } && recording.PriceCallsAtBegin == 1 && recording.PriceCallsDuringTransaction == 0, "Binance price is fetched before Begin(write): no external call while holding the advisory lock");

// ---------- 9. Product rules served to the UI match what create applies ----------
Seed();
var loanProducts = await Call(Finance(), "GET", "/api/loans/products", null, 1, key: null);
var consumer = loanProducts.Body["products"]!.AsArray().OfType<JsonObject>().Single(x => J.Str(x, "type") == "Vay tiêu dùng");
var appliedLoan = await Call(Finance(), "POST", "/api/loans/create", new { user_id = 1, type = "Vay tiêu dùng", amount = 10_000_000, term = 12 }, 1);
Check(loanProducts.Status == 200 && J.Money(consumer, "annualRate") == J.Money(appliedLoan.Body["loan"]!.AsObject(), "interestRate") && J.Int(consumer, "maxTermMonths") == 60, "GET /loans/products shows the same rate that loans/create applies");
var savingsProducts = await Call(Finance(), "GET", "/api/savings/products", null, 1, key: null);
var tiers = savingsProducts.Body["tiers"]!.AsArray().OfType<JsonObject>().ToArray();
Check(savingsProducts.Status == 200 && Enumerable.Range(0, 61).All(term => J.Money(tiers.Single(t => term >= J.Int(t, "fromTerm") && term <= J.Int(t, "toTerm")), "annualRate") == FinancePolicy.SavingsRate(term)), "GET /savings/products covers every term 0-60 with the rate create applies");

// ---------- 10. Flexible savings (term 0) earn interest for the months held ----------
Seed();
var flexible = await Call(Finance(), "POST", "/api/savings/create", new { user_id = 1, name = "QA linh hoạt", amount = 12_000_000, term = 0 }, 1);
var flexibleId = J.Str(flexible.Body["account"]!.AsObject(), "id");
store.Tables["savingsaccounts"].Single(x => J.Str(x, "id") == flexibleId)["startDate"] = DateTime.UtcNow.AddDays(-95).ToString("yyyy-MM-dd");
before = Balance(1);
var closed = await Call(Finance(), "POST", "/api/savings/close", new { user_id = 1, id = flexibleId }, 1);
Check(closed.Status == 200 && J.Money(closed.Body, "interest") == 126_000 && Balance(1) - before == 12_126_000, "Flexible savings held 3 months pays 12M x 4.2% x 3/12 = 126.000 (was always 0)");

// ---------- 11. PIN is verified before the write lock, and re-checked inside it ----------
Seed();
var callOrder = new List<string>();
var pinRepo = new OrderRepository(store, callOrder);
var txController = new TransactionsController(new TransactionService(pinRepo, config, protection, http, environment), pinRepo);
JsonResult? Run(TransactionsController c, object body, string key)
{
    var ctx = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "Test")) };
    ctx.Request.Method = "POST"; ctx.Request.Path = "/api/transactions/transfer"; ctx.Request.Headers["Idempotency-Key"] = key;
    ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body)));
    ctx.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpRequestBodyDetectionFeature>(new BodyDetection());
    c.ControllerContext = new ControllerContext { HttpContext = ctx };
    return c.Execute().GetAwaiter().GetResult() as JsonResult;
}
var pinOk = Run(txController, new { to_phone = "0900000002", amount = 10_000, pin = Pin }, "qa-pin-callOrder-1");
Check(pinOk?.StatusCode == 200 && callOrder.IndexOf("peek:users") >= 0 && callOrder.IndexOf("peek:users") < callOrder.IndexOf("begin:write"), "PIN hash is read and verified before Begin(write) takes the advisory lock");
callOrder.Clear();
pinRepo.OnBegin = () => store.Tables["users"].Single(x => J.Int(x, "user_id") == 1)["pinHash"] = BCrypt.Net.BCrypt.HashPassword("654321", 4);
before = Balance(1);
try { Run(new TransactionsController(new TransactionService(pinRepo, config, protection, http, environment), pinRepo), new { to_phone = "0900000002", amount = 10_000, pin = Pin }, "qa-pin-callOrder-2"); Check(false, "PIN changed after pre-check must be rejected"); }
catch (ApiError e) { Check(e.Status == 401 && e.Code == "INVALID_PIN" && Balance(1) == before, "PIN changed between pre-check and lock is verified again and rejected; balance unchanged"); }
pinRepo.OnBegin = null;

var output = Path.GetFullPath(args.FirstOrDefault() ?? "docs/wallet-test-results/money-flow-results.json");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { generatedAt = DateTimeOffset.UtcNow, passed = results.Count, scope = "production services/controller with in-memory repository; no PostgreSQL, network or real provider", results }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
Console.WriteLine($"PASS: {results.Count} money-flow/callback/validation regression checks -> {output}");

sealed class MemoryRepository : IWalletRepository
{
    public Dictionary<string, List<JsonObject>> Tables { get; } = [];
    public Task Begin(bool write) => Task.CompletedTask;
    public Task<List<JsonObject>> All(string table) => Task.FromResult(Tables.TryGetValue(table, out var list) ? list : Tables[table] = []);
    public async Task Add(string table, JsonObject data) => (await All(table)).Add(data);
    public async Task<int> Next(string table, string field) => (await All(table)).Select(x => J.Int(x, field)).DefaultIfEmpty().Max() + 1;
    public async Task<string> NextCode(string table, string prefix) => prefix + ((await All(table)).Count + 1).ToString("D4");
    public Task Commit() => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

sealed class OrderRepository(MemoryRepository inner, List<string> order) : IWalletRepository
{
    public Action? OnBegin { get; set; }
    public Task Begin(bool write) { order.Add(write ? "begin:write" : "begin:read"); OnBegin?.Invoke(); return Task.CompletedTask; }
    public Task<JsonObject?> Peek(string table, string field, string value) { order.Add("peek:" + table); return ((IWalletRepository)inner).Peek(table, field, value); }
    public Task<List<JsonObject>> All(string table) => inner.All(table);
    public Task Add(string table, JsonObject data) => inner.Add(table, data);
    public Task<int> Next(string table, string field) => inner.Next(table, field);
    public Task<string> NextCode(string table, string prefix) => inner.NextCode(table, prefix);
    public Task Commit() => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

sealed class LockOrderRepository(MemoryRepository inner, PriceHandler prices) : IWalletRepository
{
    private bool inTransaction;
    public int PriceCallsAtBegin { get; private set; } = -1;
    public int PriceCallsDuringTransaction { get; private set; }
    public Task Begin(bool write)
    {
        PriceCallsAtBegin = prices.Calls; inTransaction = write;
        prices.OnCall = () => { if (inTransaction) PriceCallsDuringTransaction++; };
        return Task.CompletedTask;
    }
    public Task<List<JsonObject>> All(string table) => inner.All(table);
    public Task Add(string table, JsonObject data) => inner.Add(table, data);
    public Task<int> Next(string table, string field) => inner.Next(table, field);
    public Task<string> NextCode(string table, string prefix) => inner.NextCode(table, prefix);
    public Task Commit() { inTransaction = false; return Task.CompletedTask; }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

sealed class PriceHandler : HttpMessageHandler
{
    private Dictionary<string, decimal> usd = [];
    public bool Fail { get; set; }
    public int Calls { get; set; }
    public Action? OnCall { get; set; }
    public void Set(params (string Symbol, decimal Usd)[] values) { usd = values.ToDictionary(x => x.Symbol, x => x.Usd); Calls = 0; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++; OnCall?.Invoke();
        if (Fail) throw new HttpRequestException("price source down");
        var body = JsonSerializer.Serialize(usd.Select(x => new { symbol = x.Key, price = x.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }
}

sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

sealed class TestEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "WalletMoneyFlowTests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

sealed class BodyDetection : Microsoft.AspNetCore.Http.Features.IHttpRequestBodyDetectionFeature
{
    public bool CanHaveBody => true;
}
