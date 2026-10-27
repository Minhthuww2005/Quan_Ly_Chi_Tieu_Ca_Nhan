using System.Text.Json.Nodes;

namespace WalletFunding.Api;

public sealed class PaymentService(IWalletRepository repository, IConfiguration configuration, IDataProtectionProvider protection,
    IHttpClientFactory http, IHostEnvironment environment, IVnpayGateway gateway) : WalletServiceBase(repository, configuration, protection, http, environment)
{
    protected override bool VerifiesPin => true;

    public override async Task<ServiceResponse> ExecuteAsync(WalletRequest context)
    {
        return await ExecuteWithReceipt(context, Process);
    }

    private async Task<JsonObject> Process()
    {
        if (Action == "vnpay" && parts.ElementAtOrDefault(2) is "ipn" or "verify" or "confirm-local") return await Callback(parts[2] == "ipn", parts[2] == "confirm-local");
        var payments = await store.All("payments");
        if (Method == "GET" && Action == "orders")
        {
            var list = payments.Where(x => J.Str(x, "kind") == "vnpay_order" && J.Int(x, "user_id") == UserId)
                .OrderByDescending(x => J.Str(x, "created_at")).Select(OrderView).ToArray();
            return J.Obj(new { success = true, orders = list });
        }
        if (Method == "GET" && Action == "status")
        {
            var order = await Owned("payments", "order_id", parts.ElementAtOrDefault(2) ?? "");
            return J.Obj(new { success = true, order = OrderView(order) });
        }
        Require(Method == "POST" && Action == "vnpay" && parts.ElementAtOrDefault(2) == "create", "Endpoint không tồn tại", 404);
        Require(gateway.IsConfigured, "Chưa cấu hình VNPay sandbox: TmnCode và HashSecret", 503);
        var input = Bind<VnpayCreateRequest>();
        await Pin(current!, input.PinValue);
        var amount = input.Amount!.Value; var type = input.Type;
        var billType = input.BillType ?? ""; var billCode = input.BillCode ?? "";
        var period = input.BillingPeriod ?? DateTime.UtcNow.ToString("yyyy-MM");
        if (type == "bill_payment")
        {
            Require(!payments.Any(x => J.Int(x, "user_id") == UserId && J.Str(x, "bill_type") == billType && J.Str(x, "bill_code") == billCode && J.Str(x, "billing_period") == period &&
                (J.Str(x, "status") == "success" || J.Str(x, "status") == "pending" && DateTimeOffset.TryParse(J.Str(x, "expires_at"), out var end) && end > DateTimeOffset.UtcNow)), "Hóa đơn đã thanh toán hoặc đang chờ VNPay xử lý", 409);
        }
        var reference = "HTD" + Guid.NewGuid().ToString("N");
        var url = gateway.CreateUrl(reference, amount, contextIp());
        var orderNew = J.Obj(new { kind = "vnpay_order", order_id = reference, user_id = UserId, amount, type, bill_type = billType, bill_code = billCode,
            billing_period = period, status = "pending", created_at = Now, expires_at = DateTimeOffset.UtcNow.AddMinutes(15).ToString("O"), checkout_url = url });
        await store.Add("payments", orderNew); StatusCode = 201;
        return J.Obj(new { success = true, order = OrderView(orderNew), paymentUrl = url });
    }

    private bool LocalSettlementEnabled => environment.IsDevelopment() && config.GetValue<bool>("Vnpay:AllowLocalReturnSettlement")
        && Uri.TryCreate(config["Vnpay:ReturnUrl"], UriKind.Absolute, out var uri) && uri.IsLoopback;

    private string contextIp() => request.Headers["X-HTD-Client-IP"] is { Length: > 0 } ip ? ip : "127.0.0.1";

    private async Task<JsonObject> Callback(bool ipn, bool local = false)
    {
        Require(Method == (local ? "POST" : "GET"), "Endpoint không tồn tại", 404);
        if (local) Require(LocalSettlementEnabled, "Xác nhận local chưa được bật", 404);
        var fields = request.Query.Values;
        if (!gateway.IsConfigured) return Reply("99", "VNPay chưa được cấu hình");
        if (!gateway.Verify(fields)) return Reply("97", "Chữ ký không hợp lệ");
        if (request.Query["vnp_TmnCode"] != gateway.MerchantCode) return Reply("97", "Merchant không khớp");
        var order = (await store.All("payments")).FirstOrDefault(x => J.Str(x, "kind") == "vnpay_order" && J.Str(x, "order_id") == request.Query["vnp_TxnRef"]);
        if (order == null) return Reply("01", "Không tìm thấy đơn hàng");
        if (!decimal.TryParse(request.Query["vnp_Amount"], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var units) || units != J.Money(order, "amount") * 100) return Reply("04", "Số tiền không khớp");
        if (local) Require(current != null && J.Int(order, "user_id") == UserId, "Không có quyền xác nhận đơn", 403);
        if (!ipn && !local) return J.Obj(new { success = true, signatureValid = true, responseCode = request.Query["vnp_ResponseCode"], orderId = J.Str(order, "order_id"), status = J.Str(order, "status"), message = "Kết quả được xác minh; trạng thái chỉ cập nhật qua IPN" });
        if (J.Str(order, "status") != "pending") return Reply("02", "Đơn hàng đã xử lý");
        var successful = request.Query["vnp_ResponseCode"] == "00" && request.Query["vnp_TransactionStatus"] == "00";
        if (Expired(order))
        {
            // The order window (vnp_ExpireDate) has closed: never credit the wallet late. A paid-but-expired
            // order is kept for manual reconciliation/refund instead of silently becoming a top-up.
            order["status"] = "expired"; order["settled_at"] = Now;
            order["gateway_transaction_no"] = request.Query["vnp_TransactionNo"]; order["response_code"] = request.Query["vnp_ResponseCode"];
            if (successful) order["reconciliation_required"] = true;
            return Reply("02", "Đơn hàng đã hết hạn");
        }
        order["status"] = successful ? "success" : "failed";
        order["gateway_transaction_no"] = request.Query["vnp_TransactionNo"];
        order["response_code"] = request.Query["vnp_ResponseCode"]; order["settled_at"] = Now;
        if (successful)
        {
            var wallet = await Wallet(J.Int(order, "user_id"));
            if (J.Str(order, "type") == "wallet_topup")
            {
                Credit(wallet, J.Money(order, "amount"));
                var tx = await Record(wallet, J.Money(order, "amount"), "deposit", "Nạp ví qua VNPay sandbox", "VNPay sandbox", J.Str(order, "order_id"));
                order["trans_id"] = J.Int(tx, "trans_id");
            }
            else
            {
                // Paid from the external sandbox bank, not deducted again from the wallet.
                var tx = await Record(wallet, 0, "bill_payment", "Thanh toán hóa đơn qua VNPay sandbox", J.Str(order, "bill_code"), J.Str(order, "order_id"));
                tx["payment_amount"] = J.Money(order, "amount"); tx["funding_source"] = "vnpay_sandbox"; order["trans_id"] = J.Int(tx, "trans_id");
            }
            await Notify(J.Int(order, "user_id"), "VNPay sandbox", "Giao dịch " + J.Str(order, "order_id") + " đã hoàn tất");
        }
        return Reply("00", "Confirm Success");
    }

    private static JsonObject Reply(string code, string message) => J.Obj(new { RspCode = code, Message = message });
    private static JsonObject OrderView(JsonObject order)
    {
        var result = J.Public(order);
        if (J.Str(order, "status") == "pending" && Expired(order)) result["status"] = "expired";
        return result;
    }
    private static bool Expired(JsonObject order) =>
        DateTimeOffset.TryParse(J.Str(order, "expires_at"), null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiry) && expiry < DateTimeOffset.UtcNow;
}
