# Một bất biến tiền đo trên bản trước sửa và bản cuối — 26/10/2026

**Lệnh:** `python3 scripts/wallet-before-after.py --label <commit> --out <file>`, chạy lần lượt trên hai API dùng chung
một database PostgreSQL 16.15 local (biến PGHOST/PGPORT/PGUSER/PGPASSWORD/PGDATABASE trỏ tới database đó).

| File | API | Commit | Kết quả |
|---|---|---|---|
| `before-bf21d48.json` | cổng 5084 | `bf21d48` (trước bản sửa luồng tiền) | FAIL: số dư người dùng +45.000.000 VND, tổng số dư các ví +45.000.000 VND |
| `after-f8cd0f8.json` | cổng 5082 | `f8cd0f8` (bản cuối) | PASS: số dư người dùng và tổng số dư các ví không đổi |

**Kịch bản:** một tài khoản QA mới có 20.000.000 VND (ghi thẳng vào database local để hai bản bắt đầu giống nhau),
mua khoản đầu tư 5.000.000 VND, rồi bán và gửi `sellPrice` 50.000.000 VND.

**Bất biến:** bán chỉ được hoàn giá trị server đang giữ, nên số dư sau khi mua và bán bằng số dư ban đầu và tổng số dư
mọi ví không tăng. Bản `bf21d48` cộng đúng `sellPrice` của client nên tạo ra 45.000.000 VND; bản cuối chỉ hoàn 5.000.000 VND.

Kiểm tra file: `sha256sum -c SHA256SUMS`. Đây là phép thử local, không dùng Neon.
