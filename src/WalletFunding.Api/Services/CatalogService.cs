using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace WalletFunding.Api;

public sealed class CatalogService(IWalletRepository repository, IConfiguration configuration, IDataProtectionProvider protection, IHttpClientFactory http, IHostEnvironment environment) : WalletServiceBase(repository, configuration, protection, http, environment)
{
    public override async Task<ServiceResponse> ExecuteAsync(WalletRequest context)
    {
        await Prepare(context);
        var data = Area == "webhook" ? await Webhook() : await Catalog();
        return new ServiceResponse(data, StatusCode);
    }

    private async Task<JsonObject> Catalog()
    {
        if (Area == "merchants")
        {
            Require(Method == "GET", "Endpoint không tồn tại", 404);
            var table = Action == "billers" ? "billers" : "merchants"; var list = (await store.All(table)).Select(x => J.Public(x)).ToArray();
            var result = J.Obj(new { success = true, data = list }); result[table] = System.Text.Json.JsonSerializer.SerializeToNode(list); return result;
        }
        if (Area == "contacts")
        {
            Require(Method == "GET" && int.TryParse(Action, out _), "Endpoint không tồn tại", 404); var id = int.Parse(Action); Own(id);
            var contacts = (await store.All("contacts")).Where(x => J.Int(x, "ownerId", J.Int(x, "user_id")) == id).Select(x => J.Public(x)).ToArray();
            var simples = (await store.All("simplecontacts")).Where(x => J.Int(x, "user_id") == id).Select(x => J.Public(x)).ToArray();
            return J.Obj(new { success = true, data = contacts.Concat(simples).ToArray(), contacts = contacts.Concat(simples).ToArray() });
        }
        var all = await store.All("notifications");
        if (Method == "GET")
        {
            Require(int.TryParse(Action, out var id), "Mã người dùng không hợp lệ"); Own(id);
            var list = all.Where(x => J.Int(x, "user_id") == id).OrderByDescending(x => J.Str(x, "createdAt")).Select(x => { var d = J.Public(x); d["is_read"] = J.Bool(x, "isSeen"); d["created_at"] = J.Str(x, "createdAt"); return d; }).ToArray();
            var result = Paginate(list, "notifications"); result["unreadCount"] = list.Count(x => !J.Bool(x, "is_read"));
            return J.Obj(new { success = true, notifications = result["notifications"]!.DeepClone(), data = result });
        }
        Require(Method == "PATCH", "Endpoint không tồn tại", 404);
        if (parts.ElementAtOrDefault(2) == "read-all")
        {
            Require(int.TryParse(Action, out var id), "Mã người dùng không hợp lệ"); Own(id);
            foreach (var x in all.Where(x => J.Int(x, "user_id") == id)) x["isSeen"] = true;
        }
        else
        {
            var item = all.FirstOrDefault(x => J.Str(x, "notification_id") == Action || J.Str(x, "_id") == Action) ?? throw new ApiError(404, "Không tìm thấy thông báo");
            Own(J.Int(item, "user_id")); item["isSeen"] = true;
        }
        return Ok("Đã đọc thông báo");
    }

    private async Task<JsonObject> Webhook()
    {
        if (Method == "GET" && Action == "health") return J.Obj(new { success = true, status = "ok", mode = "simulation" });
        Require(Method == "POST" && Action is "payment" or "vnpay", "Endpoint không tồn tại", 404);
        var key = config["Payment:WebhookSecret"]; Require(!string.IsNullOrEmpty(key), "Chưa cấu hình chữ ký webhook", 503);
        var signature = request.Headers["X-Payment-Signature"].ToString();
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key!), Encoding.UTF8.GetBytes(body.ToJsonString()));
        byte[] actual; try { actual = Convert.FromHexString(signature); } catch (FormatException) { throw new ApiError(401, "Chữ ký webhook không hợp lệ"); }
        Require(CryptographicOperations.FixedTimeEquals(expected, actual), "Chữ ký webhook không hợp lệ", 401);
        Required("gateway_transaction_id"); var gateway = await Find("gatewaytransactions", "gateway_tx_id", J.Str(body, "gateway_transaction_id"));
        Require(J.Money(gateway, "amount") == J.Money(body, "amount"), "Số tiền callback không khớp", 400);
        // Simulated top-ups settle inside their own transaction. Replayed callbacks cannot credit twice.
        Require(J.Str(gateway, "status") == "success", "Giao dịch chưa được quyết toán bởi hệ thống", 409);
        gateway["callback_received_at"] = Now; return J.Obj(new { success = true, message = "Đã ghi nhận callback; không cộng tiền lần hai" });
    }
}
