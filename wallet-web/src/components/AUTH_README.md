# Authentication Components

Hệ thống authentication đầy đủ cho HTD PAY Wallet với kết nối thật đến API backend.

## 📦 Components

### 1. **LoginModal**
Modal đăng nhập với các tính năng:
- Đăng nhập bằng email hoặc username
- Hiển thị/ẩn mật khẩu
- Ghi nhớ đăng nhập
- Kết nối API `/api/auth/login`
- Social login (Facebook, Google) qua `/api/auth/social`
- Link chuyển sang đăng ký và quên mật khẩu

### 2. **RegisterModal**
Modal đăng ký tài khoản mới:
- Form đầy đủ: Họ tên, Email, SĐT, Mật khẩu
- Validation phía client
- Xác nhận mật khẩu
- Đồng ý điều khoản
- Kết nối API `/api/auth/register`
- Hiển thị thông báo thành công

### 3. **ForgotPasswordModal**
Modal quên mật khẩu:
- Nhập email để reset
- Kết nối API `/api/auth/forgot-password`
- Hiển thị thông báo đã gửi email
- Link quay lại đăng nhập

### 4. **AuthModals**
Component wrapper quản lý tất cả modals:
- Tự động chuyển đổi giữa các modal
- State management tập trung
- Custom hook `useAuthModals()`

## 🚀 Cách sử dụng

### Sử dụng với AuthModals (Khuyên dùng)

```tsx
import AuthModals, { useAuthModals } from '@/components/AuthModals';

function App() {
  const { activeModal, openLogin, openRegister, close } = useAuthModals();

  return (
    <>
      <button onClick={openLogin}>Đăng nhập</button>
      <button onClick={openRegister}>Đăng ký</button>
      
      <AuthModals initialModal={activeModal} onClose={close} />
    </>
  );
}
```

### Sử dụng riêng lẻ

```tsx
import { useState } from 'react';
import LoginModal from '@/components/LoginModal';
import RegisterModal from '@/components/RegisterModal';
import ForgotPasswordModal from '@/components/ForgotPasswordModal';

function App() {
  const [showLogin, setShowLogin] = useState(false);
  const [showRegister, setShowRegister] = useState(false);
  const [showForgot, setShowForgot] = useState(false);

  return (
    <>
      <LoginModal
        isOpen={showLogin}
        onClose={() => setShowLogin(false)}
        onSwitchToRegister={() => {
          setShowLogin(false);
          setShowRegister(true);
        }}
        onForgotPassword={() => {
          setShowLogin(false);
          setShowForgot(true);
        }}
      />
      
      <RegisterModal
        isOpen={showRegister}
        onClose={() => setShowRegister(false)}
        onSwitchToLogin={() => {
          setShowRegister(false);
          setShowLogin(true);
        }}
      />
      
      <ForgotPasswordModal
        isOpen={showForgot}
        onClose={() => {
          setShowForgot(false);
          setShowLogin(true);
        }}
      />
    </>
  );
}
```

## 🔌 API Endpoints

### Login - POST `/api/auth/login`
```json
{
  "identifier": "alice@company.com", // hoặc username
  "password": "password123"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Đăng nhập thành công",
  "token": "jwt_token_here",
  "user": {
    "user_id": 1,
    "name": "Alice Nguyễn",
    "email": "alice@company.com",
    "phone": "0901234567"
  }
}
```

### Register - POST `/api/auth/register`
```json
{
  "name": "Alice Nguyễn",
  "email": "alice@company.com",
  "phone": "0901234567",
  "password": "password123"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Đăng ký thành công",
  "userId": 1
}
```

### Social Login - POST `/api/auth/social`
```json
{
  "provider": "facebook", // hoặc "google"
  "accessToken": "social_access_token"
}
```

### Forgot Password - POST `/api/auth/forgot-password`
```json
{
  "email": "alice@company.com"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Email reset mật khẩu đã được gửi"
}
```

## 💾 LocalStorage

Sau khi đăng nhập thành công, các thông tin sau được lưu:

```javascript
localStorage.setItem('token', 'jwt_token');
localStorage.setItem('isLoggedIn', 'true');
localStorage.setItem('userInfo', JSON.stringify({
  user_id: 1,
  name: "Alice Nguyễn",
  email: "alice@company.com",
  phone: "0901234567"
}));
```

## 🎨 Customization

### Thay đổi màu sắc
Sửa trong file component:
- `from-blue-600 to-indigo-600` → Gradient nút submit
- `text-blue-600` → Màu link và text nổi bật
- `border-slate-300` → Màu viền input

### Thay đổi validation
Sửa logic trong các hàm:
- `handleLogin()` - LoginModal
- `handleRegister()` - RegisterModal
- `handleSubmit()` - ForgotPasswordModal

## 🧪 Testing

### Test với mock data
```typescript
// Test login với user có sẵn trong database
email: alice@company.com
password: password123

// Hoặc
email: bob@partner.com
password: password123
```

### Test validation
- Email không hợp lệ: `test@invalid`
- SĐT không hợp lệ: `123` (phải 10-11 số)
- Mật khẩu ngắn: `123` (phải ít nhất 6 ký tự)
- Mật khẩu không khớp

## 🔒 Security Features

- ✅ Password hashing với bcrypt (12 rounds)
- ✅ JWT token authentication (30 ngày hết hạn)
- ✅ Reset password token (1 giờ hết hạn)
- ✅ Input validation phía client và server
- ✅ Protected routes với token check
- ✅ Secure HTTP-only cookies (production)

## 📝 Notes

1. **API Base URL**: Hiện tại sử dụng `/api/auth/*`. Nếu backend chạy khác port, cần config proxy trong `vite.config.ts`

2. **Social Login**: Cần tích hợp Facebook SDK và Google Sign-In SDK để lấy `accessToken` thật

3. **Email Service**: Cần cấu hình SMTP server để gửi email reset password

4. **Production**: Nhớ thay đổi `JWT_SECRET` trong `.env` file

## 🐛 Troubleshooting

### Lỗi CORS
Thêm vào `vite.config.ts`:
```typescript
server: {
  proxy: {
    '/api': {
      target: 'http://localhost:5000',
      changeOrigin: true
    }
  }
}
```

### Lỗi "Cannot connect to database"
Kiểm tra:
1. MongoDB đang chạy: `mongod --version`
2. Connection string trong `.env.local`
3. Network firewall

### Token hết hạn
Token JWT có thời hạn 30 ngày. Sau đó user phải đăng nhập lại.
Có thể implement refresh token mechanism.

## 📚 Related Files

- `src/api/auth/login/route.ts` - Login API handler
- `src/api/auth/register/route.ts` - Register API handler
- `src/api/auth/social/route.ts` - Social login handler
- `src/api/auth/forgot-password/route.ts` - Forgot password handler
- `src/api/auth/reset-password/route.ts` - Reset password handler
- `src/lib/schemas/user.schema.ts` - User Mongoose schema
- `src/lib/mongodb.ts` - MongoDB connection

## 🎯 Roadmap

- [ ] Email verification sau đăng ký
- [ ] Two-factor authentication (2FA)
- [ ] Biometric authentication (Face ID, Touch ID)
- [ ] Session management (multiple devices)
- [ ] Password strength meter
- [ ] Rate limiting cho login attempts
- [ ] Account lockout after failed attempts
