using System.Text.Json.Nodes;

namespace WalletFunding.Api;

public sealed class BankingService(IWalletRepository repository, IConfiguration configuration, IDataProtectionProvider protection, IHttpClientFactory http, IHostEnvironment environment) : WalletServiceBase(repository, configuration, protection, http, environment)
{
    public override async Task<ServiceResponse> ExecuteAsync(WalletRequest context)
    {
        return await ExecuteWithReceipt(context, Banking);
    }

    private async Task<JsonObject> Banking()
    {
        if (Area == "banks")
        {
            var banks = await store.All("supportedbanks");
            if (Method == "GET")
            {
                if (parts.Length > 2) Require(Admin, "Yêu cầu quyền quản trị", 403);
                var list = banks.Where(x => parts.Length > 2 || J.Bool(x, "is_active")).OrderBy(x => J.Int(x, "display_order")).Select(x => J.Public(x)).ToArray();
                return J.Obj(new { success = true, data = list, total = list.Length });
            }
            Require(Admin, "Yêu cầu quyền quản trị", 403);
            if (Method == "POST")
            {
                Required("bank_code", "bank_name"); Require(!banks.Any(x => J.Str(x, "bank_code") == J.Str(body, "bank_code")), "Ngân hàng đã tồn tại", 409);
                var d = body.DeepClone().AsObject(); d["is_active"] ??= JsonValue.Create(true); await store.Add("supportedbanks", d); return J.Obj(new { success = true, data = J.Public(d) });
            }
            var bank = await Find("supportedbanks", "bank_code", parts.ElementAtOrDefault(2) ?? "");
            // Braces matter here: without them the trailing else-branches bound to the inner `if`
            // inside the foreach, so DELETE was a silent no-op and a partial PUT returned 404.
            if (Method == "PATCH")
            {
                bank["is_active"] = !J.Bool(bank, "is_active");
            }
            else if (Method == "PUT")
            {
                foreach (var field in new[] { "bank_name", "bank_full_name", "short_name", "logo_url", "is_active", "display_order", "gateway_support" })
                {
                    if (body.ContainsKey(field)) bank[field] = body[field]?.DeepClone();
                }
            }
            else if (Method == "DELETE")
            {
                Require(!(await store.All("linkedbanks")).Any(x => J.Str(x, "bank_code") == J.Str(bank, "bank_code") && J.Str(x, "status") == "active"), "Ngân hàng đang được liên kết");
                banks.Remove(bank);
            }
            else
            {
                throw new ApiError(404, "Endpoint không tồn tại");
            }
            return J.Obj(new { success = true, message = "Cập nhật ngân hàng thành công", data = J.Public(bank) });
        }
        if (Area == "bank-accounts")
        {
            var accounts = await store.All("bankaccounts");
            if (Action == "verify" && Method == "POST")
            {
                Required("bank_code", "account_number");
                var account = accounts.FirstOrDefault(x => J.Str(x, "bank_code") == J.Str(body, "bank_code") && J.Str(x, "account_number") == J.Str(body, "account_number") && J.Str(x, "status") == "active");
                Require(account != null, "Không tìm thấy tài khoản ngân hàng", 404);
                // Only what the transfer form needs to confirm the payee; no balance, contact data or internal ids.
                return J.Obj(new { success = true, message = "Tài khoản hợp lệ", data = new { bank_code = J.Str(account!, "bank_code"), bank_name = J.Str(account!, "bank_name"), account_number = J.Str(account!, "account_number"), account_name = J.Str(account!, "account_name"), status = J.Str(account!, "status") } });
            }
            Require(Method == "GET", "Endpoint không tồn tại", 404);
            if (Action == "stats") { Require(Admin, "Yêu cầu quyền quản trị", 403); return J.Obj(new { success = true, total = accounts.Count, stats = accounts.GroupBy(x => J.Str(x, "bank_code")).Select(g => new { bank_code = g.Key, total = g.Count() }).ToArray() }); }
            if (Action == "search")
            {
                // Substring search over names/numbers would let any user enumerate other people's accounts.
                Require(Admin, "Yêu cầu quyền quản trị", 403);
                var q = request.Query["q"].ToString(); if (q == "") q = request.Query["query"].ToString(); Require(q.Length >= 2, "Nhập ít nhất 2 ký tự tìm kiếm");
                var list = accounts.Where(x => J.Str(x, "status") == "active" && (J.Str(x, "account_name").Contains(q, StringComparison.OrdinalIgnoreCase) || J.Str(x, "account_number").Contains(q))).Take(20).Select(x => J.Public(x, "email", "phone", "balance")).ToArray();
                return J.Obj(new { success = true, data = list, accounts = list });
            }
            Require(Admin, "Yêu cầu quyền quản trị", 403);
            return J.Obj(new { success = true, accounts = accounts.Select(x => J.Public(x)).ToArray(), total = accounts.Count });
        }
        if (Area == "cards") return await Cards();
        if (Method == "GET" && Action == "linked")
        {
            Require(int.TryParse(parts.ElementAtOrDefault(2), out var id), "Mã người dùng không hợp lệ"); Own(id);
            var banks = (await store.All("linkedbanks")).Where(x => J.Int(x, "user_id") == id && J.Str(x, "status") == "active").Select(x => J.Public(x)).ToArray();
            return J.Obj(new { success = true, data = new { banks, total = banks.Length } });
        }
        if (Method == "POST" && Action == "link")
        {
            Required("bank_name", "account_number", "account_holder");
            var supported = (await store.All("supportedbanks")).Where(x => J.Str(x, "bank_name") == J.Str(body, "bank_name") && J.Bool(x, "is_active")).ToArray();
            Require(supported.Length > 0, "Ngân hàng không được hỗ trợ");
            var account = (await store.All("bankaccounts")).FirstOrDefault(x => supported.Any(bank => J.Str(x, "bank_code") == J.Str(bank, "bank_code")) && J.Str(x, "account_number") == J.Str(body, "account_number") && J.Str(x, "status") == "active");
            Require(account != null, "Không tìm thấy tài khoản ngân hàng", 404);
            Require(NormalizeName(J.Str(account!, "account_name")) == NormalizeName(J.Str(body, "account_holder")), "Tên chủ tài khoản không khớp");
            var linked = await store.All("linkedbanks");
            Require(!linked.Any(x => J.Int(x, "user_id") == RequestedId && J.Int(x, "account_id") == J.Int(account!, "account_id") && J.Str(x, "status") == "active"), "Tài khoản đã được liên kết", 409);
            var number = J.Str(account!, "account_number");
            var bank = J.Obj(new { bank_id = await store.Next("linkedbanks", "bank_id"), user_id = RequestedId, account_id = J.Int(account!, "account_id"), bank_code = J.Str(account!, "bank_code"), bank_name = J.Str(body, "bank_name"), account_last4 = number[^Math.Min(4, number.Length)..], account_holder = J.Str(account!, "account_name"), status = "active", is_default = !linked.Any(x => J.Int(x, "user_id") == RequestedId && J.Str(x, "status") == "active"), linked_at = Now });
            await store.Add("linkedbanks", bank); return J.Obj(new { success = true, message = "Liên kết ngân hàng thành công", data = J.Public(bank) });
        }
        if (Method == "POST" && Action == "topup")
        {
            var user = await User(RequestedId); await Pin(user, J.Str(body, "pin_code", J.Str(body, "pinCode", J.Str(body, "pin"))));
            var bank = await Owned("linkedbanks", "bank_id", J.Str(body, "bank_id", J.Str(body, "linked_bank_id"))); Require(J.Str(bank, "status") == "active", "Liên kết không hoạt động");
            var amount = Amount(body, minimum: 50_000); Require(amount <= 50_000_000, "Tối đa 50.000.000 VND mỗi lần nạp");
            var wallet = await Wallet(RequestedId); Require(await SentToday(wallet, "deposit") + amount <= 200_000_000, "Vượt hạn mức nạp tiền trong ngày", 403);
            var accounts = await store.All("bankaccounts"); var account = accounts.FirstOrDefault(x => J.Int(x, "account_id") == J.Int(bank, "account_id"));
            if (account == null)
            {
                var candidates = accounts.Where(x => J.Str(x, "bank_name") == J.Str(bank, "bank_name") && J.Str(x, "account_number").EndsWith(J.Str(bank, "account_last4"))).ToArray();
                Require(candidates.Length == 1, "Vui lòng liên kết lại tài khoản ngân hàng cũ"); account = candidates[0]; bank["account_id"] = J.Int(account, "account_id");
            }
            Require(J.Str(account, "status") == "active", "Tài khoản ngân hàng không hoạt động", 403);
            Require(J.Money(account, "balance") >= amount, "Số dư ngân hàng không đủ"); account["balance"] = J.Money(account, "balance") - amount; Credit(wallet, amount);
            var reference = "TOPUP_" + Guid.NewGuid().ToString("N"); var tx = await Record(wallet, amount, "deposit", "Nạp tiền", "Nạp từ " + J.Str(bank, "bank_name"), reference);
            await store.Add("gatewaytransactions", J.Obj(new { gateway_tx_id = reference, user_id = RequestedId, type = "topup", status = "success", amount, created_at = Now, mode = "simulation" }));
            return J.Obj(new { success = true, message = "Nạp tiền thành công (ngân hàng mô phỏng)", data = new { transaction_id = J.Int(tx, "trans_id"), gateway_transaction_id = reference, amount, bank_name = J.Str(bank, "bank_name"), bank_new_balance = J.Money(account, "balance"), wallet_new_balance = J.Money(wallet, "balance") } });
        }
        if (Method == "DELETE" && Action == "unlink")
        {
            var bank = await Owned("linkedbanks", "bank_id", parts.ElementAtOrDefault(2) ?? ""); bank["status"] = "unlinked"; bank["is_default"] = false; return Ok("Đã hủy liên kết");
        }
        if (Method == "PUT" && Action == "set-default")
        {
            var bank = await Owned("linkedbanks", "bank_id", J.Str(body, "bank_id")); Require(J.Str(bank, "status") == "active", "Liên kết không hoạt động");
            foreach (var b in (await store.All("linkedbanks")).Where(x => J.Int(x, "user_id") == J.Int(bank, "user_id"))) b["is_default"] = ReferenceEquals(b, bank);
            return J.Obj(new { success = true, data = J.Public(bank) });
        }
        throw new ApiError(404, "Endpoint không tồn tại");
    }

    private async Task<JsonObject> Cards()
    {
        var cards = await store.All("cards");
        if (Method == "GET")
        {
            Require(int.TryParse(Action, out var id), "Mã người dùng không hợp lệ"); Own(id);
            var list = cards.Where(x => J.Int(x, "user_id") == id).Select(CardView).ToArray();
            return J.Obj(new { success = true, cards = list });
        }
        if (Method == "POST" && Action == "add")
        {
            Required("card_number", "holder_name", "expiry_month", "expiry_year", "brand", "type", "issuer");
            var number = J.Str(body, "card_number").Replace(" ", "");
            Require(System.Text.RegularExpressions.Regex.IsMatch(number, "^[0-9]{13,19}$") && Luhn(number), "Số thẻ không hợp lệ");
            var month = J.Int(body, "expiry_month"); var year = J.Int(body, "expiry_year"); if (year < 100) year += 2000;
            Require(month is >= 1 and <= 12 && year >= DateTime.UtcNow.Year && year < 2100 && new DateTime(year, month, 1).AddMonths(1) > DateTime.UtcNow, "Thẻ đã hết hạn hoặc ngày không hợp lệ");
            Require(!cards.Any(x => J.Int(x, "user_id") == RequestedId && J.Str(x, "last4_digits") == number[^4..] && J.Str(x, "issuer") == J.Str(body, "issuer")), "Thẻ đã được đăng ký", 409);
            var card = J.Obj(new { card_id = await store.Next("cards", "card_id"), user_id = RequestedId, card_number_encrypted = Encrypt(number), last4_digits = number[^4..], holder_name = J.Str(body, "holder_name").ToUpperInvariant(), expiry_month = month, expiry_year = year, brand = J.Str(body, "brand").ToUpperInvariant(), type = J.Str(body, "type"), issuer = J.Str(body, "issuer"), status = "active", is_default = false, created_at = Now });
            await store.Add("cards", card); StatusCode = 201; return J.Obj(new { success = true, message = "Thêm thẻ thành công", card = CardView(card) });
        }
        var selected = await Owned("cards", "card_id", Action);
        if (Method == "PATCH") { var status = J.Str(body, "status"); Require(status is "active" or "blocked" or "inactive", "Trạng thái không hợp lệ"); selected["status"] = status; }
        else if (Method == "DELETE") cards.Remove(selected);
        else throw new ApiError(404, "Endpoint không tồn tại");
        return J.Obj(new { success = true, card = J.Public(selected) });
    }
    private static JsonObject CardView(JsonObject card)
    {
        var result = J.Public(card);
        result["expiry_date"] = J.Str(card, "expiry_date", J.Str(card, "expiry_month").PadLeft(2, '0') + "/" + J.Str(card, "expiry_year"));
        result["card_number"] = "•••• •••• •••• " + J.Str(card, "last4_digits");
        result["card_holder_name"] = J.Str(card, "holder_name");
        result["bank_name"] = J.Str(card, "issuer");
        result["card_type"] = J.Str(card, "brand", "visa").ToLowerInvariant();
        return result;
    }
    private static bool Luhn(string number)
    {
        var sum = 0; var twice = false;
        for (var i = number.Length - 1; i >= 0; i--) { var d = number[i] - '0'; if (twice) { d *= 2; if (d > 9) d -= 9; } sum += d; twice = !twice; }
        return sum % 10 == 0;
    }
}
