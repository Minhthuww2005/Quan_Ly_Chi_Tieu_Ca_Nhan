# HTD PAY: Vercel frontend and ASP.NET Core backend

Deploy wallet-web on Vercel Hobby (Vite, npm run build, dist). Set VITE_API_ORIGIN to the HTTPS backend origin before building. The value is public; never put database, JWT or VNPay secrets in VITE_ variables.

Backend: Dockerfile.wallet, port 8080, Production. Required server settings: ConnectionStrings__Database, Jwt__SigningKey, Google__ClientId, Vnpay__TmnCode, Vnpay__HashSecret, Vnpay__PaymentUrl, Vnpay__ReturnUrl, Cors__AllowedOrigins (exact Vercel frontend origin), Cache__Redis. Persist .wallet-keys to preserve access to encrypted existing data. The current wallet requires an existing htdpay schema; review additive migrations separately before deploying a newer build against shared Neon.

VNPay: ReturnUrl = https://FRONTEND/payments/return. Register https://BACKEND/api/payments/vnpay/ipn with VNPay. Return verification is read-only in Production; signed IPN is authoritative and preserves replay, amount, merchant and expiration checks. A browser redirect alone must not credit the wallet.

For localhost demonstrations only, Vnpay__AllowLocalReturnSettlement=true additionally enables authenticated POST /api/payments/vnpay/confirm-local with the signed callback query. It requires Development and a loopback ReturnUrl; disabled for public ReturnUrl and Production even if flag is true. The owning user, signature, amount and expiration are validated. Replay cannot credit twice. This convenience endpoint is not a production payment integration.

Free backend hosts may sleep; test cold startup, SignalR reconnect and outbox processing. Do not promise continuous background execution on a sleeping free instance.
