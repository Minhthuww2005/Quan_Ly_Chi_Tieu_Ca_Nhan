using System.Globalization;
using System.Text.Json.Nodes;

namespace WalletFunding.Api;

// A shared ledger: recording a cost never silently debits a personal wallet.
public sealed class GroupExpenseService(IWalletRepository repository, IConfiguration configuration,
    IDataProtectionProvider protection, IHttpClientFactory http, IHostEnvironment environment)
    : WalletServiceBase(repository, configuration, protection, http, environment)
{
    public override Task<ServiceResponse> ExecuteAsync(WalletRequest context) => ExecuteWithReceipt(context, Handle);

    private async Task<JsonObject> Handle()
    {
        var adminView = Area == "admin";
        if (adminView) Require(Admin, "Yêu cầu quyền quản trị", 403);
        var offset = adminView ? 2 : 1;
        var idText = parts.ElementAtOrDefault(offset) ?? "";
        var groups = await store.All("expensegroups");
        var members = await store.All("expensemembers");
        if (idText == "")
        {
            if (Method == "GET")
            {
                var visible = groups.Where(g => adminView || members.Any(m => J.Int(m, "group_id") == J.Int(g, "group_id") && J.Int(m, "user_id") == UserId && J.Bool(m, "active")));
                return J.Obj(new { success = true, groups = visible.Select(g => new { id = J.Int(g, "group_id"), name = J.Str(g, "name"), budget = J.Money(g, "budget"), archived = J.Bool(g, "archived"), ownerId = J.Int(g, "owner_id"), version = J.Int(g, "version") }).ToArray() });
            }
            Require(!adminView && Method == "POST", "Endpoint không tồn tại", 404);
            var name = Text("name", 100);
            var budget = Budget();
            var id = await store.Next("expensegroups", "group_id");
            await store.Add("expensegroups", J.Obj(new { group_id = id, owner_id = UserId, name, budget, archived = false, version = 1, created_at = Now }));
            await store.Add("expensemembers", J.Obj(new { group_id = id, user_id = UserId, active = true, joined_at = Now }));
            StatusCode = 201;
            return J.Obj(new { success = true, id });
        }
        Require(int.TryParse(idText, out var groupId), "Mã nhóm không hợp lệ");
        var group = groups.FirstOrDefault(g => J.Int(g, "group_id") == groupId);
        Require(group != null, "Không tìm thấy nhóm", 404);
        var own = J.Int(group!, "owner_id") == UserId;
        Require(adminView || members.Any(m => J.Int(m, "group_id") == groupId && J.Int(m, "user_id") == UserId && J.Bool(m, "active")), "Không tìm thấy nhóm", 404);
        var action = parts.ElementAtOrDefault(offset + 1) ?? "";
        var target = parts.ElementAtOrDefault(offset + 2) ?? "";
        Require(parts.Length <= offset + (action == "" ? 1 : target == "" ? 2 : 3), "Endpoint không tồn tại", 404);
        var entries = (await store.All("expenseentries")).Where(x => J.Int(x, "group_id") == groupId && !J.Bool(x, "deleted")).ToArray();
        if (Method == "GET" && action == "")
        {
            var month = request.Query["month"];
            if (month == "") month = Today[..7];
            Require(DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _), "Tháng không hợp lệ");
            var selected = entries.Where(x => J.Str(x, "date").StartsWith(month + "-", StringComparison.Ordinal)).OrderByDescending(x => J.Str(x, "date")).ThenByDescending(x => J.Int(x, "entry_id")).ToArray();
            var income = selected.Where(x => J.Str(x, "kind") == "income").Sum(x => J.Money(x, "amount"));
            var expense = selected.Where(x => J.Str(x, "kind") == "expense").Sum(x => J.Money(x, "amount"));
            var users = await store.All("users");
            return J.Obj(new { success = true, group = J.Public(group!), canManage = own && !adminView, month,
                members = members.Where(m => J.Int(m, "group_id") == groupId && J.Bool(m, "active")).Select(m => new { userId = J.Int(m, "user_id"), name = users.FirstOrDefault(u => J.Int(u, "user_id") == J.Int(m, "user_id")) is { } u ? J.Str(u, "name") : "Thành viên", isOwner = J.Int(m, "user_id") == J.Int(group!, "owner_id") }).ToArray(),
                entries = selected.Select(x => J.Public(x)).ToArray(),
                summary = new { income, expense, balance = income - expense, budget = J.Money(group!, "budget"), overBudget = J.Money(group!, "budget") > 0 && expense > J.Money(group!, "budget"), byCategory = selected.Where(x => J.Str(x, "kind") == "expense").GroupBy(x => J.Str(x, "category")).Select(g => new { category = g.Key, amount = g.Sum(x => J.Money(x, "amount")) }).ToArray() } });
        }
        if (adminView)
        {
            Require(Method == "PATCH" && action == "status" && target == "", "Endpoint không tồn tại", 404);
            Require(body["archived"] is JsonValue v && v.TryGetValue<bool>(out _), "Trạng thái không hợp lệ");
            Version(group!); group!["archived"] = J.Bool(body, "archived"); Bump(group!);
            await store.Add("securitylogs", J.Obj(new { user_id = UserId, action = "group_status", group_id = groupId, archived = J.Bool(group!, "archived"), created_at = Now }));
            return Ok("Đã cập nhật trạng thái nhóm");
        }
        Require(!J.Bool(group!, "archived"), "Nhóm đã được lưu trữ", 409);
        if (action == "" && Method == "PATCH")
        {
            Require(own, "Chỉ trưởng nhóm được sửa nhóm", 403);
            var name = Text("name", 100); var budget = Budget(); Version(group!);
            group!["name"] = name; group["budget"] = budget; Bump(group);
            return Ok("Đã cập nhật nhóm");
        }
        if (action == "members")
        {
            Require(own, "Chỉ trưởng nhóm được quản lý thành viên", 403);
            if (Method == "POST" && target == "")
            {
                var email = Text("email", 254);
                var user = (await store.All("users")).FirstOrDefault(u => J.Str(u, "email").Equals(email, StringComparison.OrdinalIgnoreCase) && !Blocked(u));
                Require(user != null, "Không tìm thấy tài khoản đang hoạt động", 404);
                var uid = J.Int(user!, "user_id");
                var existing = members.FirstOrDefault(m => J.Int(m, "group_id") == groupId && J.Int(m, "user_id") == uid);
                Require(existing == null || !J.Bool(existing, "active"), "Thành viên đã có trong nhóm", 409);
                if (existing != null) existing["active"] = true;
                else await store.Add("expensemembers", J.Obj(new { group_id = groupId, user_id = uid, active = true, joined_at = Now }));
                await Notify(uid, "Chi tiêu nhóm", "Bạn được thêm vào nhóm " + J.Str(group!, "name"));
                return Ok("Đã thêm thành viên");
            }
            if (Method == "DELETE" && int.TryParse(target, out var uidRemove))
            {
                Require(uidRemove != UserId, "Không thể xóa trưởng nhóm");
                var member = members.FirstOrDefault(m => J.Int(m, "group_id") == groupId && J.Int(m, "user_id") == uidRemove && J.Bool(m, "active"));
                Require(member != null, "Không tìm thấy thành viên", 404); member!["active"] = false;
                return Ok("Đã xóa thành viên khỏi nhóm");
            }
        }
        if (action == "entries")
        {
            JsonObject? entry = null;
            if (target != "")
            {
                Require(int.TryParse(target, out var eid), "Mã khoản thu chi không hợp lệ");
                entry = entries.FirstOrDefault(x => J.Int(x, "entry_id") == eid);
                Require(entry != null, "Không tìm thấy khoản thu chi", 404);
                Require(own || J.Int(entry!, "created_by") == UserId, "Chỉ người tạo hoặc trưởng nhóm được sửa khoản thu chi", 403);
                Version(entry!);
            }
            if (Method == "DELETE" && entry != null) { entry["deleted"] = true; Bump(entry); return Ok("Đã xóa khoản thu chi"); }
            Require(Method == "POST" && target == "" || Method == "PUT" && entry != null, "Endpoint không tồn tại", 404);
            var amount = Amount(body); var title = Text("title", 150); var category = Text("category", 60);
            var kind = J.Str(body, "kind"); Require(kind is "income" or "expense", "Loại thu chi không hợp lệ");
            var date = J.Str(body, "date");
            Require(DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) && day >= new DateOnly(2000, 1, 1) && day <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), "Ngày phát sinh không hợp lệ");
            var payer = J.Int(body, "payer_id", UserId);
            Require(members.Any(m => J.Int(m, "group_id") == groupId && J.Int(m, "user_id") == payer && J.Bool(m, "active")), "Người thanh toán không thuộc nhóm");
            if (entry == null)
            {
                entry = J.Obj(new { entry_id = await store.Next("expenseentries", "entry_id"), group_id = groupId, created_by = UserId, created_at = Now, version = 0, deleted = false });
                await store.Add("expenseentries", entry); StatusCode = 201;
            }
            entry["amount"] = amount; entry["title"] = title; entry["category"] = category; entry["kind"] = kind; entry["date"] = date; entry["payer_id"] = payer; Bump(entry);
            return J.Obj(new { success = true, entry = J.Public(entry) });
        }
        throw new ApiError(404, "Endpoint không tồn tại");
    }
    private string Text(string field, int max) { var text = J.Str(body, field).Trim(); Require(text.Length > 0 && text.Length <= max, $"{field} cần từ 1 đến {max} ký tự"); return text; }
    private decimal Budget() { Require(body.ContainsKey("budget"), "Thiếu ngân sách"); var budget = J.Money(body, "budget"); Require(decimal.TryParse(J.Str(body, "budget"), NumberStyles.Float, CultureInfo.InvariantCulture, out _) && budget >= 0 && budget <= 1_000_000_000_000 && decimal.Truncate(budget) == budget, "Ngân sách không hợp lệ"); return budget; }
    private void Version(JsonObject entity) => Require(J.Int(body, "version") == J.Int(entity, "version"), "Dữ liệu đã thay đổi. Hãy tải lại trước khi sửa.", 409);
    private static void Bump(JsonObject entity) { entity["version"] = J.Int(entity, "version") + 1; entity["updated_at"] = Now; }
}
