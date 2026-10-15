using System.Text.Json.Nodes;

namespace WalletFunding.Api;

public sealed class FinanceService(IWalletRepository repository, IConfiguration configuration, IDataProtectionProvider protection, IHttpClientFactory http, IHostEnvironment environment) : WalletServiceBase(repository, configuration, protection, http, environment)
{
    public override async Task<ServiceResponse> ExecuteAsync(WalletRequest context)
    {
        return await ExecuteWithReceipt(context, Finance);
    }

    private async Task<JsonObject> Finance()
    {
        var table = Area switch { "savings" => "savingsaccounts", "loans" => "loanaccounts", "insurance" => "insurances", _ => "investments" };
        if (Method == "GET")
        {
            Require(int.TryParse(Action, out var id), "Mã người dùng không hợp lệ"); Own(id);
            var docs = (await store.All(table)).Where(x => J.Int(x, "user_id") == id).Select(x => J.Public(x)).ToArray();
            var active = docs.Where(x => J.Str(x, "status") == "active").ToArray();
            JsonObject summary;
            string field;
            if (Area == "savings")
            {
                field = "accounts"; foreach (var d in docs) d["expectedInterest"] = J.Money(d, "balance") * J.Money(d, "interestRate") * J.Int(d, "term") / 1200;
                summary = J.Obj(new { totalBalance = active.Sum(x => J.Money(x, "balance")), totalInterest = active.Sum(x => J.Money(x, "expectedInterest")), activeCount = active.Length });
            }
            else if (Area == "loans")
            {
                field = "loans"; foreach (var d in docs) d["paidPercent"] = J.Money(d, "amount") > 0 ? (J.Money(d, "amount") - J.Money(d, "remainingAmount")) / J.Money(d, "amount") * 100 : 0;
                var borrowed = docs.Sum(x => J.Money(x, "amount")); var remaining = docs.Sum(x => J.Money(x, "remainingAmount"));
                summary = J.Obj(new { totalBorrowed = borrowed, totalPaid = borrowed - remaining, totalRemaining = remaining, activeCount = active.Length });
            }
            else if (Area == "insurance")
            {
                field = "policies"; foreach (var d in docs) d["daysRemaining"] = DateTime.TryParse(J.Str(d, "endDate"), out var end) ? Math.Ceiling((end - DateTime.UtcNow).TotalDays) : 0;
                summary = J.Obj(new { totalPremium = active.Sum(x => J.Money(x, "premium")), totalCoverage = active.Sum(x => J.Money(x, "coverage")), activeCount = active.Length });
            }
            else
            {
                field = "investments"; var invested = active.Sum(x => J.Money(x, "amount")); var value = active.Sum(x => J.Money(x, "currentValue"));
                summary = J.Obj(new { totalInvested = invested, totalValue = value, totalProfit = value - invested, overallReturn = invested > 0 ? (value - invested) / invested * 100 : 0 });
            }
            var result = J.Obj(new { success = true, summary }); result[field] = System.Text.Json.JsonSerializer.SerializeToNode(docs); return result;
        }
        Require(Method is "POST" or "PUT", "Endpoint không tồn tại", 404);
        var wallet = await Wallet(RequestedId);
        if (Action == "create")
        {
            var record = J.Obj(new { user_id = RequestedId, status = "active", created_at = Now, startDate = Today });
            string field;
            if (Area == "savings")
            {
                Required("name", "term", "interestRate"); var amount = Amount(body, minimum: 100_000); var term = J.Int(body, "term"); Require(term is >= 0 and <= 60, "Kỳ hạn không hợp lệ"); var rate = Rate();
                Debit(wallet, amount); record["id"] = await store.NextCode(table, "SAV"); record["name"] = J.Str(body, "name"); record["balance"] = amount; record["interestRate"] = rate; record["term"] = term; record["maturityDate"] = DateTime.UtcNow.AddMonths(term).ToString("yyyy-MM-dd"); record["expectedInterest"] = amount * rate * term / 1200;
                await Record(wallet, -amount, "savings", "Mở sổ tiết kiệm", J.Str(body, "name")); field = "account";
            }
            else if (Area == "investments")
            {
                Required("name", "type"); var amount = Amount(body, minimum: 5_000_000); Debit(wallet, amount);
                record["id"] = await store.NextCode(table, "INV"); record["type"] = J.Str(body, "type"); record["name"] = J.Str(body, "name"); record["amount"] = amount; record["currentValue"] = amount; record["profit"] = 0; record["profitPercent"] = 0; record["purchaseDate"] = Today;
                if (J.Str(body, "type") == "Tiền điện tử")
                {
                    var price = await CryptoPrice(J.Str(body, "name")); record["purchasePrice"] = price; record["quantity"] = amount / price;
                }
                await Record(wallet, -amount, "investment", "Đầu tư", J.Str(body, "name")); field = "investment";
            }
            else if (Area == "loans")
            {
                Required("type", "term", "interestRate"); var amount = Amount(body, minimum: 10_000_000); var term = Term(360); var rate = Rate();
                var monthly = (double)rate / 1200; var payment = monthly == 0 ? amount / term : (decimal)((double)amount * monthly * Math.Pow(1 + monthly, term) / (Math.Pow(1 + monthly, term) - 1));
                record["id"] = await store.NextCode(table, "LOAN"); record["type"] = J.Str(body, "type"); record["amount"] = amount; record["remainingAmount"] = amount; record["interestRate"] = rate; record["term"] = term; record["monthlyPayment"] = Math.Round(payment); record["nextPaymentDate"] = DateTime.UtcNow.AddMonths(1).ToString("yyyy-MM-dd");
                Credit(wallet, amount); await Record(wallet, amount, "loan", "Giải ngân khoản vay", J.Str(body, "type")); field = "loan";
            }
            else
            {
                Required("name", "type", "term"); var premium = Amount(body, "premium", 500_000); var coverage = Amount(body, "coverage"); var term = Term(30);
                Debit(wallet, premium * term);
                await Record(wallet, -premium * term, "insurance", "Mua bảo hiểm", J.Str(body, "name"));
                record["id"] = await store.NextCode(table, "INS"); record["name"] = J.Str(body, "name"); record["type"] = J.Str(body, "type"); record["premium"] = premium; record["coverage"] = coverage; record["endDate"] = DateTime.UtcNow.AddYears(term).ToString("yyyy-MM-dd"); field = "policy";
            }
            await store.Add(table, record); StatusCode = 201; var result = Ok("Tạo thành công"); result[field] = J.Public(record); return result;
        }
        if (Area == "investments" && Action == "sync-price")
        {
            var docs = (await store.All(table)).Where(x => J.Int(x, "user_id") == RequestedId && J.Str(x, "status") == "active" && J.Str(x, "type") == "Tiền điện tử").ToArray();
            foreach (var d in docs)
            {
                Require(J.Money(d, "quantity") > 0, "Khoản đầu tư cũ chưa có số lượng coin để đồng bộ giá");
                UpdateProfit(d, Math.Round(await CryptoPrice(J.Str(d, "name")) * J.Money(d, "quantity")));
            }
            return J.Obj(new { success = true, message = "Đã đồng bộ giá", updatedCount = docs.Length, investments = docs.Select(x => J.Public(x)).ToArray() });
        }
        var item = await Owned(table, "id", J.Str(body, "id")); Require(J.Int(item, "user_id") == RequestedId, "Không có quyền truy cập", 403);
        if (Area == "insurance")
        {
            if (Action == "cancel") { Require(J.Str(item, "status") == "active", "Hợp đồng đã hủy"); item["status"] = "cancelled"; }
            else if (Action == "renew") { var term = Term(30); Require(DateTime.TryParse(J.Str(item, "endDate"), out var end), "Ngày hết hạn không hợp lệ"); Debit(wallet, J.Money(item, "premium") * term); await Record(wallet, -J.Money(item, "premium") * term, "insurance", "Gia hạn bảo hiểm", J.Str(item, "name")); item["endDate"] = (end > DateTime.UtcNow ? end : DateTime.UtcNow).AddYears(term).ToString("yyyy-MM-dd"); item["status"] = "active"; }
            else throw new ApiError(404, "Endpoint không tồn tại");
            return J.Obj(new { success = true, message = "Cập nhật bảo hiểm thành công", policy = J.Public(item) });
        }
        Require(J.Str(item, "status") == "active", "Khoản này không còn hoạt động");
        if (Area == "savings" && Action == "close")
        {
            Require(DateTime.TryParse(J.Str(item, "startDate"), out var start), "Ngày mở sổ không hợp lệ");
            var months = Math.Max(0, (int)Math.Floor((DateTime.UtcNow - start).TotalDays / 30)); var term = J.Int(item, "term"); var early = months < term;
            var principal = J.Money(item, "balance"); var interest = Math.Round(principal * J.Money(item, "interestRate") * (early ? 0.5m : 1m) * Math.Min(months, term) / 1200);
            item["status"] = "closed"; Credit(wallet, principal + interest); await Record(wallet, principal + interest, "savings", "Đóng sổ tiết kiệm", J.Str(item, "name"));
            return J.Obj(new { success = true, message = "Đóng sổ thành công", principal, interest, total = principal + interest, penalty = early ? "Rút trước hạn, lãi suất giảm 50%" : null });
        }
        if (Area == "investments")
        {
            Require(Action is "update" or "sell", "Endpoint không tồn tại", 404);
            var value = Amount(body, Action == "sell" ? "sellPrice" : "currentValue", 0); UpdateProfit(item, value);
            if (Action == "sell") { item["status"] = "sold"; Credit(wallet, value); await Record(wallet, value, "investment", "Bán đầu tư", J.Str(item, "name")); }
            return J.Obj(new { success = true, message = "Cập nhật đầu tư thành công", investment = J.Public(item), sellPrice = value, profit = J.Money(item, "profit"), profitPercent = J.Money(item, "profitPercent") });
        }
        if (Area == "loans")
        {
            Require(Action is "payment" or "payoff", "Endpoint không tồn tại", 404);
            var remaining = J.Money(item, "remainingAmount"); var amount = Action == "payoff" ? remaining : Amount(body);
            Require(amount <= remaining && amount >= Math.Min(J.Money(item, "monthlyPayment"), remaining), "Số tiền trả khoản vay không hợp lệ");
            Debit(wallet, amount); item["remainingAmount"] = remaining - amount;
            if (remaining == amount) item["status"] = "paid";
            Require(DateTime.TryParse(J.Str(item, "nextPaymentDate"), out var next), "Ngày thanh toán không hợp lệ"); item["nextPaymentDate"] = next.AddMonths(1).ToString("yyyy-MM-dd");
            await Record(wallet, -amount, "loan", "Thanh toán khoản vay", J.Str(item, "id"));
            return J.Obj(new { success = true, message = "Thanh toán khoản vay thành công", remainingAmount = remaining - amount, payoffAmount = amount, totalPaid = J.Money(item, "amount") - remaining + amount, nextPaymentDate = J.Str(item, "nextPaymentDate"), status = J.Str(item, "status") });
        }
        throw new ApiError(404, "Endpoint không tồn tại");
    }
    private int Term(int max) { var term = J.Int(body, "term"); Require(term >= 1 && term <= max, "Kỳ hạn không hợp lệ"); return term; }
    private decimal Rate() { var rate = J.Money(body, "interestRate"); Require(rate is >= 0 and <= 100, "Lãi suất không hợp lệ"); return rate; }
    private static void UpdateProfit(JsonObject item, decimal value)
    {
        item["currentValue"] = value; item["profit"] = value - J.Money(item, "amount"); item["profitPercent"] = J.Money(item, "amount") > 0 ? (value - J.Money(item, "amount")) / J.Money(item, "amount") * 100 : 0;
    }
    private async Task<decimal> CryptoPrice(string name)
    {
        var symbols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["bitcoin"] = "BTCUSDT", ["btc"] = "BTCUSDT", ["ethereum"] = "ETHUSDT", ["eth"] = "ETHUSDT", ["bnb"] = "BNBUSDT", ["solana"] = "SOLUSDT", ["sol"] = "SOLUSDT", ["cardano"] = "ADAUSDT", ["ada"] = "ADAUSDT", ["xrp"] = "XRPUSDT", ["dogecoin"] = "DOGEUSDT", ["doge"] = "DOGEUSDT", ["polkadot"] = "DOTUSDT" };
        Require(symbols.TryGetValue(name.Trim(), out var symbol), "Đồng tiền chưa được hỗ trợ");
        try
        {
            using var client = http.CreateClient(); client.Timeout = TimeSpan.FromSeconds(10);
            var json = JsonNode.Parse(await client.GetStringAsync("https://api.binance.com/api/v3/ticker/price?symbol=" + symbol))!.AsObject();
            var usdToVnd = config.GetValue<decimal>("Investment:UsdToVnd", 24_000);
            var price = J.Money(json, "price") * usdToVnd; Require(price > 0, "Nguồn giá không hợp lệ", 503); return price;
        }
        catch (HttpRequestException) { throw new ApiError(503, "Không lấy được giá thị trường; ví chưa bị trừ tiền"); }
        catch (TaskCanceledException) { throw new ApiError(503, "Nguồn giá không phản hồi; ví chưa bị trừ tiền"); }
    }
}
