# Phạm vi cố định

## Chỉ có ba chức năng nghiệp vụ

| Mã | Chức năng | Route/module chính |
|---|---|---|
| REV-01 | Quản lý giao dịch doanh thu Income | Minimal API `/api/revenues` |
| REV-02 | Tổng hợp và Dashboard doanh thu | Controller `GET /api/reports/revenue` |
| REV-03 | Doanh thu định kỳ và xử lý nền | `/api/recurring-rules` + worker |

## Dữ liệu được phép có

- Users
- Groups
- GroupMembers
- Wallets
- Categories
- Transactions, chỉ dùng `Kind=Income`
- RecurringRules
- RecurringRunLogs

## Không được tự thêm

- Expense hoặc quản lý chi tiêu.
- Budget hoặc BudgetAlert.
- Module thông báo độc lập.
- Bảng Report lưu sẵn.
- Endpoint riêng cho từng kiểu groupBy.
- Module riêng cho Daily, Weekly và Monthly.
- RabbitMQ song song với Channel nếu nhóm chưa đổi quyết định kiến trúc.

Nếu một ý tưởng mới không chứng minh thêm yêu cầu kỹ thuật trong workbook, không đưa vào scope.
