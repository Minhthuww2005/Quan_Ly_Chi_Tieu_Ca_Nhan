# EF Core, Redis, Channel/BackgroundService và SignalR trong HTD PAY

## Luồng đã nối vào ứng dụng

Chức năng thu chi nhóm tại `/groups` và `/admin/groups` là luồng sử dụng bốn kỹ thuật. Controller kiểm tra JWT; service kiểm tra tài khoản và thành viên. Repository đọc/ghi ba bảng nhóm bằng `GroupExpensesDbContext` và EF Core/Npgsql. Dữ liệu ví cũ tiếp tục dùng Npgsql; hai cơ chế dùng chung connection và transaction khi một yêu cầu cần cả hai.

Khi một yêu cầu ghi thay đổi nhóm/thành viên/khoản thu chi, repository tăng `report_version` của nhóm, lưu thay đổi bằng `SaveChangesAsync` và thêm một dòng `htdpay.group_outbox` trong cùng transaction. Chỉ sau commit mới ghi tín hiệu vào Channel. Channel có giới hạn 128 tín hiệu; nếu đầy, sự kiện vẫn nằm trong PostgreSQL. Worker quét định kỳ hai giây để tìm sự kiện chưa hoàn tất, kể cả sự kiện tồn tại từ trước khi host khởi động.

Worker đọc tối đa 20 sự kiện mỗi batch với `FOR UPDATE SKIP LOCKED`. Worker kiểm tra lại tài khoản và thành viên đang hoạt động, gửi `GroupChanged` qua SignalR tới các user ID tương ứng và admin đang hoạt động, rồi đánh dấu processed. Thất bại giao vận được lên lịch thử lại với độ trễ tăng dần, tối đa 60 giây. Lỗi database rollback batch và được quét lại. Khi host dừng, các thao tác chờ Channel/timer/database nhận cancellation token. Sự kiện chưa commit hoàn tất vẫn chờ ở database.

Sự kiện chỉ chứa `eventId` và `groupId`, không chứa số tiền, tên thành viên hoặc nội dung khoản chi. Giao diện nhận tín hiệu, gom các tín hiệu gần nhau trong 200 ms và tải lại qua API. Reconnect luôn lấy snapshot mới. Thành viên đã bị thu hồi quyền không thể dùng cache để đọc nhóm, vì service kiểm tra quyền từ PostgreSQL trước khi đọc cache.

## EF Core và mô hình dữ liệu

`GroupExpensesDbContext` ánh xạ ba kiểu `GroupDocument<T>` vào `htdpay.expensegroups`, `expensemembers`, `expenseentries`; `record_key` là khóa và `data` là JSONB. Đây là ánh xạ tương thích schema đang có, không phải mô hình bảng quan hệ chuẩn hóa của module RevenueFlow. EF Core dùng `AsNoTracking` khi đọc và trạng thái Added/Modified/Deleted khi ghi. Cache theo request của repository tiếp tục giữ các tài liệu được service chỉnh sửa.

DbContext tham gia transaction Npgsql có sẵn qua `UseTransactionAsync`. Không bật retry tự động quanh các phần rời của transaction. Nếu EF lỗi, controller không commit; dispose transaction rollback cả phần Npgsql lẫn EF. Migration bổ sung dùng SQL có khóa, không dùng `EnsureCreated`, không xóa hoặc tạo lại 31 bảng ví nguồn. `group_outbox` và schema version 3 được thêm sau ba bảng nhóm của version 2. Không mô tả đây là EF migration Baseline của RevenueFlow.

## Redis và tính mới của báo cáo

Cache chỉ chứa các khoản của một tháng và tổng hợp; tên nhóm, trạng thái, thành viên và quyền vẫn đọc từ database. Khóa logic là `htdpay:groups:v1:{groupId}:{report_version}:{yyyy-MM}` với tiền tố `HTDPay:{hash-máy-chủ-cổng-database}:` và TTL 60 giây. Khi ghi xong, `report_version` đã tăng trong transaction, nên request mới không dùng lại cache của phiên bản cũ, kể cả khi worker chưa chạy. Khóa cũ hết hạn tự nhiên.

Header `X-Group-Cache` trả `MISS`, `HIT`, `BYPASS` hoặc `NOT-USED`; `X-Group-Persistence` xác định endpoint dùng EF Core/PostgreSQL. Redis hỏng làm request đọc lại database và trả BYPASS; circuit breaker bỏ qua Redis trong 5 giây sau lỗi để tránh thử lặp lại ở từng bước của cùng yêu cầu; không lấy số liệu mặc định bằng 0 để báo thành công. HIT giúp bỏ truy vấn tải khoản thu chi, nhưng vẫn có truy vấn tài khoản/nhóm/thành viên nhằm kiểm tra quyền. Không gọi kết quả này là “không truy vấn database”.

## Chạy local

1. Cấu hình database và JWT như trước trong `.env.wallet`.
2. Khởi động Redis local, ví dụ service redis trong `docker-compose.yml`, hoặc Redis riêng chỉ bind loopback.
3. Đặt `Cache__Redis=localhost:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000,asyncTimeout=5000` theo cổng thực tế. Không commit cấu hình chứa mật khẩu.
4. Chạy API qua `scripts/start-wallet.ps1`, giao diện qua `pnpm dev` trong `wallet-web`. Vite proxy cả `/api` và `/hubs` với WebSocket.
5. Mở hai tài khoản thuộc cùng nhóm và một admin. Khi một tài khoản thêm khoản chi, hai màn hình còn lại tải số liệu mới; người ngoài nhóm không nhận sự kiện.

SignalR dùng JWT hiện tại, đọc access_token từ query chỉ trên `/hubs/groups` khi trình duyệt dùng WebSocket/SSE. Access token không được đưa vào log của ứng dụng. Kết nối bị đóng khi token hết hạn. Cache/Channel/worker/SignalR không xử lý việc trừ tiền ví hoặc xác nhận thanh toán VNPay.

## Phạm vi và giới hạn

- EF Core hiện dùng cho dữ liệu nhóm; các bảng ví khác tiếp tục dùng Npgsql JSONB.
- Cache dùng Redis thật khi cấu hình/kết nối đúng; BYPASS cần được phân biệt với HIT trong minh chứng.
- Worker và database cung cấp giao vận ít nhất một lần. Nếu process dừng sau khi phát nhưng trước khi đánh dấu processed, cùng sự kiện có thể được gửi lại; giao diện chỉ tải lại nên không tạo khoản chi trùng.
- SignalR hiện phục vụ demo một backend. Nhiều backend cần backplane hoặc dịch vụ SignalR phù hợp; việc Redis cache đã hoạt động không tự tạo SignalR backplane.
- Cần chính sách dọn outbox đã xử lý khi dữ liệu lớn. Cách truy vấn JSONB theo collection hiện vẫn có giới hạn quy mô.
- Các lab RevenueFlow cũ vẫn là thí nghiệm riêng; chỉ dùng luồng nhóm này làm minh chứng tích hợp HTD PAY. Không lấy toàn bộ chức năng hoặc số test của RevenueFlow gán cho ví.

## Nguồn kỹ thuật

- Microsoft Learn, [Transactions in EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions).
- Microsoft Learn, [Background tasks with hosted services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0).
- Microsoft Learn, [SignalR authentication and authorization](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-10.0).

## Kết quả kiểm chứng ngày 23/10/2026

| Bộ kiểm tra | Kết quả | Phạm vi |
|---|---|---|
| WalletGroupTests | 50 kiểm tra đạt | Service thật, repository bộ nhớ; quyền, dữ liệu đầu vào, version, ngân sách, lưu trữ nhóm |
| WalletGroupIntegration | 50 kiểm tra đạt | HTTP + PostgreSQL + EF Core + Redis + WebSocket thật; nhóm QA số 4 |
| wallet-group-http-tests.py | 38 kiểm tra HTTP đạt | Chạy lại trên bản EF Core; nhóm QA số 5; cập nhật đồng thời nhận 200/409; số dư ví không đổi |
| Build backend và integration runner | Thành công, 0 lỗi/0 cảnh báo | .NET 10; binary mới dùng cho demo |
| Build client/admin | Thành công | TypeScript/Vite; còn cảnh báo bundle chính lớn và chú thích PURE trong dependency SignalR |

Ba bộ có phạm vi giao nhau nên không cộng thành số ca chức năng độc lập. Các kiểm tra ghi dữ liệu được chạy trên PostgreSQL QA local riêng. Không dùng Neon để tạo giao dịch thử cho các bộ này. CI đã được cấu hình chạy bộ tích hợp với PostgreSQL/Redis riêng; kết quả local không đồng nghĩa một GitHub Actions run đã đạt.

Bộ tích hợp xác nhận Redis có TTL dương không quá 60 giây; cold-cache MISS rồi HIT; thêm khoản chi làm đổi generation và số liệu ngay. Để tách giai đoạn thiết lập kết nối, test chờ Redis hết BYPASS và chỉ xóa đúng khóa nhóm QA trước khi kiểm tra cold-cache. Đã quan sát được BYPASS khi kết nối cache chưa sẵn sàng; không dùng phản hồi đó làm minh chứng HIT.

WebSocket được kiểm tra bằng bốn .NET client: trưởng nhóm, thành viên, người ngoài và admin. Người ngoài không nhận sự kiện; sau khi xóa thành viên, user đó không nhận event mới và đọc API nhận 404. Reconnect đọc lại số liệu 123000 VND đã commit. Sự kiện chèn trực tiếp vào outbox mà không phát tín hiệu Channel vẫn được scanner xử lý. Worker thật dừng qua StopAsync trong giới hạn kiểm tra 5 giây.

Kiểm tra rollback cố ý làm EF vi phạm unique index sau khi sửa tên một tài khoản QA bằng Npgsql trong cùng transaction. EF ném DbUpdateException; dispose unit of work rollback cả hai phần, và API auth/me không trả tên thử MUST_ROLL_BACK. Đây là minh chứng transaction liên kết, không phải một giao dịch tiền trên tài khoản thật.

Chưa kiểm tra thao tác trực quan bằng trình duyệt trong phiên này. Build giao diện và các .NET WebSocket client không thay thế kiểm thử tương tác của JavaScript trên từng trình duyệt. Không suy rộng số liệu local thành hiệu năng production hoặc khả năng chạy nhiều backend.
