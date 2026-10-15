using System.Text.Json.Nodes;

namespace WalletFunding.Api;

public sealed class TransactionService(IWalletRepository repository, IConfiguration configuration, IDataProtectionProvider protection, IHttpClientFactory http, IHostEnvironment environment) : WalletServiceBase(repository, configuration, protection, http, environment)
{
    public override async Task<ServiceResponse> ExecuteAsync(WalletRequest context)
    {
        return await ExecuteWithReceipt(context, Transactions);
    }

    private async Task<JsonObject> Transactions()
    {
        if (Method == "GET")
        {
            var idPart = Area == "transfer" ? parts.ElementAtOrDefault(2) : Action;
            Require(int.TryParse(idPart, out var id), "Mã người dùng không hợp lệ"); Own(id); var wallet = await Wallet(id);
            if (Area == "transfer" && Action == "daily-summary")
            {
                var user = await User(id); var today = await SentToday(wallet, "transfer"); var limit = DailyLimit(user);
                return J.Obj(new { success = true, data = new { userId = id, kycTier = J.Str(user, "kycTier", "TIER_1"), dailyLimit = limit, todaySent = today, remaining = Math.Max(0, limit - today), percentUsed = Math.Min(100, today / limit * 100) } });
            }
            var all = (await store.All("transactions")).Where(x => J.Int(x, "wallet_id") == J.Int(wallet, "wallet_id"));
            var type = request.Query["type"].ToString(); var status = request.Query["status"].ToString();
            if (type != "") all = all.Where(x => J.Str(x, "type") == type);
            if (status != "") all = all.Where(x => J.Str(x, "status") == status);
            var limitCount = Math.Clamp(int.TryParse(request.Query["limit"], out var l) ? l : 50, 1, 200);
            var offset = Math.Max(0, int.TryParse(request.Query["offset"], out var o) ? o : int.TryParse(request.Query["skip"], out o) ? o : 0);
            var ordered = all.OrderByDescending(x => J.Str(x, "timestamp")).ToArray(); var list = ordered.Skip(offset).Take(limitCount).Select(x => J.Public(x)).ToArray();
            return J.Obj(new { success = true, transactions = list, data = list, total = ordered.Length, hasMore = ordered.Length > offset + limitCount });
        }
        Require(Method == "POST", "Endpoint không tồn tại", 404);
        var userFrom = await User(RequestedId); var walletFrom = await Wallet(RequestedId);
        await Pin(userFrom, J.Str(body, "pin", J.Str(body, "pinCode")));
        var amount = Amount(body, minimum: Action == "phone-topup" ? 10_000 : 10_000);
        if (Action is "transfer" or "execute")
        {
            var phone = J.Str(body, "to_phone", J.Str(body, "receiverPhone"));
            JsonObject userTo;
            if (!string.IsNullOrEmpty(phone)) userTo = await Find("users", "phone", phone);
            else userTo = await User(J.Int(body, "recipient_id"));
            Require(J.Int(userTo, "user_id") != RequestedId, "Không thể chuyển tiền cho chính mình");
            Require(!Blocked(userTo), "Tài khoản người nhận đã bị khóa", 403);
            var walletTo = await Wallet(J.Int(userTo, "user_id")); var today = await SentToday(walletFrom, "transfer"); var limit = DailyLimit(userFrom);
            Require(today + amount <= limit, "Vượt hạn mức chuyển tiền trong ngày", 403, "DAILY_LIMIT_EXCEEDED");
            var description = J.Str(body, "message", J.Str(body, "description", "Chuyển tiền")); var reference = Guid.NewGuid().ToString("N");
            Debit(walletFrom, amount); Credit(walletTo, amount);
            var tx = await Record(walletFrom, -amount, "transfer", "Chuyển tiền", description, reference); tx["recipient"] = J.Str(userTo, "name");
            var received = await Record(walletTo, amount, "transfer", "Nhận tiền", description, reference); received["recipient"] = J.Str(userFrom, "name");
            await Notify(J.Int(userTo, "user_id"), "Nhận tiền", $"Bạn đã nhận {amount:N0} VND");
            return J.Obj(new { success = true, message = "Chuyển tiền thành công", transaction = new { trans_id = J.Int(tx, "trans_id"), amount, recipient = J.Str(userTo, "name"), new_balance = J.Money(walletFrom, "balance") }, data = new { transactionId = "TRF" + J.Str(tx, "trans_id"), sender = new { userId = RequestedId, name = J.Str(userFrom, "name"), newBalance = J.Money(walletFrom, "balance") }, receiver = new { userId = J.Int(userTo, "user_id"), name = J.Str(userTo, "name"), phone, newBalance = J.Money(walletTo, "balance") }, amount, description, timestamp = Now, kycInfo = new { tier = J.Str(userFrom, "kycTier", "TIER_1"), dailyLimit = limit, todaySent = today + amount, remaining = limit - today - amount } } });
        }
        if (Action == "bank-transfer")
        {
            Required("bank_code", "account_number");
            var account = (await store.All("bankaccounts")).FirstOrDefault(x => J.Str(x, "bank_code") == J.Str(body, "bank_code") && J.Str(x, "account_number") == J.Str(body, "account_number"));
            Require(account != null && J.Str(account, "status") == "active", "Không tìm thấy tài khoản ngân hàng", 404);
            if (J.Str(body, "account_name") != "") Require(NormalizeName(J.Str(body, "account_name")) == NormalizeName(J.Str(account!, "account_name")), "Tên chủ tài khoản không khớp");
            Debit(walletFrom, amount); account!["balance"] = J.Money(account, "balance") + amount;
            var tx = await Record(walletFrom, -amount, "transfer", "Chuyển đến ngân hàng", J.Str(body, "message", "Chuyển tiền ngân hàng"));
            return J.Obj(new { success = true, message = "Chuyển tiền thành công (ngân hàng mô phỏng)", transaction = new { trans_id = J.Int(tx, "trans_id"), amount, recipient = J.Str(account, "account_name"), new_balance = J.Money(walletFrom, "balance") } });
        }
        Require(Action is "phone-topup" or "bill-payment", "Endpoint không tồn tại", 404);
        if (Action == "phone-topup") { Required("phone_number", "provider"); Require(new decimal[] { 10_000, 20_000, 50_000, 100_000, 200_000, 500_000 }.Contains(amount), "Mệnh giá không hợp lệ"); }
        else
        {
            Required("bill_type", "bill_code");
            var period = J.Str(body, "billing_period", DateTime.UtcNow.ToString("yyyy-MM"));
            Require(!(await store.All("payments")).Any(x => J.Int(x, "user_id") == RequestedId && J.Str(x, "bill_type") == J.Str(body, "bill_type") && J.Str(x, "bill_code") == J.Str(body, "bill_code") && J.Str(x, "billing_period") == period &&
                (J.Str(x, "status") == "success" || J.Str(x, "status") == "pending" && DateTimeOffset.TryParse(J.Str(x, "expires_at"), out var expiry) && expiry > DateTimeOffset.UtcNow)), "Hóa đơn đã thanh toán hoặc đang chờ VNPay xử lý", 409);
            await store.Add("payments", J.Obj(new { kind = "bill_payment", user_id = RequestedId, bill_type = J.Str(body, "bill_type"), bill_code = J.Str(body, "bill_code"), billing_period = period, amount, status = "success", funding_source = "wallet", created_at = Now }));
        }
        Debit(walletFrom, amount);
        var record = await Record(walletFrom, -amount, Action == "phone-topup" ? "payment" : "bill_payment", Action == "phone-topup" ? "Nạp điện thoại" : "Thanh toán hóa đơn", Action == "phone-topup" ? J.Str(body, "phone_number") : J.Str(body, "bill_code"));
        return J.Obj(new { success = true, message = "Thanh toán thành công (mô phỏng)", transaction = new { trans_id = J.Int(record, "trans_id"), amount, new_balance = J.Money(walletFrom, "balance") } });
    }
}
