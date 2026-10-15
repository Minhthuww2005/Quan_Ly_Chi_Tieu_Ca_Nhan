# Kiến trúc OOP của HTD PAY trên ASP.NET Core

## Cấu trúc và trách nhiệm

```text
src/WalletFunding.Api/
  Controllers/     Nhận HTTP, chuyển request/result, khai báo route
  Contracts/       WalletRequest, RequestValues, ServiceResponse
  Services/        Các lớp xử lý nghiệp vụ và interface IWalletService
  Domain/          WalletAccount đóng gói quy tắc số dư
  Repositories/    IWalletRepository và PostgresWalletRepository
  Middleware/      ApiExceptionMiddleware
  Common/          JsonHelpers và ApiError
  Program.cs       Đăng ký DI, JWT, middleware, MVC và Swagger
```

Luồng gọi: React → Controller → Service → Repository → PostgreSQL trên Neon. Controller chuyển dữ liệu HTTP thành WalletRequest; service trả ServiceResponse. Service không nhận HttpContext, không gọi SQL. Repository dùng Npgsql để lưu dữ liệu JSONB vào schema htdpay. Frontend vẫn dùng các đường dẫn API cũ.

## Các controller và service

| Controller | Service | Nhóm đường dẫn |
|---|---|---|
| AuthController | AuthService | /api/auth, /api/security, /api/admin/login |
| WalletController | WalletService | /api/wallet |
| TransactionsController | TransactionService | /api/transactions, /api/transfer |
| BankingController | BankingService | /api/bank, /api/banks, /api/bank-accounts, /api/cards |
| FinanceController | FinanceService | /api/savings, /api/investments, /api/loans, /api/insurance |
| AdministrationController | AdministrationService | /api/admin, /api/kyc |
| CatalogController | CatalogService | /api/contacts, /api/notifications, /api/merchants, /api/webhook |

WalletControllerBase chứa phần chuyển đổi HTTP và ranh giới transaction dùng chung. WalletServiceBase chứa kiểm tra tài khoản, quyền sở hữu, PIN và helper nghiệp vụ dùng chung. Mỗi service là một lớp độc lập, không còn các phần của một lớp partial WalletApi. Các controller dùng route theo nhóm và phần đường dẫn còn lại để giữ tương thích API nguồn; chưa tách mọi thao tác thành action MVC với DTO riêng.

## Ví dụ luồng chuyển tiền

1. TransferModal của React gọi POST /api/transfer/execute. transport.ts gắn JWT.
2. TransactionsController nhận request. WalletControllerBase đọc JSON, query, header và principal, tạo WalletRequest.
3. Repository mở transaction và advisory lock cho request ghi.
4. TransactionService kiểm tra người gửi, quyền sở hữu, PIN, số tiền, người nhận và hạn mức theo KYC.
5. WalletAccount.Debit kiểm tra trạng thái ví và số dư, rồi trừ tiền. WalletAccount.Credit cộng tiền vào ví nhận.
6. Service tạo hai bản ghi giao dịch cùng reference và thông báo người nhận.
7. Repository ghi thay đổi và commit. Controller chuyển ServiceResponse thành JSON với HTTP status tương ứng.
8. Nếu có lỗi, middleware chuyển ApiError thành phản hồi thích hợp. Transaction chưa commit được giải phóng và rollback khi scoped repository được dispose.

## OOP thể hiện ở đâu?

- Đóng gói: WalletAccount giữ quy tắc Debit/Credit; bên gọi không cần biết cách cập nhật số dư trong JSONB.
- Trừu tượng: service truy cập dữ liệu qua IWalletRepository; triển khai hiện tại là PostgresWalletRepository.
- Kế thừa: các service kế thừa WalletServiceBase; controller kế thừa WalletControllerBase để dùng chung phần xử lý có cùng trách nhiệm.
- Đa hình: IWalletService và ExecuteAsync được các service triển khai; controller base gọi qua interface.
- Dependency injection: Program.cs đăng ký repository và service theo lifetime Scoped, dùng chung một repository trong mỗi request. NpgsqlDataSource được đăng ký Singleton để quản lý pool kết nối.

## Những công nghệ ASP.NET Core có thể nghiên cứu trong báo cáo

ASP.NET Core MVC với ApiController và attribute routing; dependency injection; middleware và pipeline; JWT authentication; kiểm tra quyền admin/chủ sở hữu; Data Protection; HttpClientFactory; rate limiting; Swagger/OpenAPI; transaction PostgreSQL qua Npgsql.

Đây là ứng dụng ASP.NET Core 10. Cấu trúc đã tách Controller–Service–Repository, nhưng dữ liệu lưu dưới dạng JSONB để tương thích bản nguồn; chưa dùng EF Core, chưa chuẩn hóa toàn bộ entity thành bảng quan hệ, và hợp đồng nghiệp vụ vẫn dùng JSON cho nhiều thao tác. Không nên ghi trong báo cáo rằng đã triển khai EF Core, Clean Architecture đầy đủ hoặc DTO riêng cho từng endpoint. Ngân hàng nội bộ vẫn mô phỏng; PaymentService và VnpayGateway bổ sung checkout VNPay sandbox, Return URL và IPN HMACSHA512. GoogleIdentityVerifier xác minh Google JWT qua khóa công khai. Xem WALLET_TEST_REPORT.md để phân biệt kiểm thử local với tích hợp thực tế.

## Kiểm thử

Sau refactor, backend build thành công với 0 lỗi và 0 cảnh báo; toàn bộ 85 kiểm thử HTTP trên PostgreSQL local đều đạt. Endpoint Swagger trả HTTP 200.

Chạy backend local bằng scripts/start-wallet.ps1 -Local, rồi chạy scripts/wallet-smoke.py. Bộ kiểm thử HTTP xác minh API từ bên ngoài, gồm đăng nhập, quyền sở hữu/admin, chuyển tiền đồng thời, PIN, tài chính, KYC, nạp ngân hàng và token reset. Swagger có thể xem tại http://localhost:5081/swagger.

