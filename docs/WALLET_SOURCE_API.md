# Đối chiếu API nguồn HTD PAY

Backend nguồn Express/Mongoose; bản chuyển đổi ASP.NET Core + PostgreSQL.

Danh sách dưới đây ghi nhận từ mã nguồn, không phải chỉ từ README.

## admin.routes.ts

- POST `/login`
- GET `/stats`
- GET `/users`
- GET `/transactions`
- GET `/kyc/pending`
- PATCH `/kyc/:kycId/approve`
- PATCH `/kyc/:kycId/reject`
- GET `/user/:userId`
- PATCH `/user/:userId/status`

## auth.routes.ts

- POST `/login`
- POST `/register`
- POST `/google`
- POST `/forgot-password`
- POST `/reset-password`
- POST `/change-password`
- POST `/set-pin`

## bank-account.routes.ts

- GET `/`
- POST `/verify`
- GET `/search`
- GET `/stats`

## bank.routes.ts

- POST `/link`
- POST `/topup`
- GET `/linked/:userId`
- DELETE `/unlink/:bankId`
- PUT `/set-default`

## card.routes.ts

- POST `/add`
- GET `/:userId`
- PATCH `/:cardId/status`
- DELETE `/:cardId`

## contact.routes.ts

- GET `/:userId`

## insurance.routes.ts

- GET `/:userId`
- POST `/create`
- POST `/renew`
- POST `/cancel`

## investment.routes.ts

- GET `/:userId`
- POST `/create`
- PUT `/update`
- POST `/sell`
- POST `/sync-price`

## kyc-test.routes.ts

- POST `/submit`
- GET `/status/:userId`
- PATCH `/update-biometric/:userId`

## kyc.routes.ts

- POST `/submit`
- POST `/submit-verification`
- GET `/:userId`
- GET `/status/:userId`
- PUT `/verify/:kycId`

## loan.routes.ts

- GET `/:userId`
- POST `/create`
- POST `/payment`
- POST `/payoff`

## merchant.routes.ts

- GET `/`
- GET `/billers`

## notification.routes.ts

- GET `/:userId`
- PATCH `/:notificationId/read`
- PATCH `/:userId/read-all`

## savings.routes.ts

- GET `/:userId`
- POST `/create`
- POST `/close`

## security.routes.ts

- GET `/:userId`
- POST `/verify-password`
- POST `/change-password`
- POST `/setup-pin`
- POST `/change-pin`
- POST `/reset-pin`

## seed.routes.ts

- POST `/bank-accounts`
- DELETE `/bank-accounts`

## supported-bank.routes.ts

- GET `/supported`
- GET `/supported/all`
- POST `/supported`
- PUT `/supported/:bank_code`
- PATCH `/supported/:bank_code/toggle`
- DELETE `/supported/:bank_code`

## transaction.routes.ts

- GET `/:userId`
- POST `/transfer`
- POST `/bank-transfer`
- POST `/phone-topup`
- POST `/bill-payment`

## transfer.routes.ts

- POST `/execute`
- GET `/history/:userId`
- GET `/daily-summary/:userId`

## wallet.routes.ts

- GET `/:userId`

## webhook.routes.ts

- POST `/payment`
- POST `/vnpay`
- GET `/health`
