using System.Text.Json.Nodes;

namespace WalletFunding.Api;

public sealed class TransactionService(IWalletRepository repository, IConfiguration configuration, IDataProtectionProvider protection, IHttpClientFactory http, IHostEnvironment environment) : WalletServiceBase(repository, configuration, protection, http, environment)
{
    protected override bool VerifiesPin => true;

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
            IEnumerable<JsonObject> all = await store.Where("transactions", "wallet_id", J.Str(wallet, "wallet_id"));
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
        if (Action is "transfer" or "execute")
        {
            var input = Bind<TransferRequest>();
            await Pin(userFrom, input.PinValue);
            var amount = input.Amount!.Value;
            var phone = input.Phone;
            JsonObject userTo;
            if (!string.IsNullOrEmpty(phone)) userTo = await Find("users", "phone", phone);
            else userTo = await User(input.RecipientId!.Value);
            Require(J.Int(userTo, "user_id") != RequestedId, "Không thể chuyển tiền cho chính mình");
            Require(!Blocked(userTo), "Tài khoản người nhận đã bị khóa", 403);
            var walletTo = await Wallet(J.Int(userTo, "user_id")); var today = await SentToday(walletFrom, "transfer"); var limit = DailyLimit(userFrom);
            await RequireTransferLimit(userFrom, walletFrom, amount);
            var description = input.Note; var reference = Guid.NewGuid().ToString("N");
            Debit(walletFrom, amount); Credit(walletTo, amount);
            var tx = await Record(walletFrom, -amount, "transfer", "Chuyển tiền", description, reference); tx["recipient"] = J.Str(userTo, "name");
            var received = await Record(walletTo, amount, "transfer", "Nhận tiền", description, reference); received["recipient"] = J.Str(userFrom, "name");
            await Notify(J.Int(userTo, "user_id"), "Nhận tiền", $"Bạn đã nhận {amount:N0} VND");
            return J.Obj(new { success = true, message = "Chuyển tiền thành công", transaction = new { trans_id = J.Int(tx, "trans_id"), amount, recipient = J.Str(userTo, "name"), new_balance = J.Money(walletFrom, "balance") }, data = new { transactionId = "TRF" + J.Str(tx, "trans_id"), sender = new { userId = RequestedId, name = J.Str(userFrom, "name"), newBalance = J.Money(walletFrom, "balance") }, receiver = new { userId = J.Int(userTo, "user_id"), name = J.Str(userTo, "name"), phone, newBalance = J.Money(walletTo, "balance") }, amount, description, timestamp = Now, kycInfo = new { tier = J.Str(userFrom, "kycTier", "TIER_1"), dailyLimit = limit, todaySent = today + amount, remaining = limit - today - amount } } });
        }
        await Pin(userFrom, J.Str(body, "pin", J.Str(body, "pinCode")));
        var paid = Amount(body, minimum: 10_000);
        if (Action == "bank-transfer")
        {
            Required("bank_code", "account_number");
            // Same daily outgoing limit as internal transfers (both are recorded as type "transfer").
            await RequireTransferLimit(userFrom, walletFrom, paid);
            var account = (await store.All("bankaccounts")).FirstOrDefault(x => J.Str(x, "bank_code") == J.Str(body, "bank_code") && J.Str(x, "account_number") == J.Str(body, "account_number"));
            Require(account != null && J.Str(account, "status") == "active", "Không tìm thấy tài khoản ngân hàng", 404);
            if (J.Str(body, "account_name") != "") Require(NormalizeName(J.Str(body, "account_name")) == NormalizeName(J.Str(account!, "account_name")), "Tên chủ tài khoản không khớp");
            Debit(walletFrom, paid); account!["balance"] = J.Money(account, "balance") + paid;
            var tx = await Record(walletFrom, -paid, "transfer", "Chuyển đến ngân hàng", J.Str(body, "message", "Chuyển tiền ngân hàng"));
            return J.Obj(new { success = true, message = "Chuyển tiền thành công (ngân hàng mô phỏng)", transaction = new { trans_id = J.Int(tx, "trans_id"), amount = paid, recipient = J.Str(account, "account_name"), new_balance = J.Money(walletFrom, "balance") } });
        }
        Require(Action is "phone-topup" or "bill-payment", "Endpoint không tồn tại", 404);
        if (Action == "phone-topup") { Required("phone_number", "provider"); Require(new decimal[] { 10_000, 20_000, 50_000, 100_000, 200_000, 500_000 }.Contains(paid), "Mệnh giá không hợp lệ"); }
        else
        {
            Required("bill_type", "bill_code");
            var period = J.Str(body, "billing_period", DateTime.UtcNow.ToString("yyyy-MM"));
            Require(!(await store.All("payments")).Any(x => J.Int(x, "user_id") == RequestedId && J.Str(x, "bill_type") == J.Str(body, "bill_type") && J.Str(x, "bill_code") == J.Str(body, "bill_code") && J.Str(x, "billing_period") == period &&
                (J.Str(x, "status") == "success" || J.Str(x, "status") == "pending" && DateTimeOffset.TryParse(J.Str(x, "expires_at"), out var expiry) && expiry > DateTimeOffset.UtcNow)), "Hóa đơn đã thanh toán hoặc đang chờ VNPay xử lý", 409);
            await store.Add("payments", J.Obj(new { kind = "bill_payment", user_id = RequestedId, bill_type = J.Str(body, "bill_type"), bill_code = J.Str(body, "bill_code"), billing_period = period, amount = paid, status = "success", funding_source = "wallet", created_at = Now }));
        }
        Debit(walletFrom, paid);
        var record = await Record(walletFrom, -paid, Action == "phone-topup" ? "payment" : "bill_payment", Action == "phone-topup" ? "Nạp điện thoại" : "Thanh toán hóa đơn", Action == "phone-topup" ? J.Str(body, "phone_number") : J.Str(body, "bill_code"));
        return J.Obj(new { success = true, message = "Thanh toán thành công (mô phỏng)", transaction = new { trans_id = J.Int(record, "trans_id"), amount = paid, new_balance = J.Money(walletFrom, "balance") } });
    }
}
