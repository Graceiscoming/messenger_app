# 08 – Implementation Guide (พร้อมนำไปพัฒนา)

## 1. โครงสร้าง Repository
```
messenger_app/
├── docs/                         # เอกสารชุดนี้
├── src/
│   ├── Messenger.Api/            # Presentation boundary + composition root
│   │   ├── Controllers/          # Auth, Users, Friends, Conversations, Messages, Files, Stickers
│   │   ├── Hubs/ChatHub.cs, SignalRNotifier.cs
│   │   ├── Middleware/           # ExceptionHandling (Domain exception → ProblemDetails)
│   │   ├── Program.cs            # DI, JWT, CORS, SignalR, Swagger
│   │   └── appsettings.json
│   ├── Messenger.Domain/         # Domain Layer
│   │   ├── Entities/             # User, Friendship, Conversation, Message, ...
│   │   ├── Enums/
│   │   ├── Services/             # I*Service + implementations
│   │   ├── Abstractions/         # IRealtimeNotifier, repositories interfaces, IUnitOfWork
│   │   ├── Dtos/
│   │   └── Exceptions/           # NotFound, Forbidden, Conflict, Validation
│   └── Messenger.Data/           # Data Access Layer
│       ├── MessengerDbContext.cs
│       ├── Configurations/       # IEntityTypeConfiguration<T> ต่อ entity
│       ├── Repositories/
│       ├── Migrations/
│       └── Seed/StickerSeed.cs
├── tests/
│   ├── Messenger.Domain.Tests/   # xUnit: unit test กฎ business (mock repo)
│   └── Messenger.Api.Tests/      # WebApplicationFactory + Testcontainers PostgreSQL
├── web/                          # React + TS (Vite)
│   └── src/{api,hubs,pages,components,store}
├── docker-compose.yml
└── readme.md
```
ทิศทาง dependency: `Api → Domain ← Data` (Data implement interface ที่ Domain ประกาศ; Api ลงทะเบียน DI)

## 2. Packages (NuGet)
- `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- `FluentValidation.AspNetCore` (หรือ DataAnnotations)
- `SixLabors.ImageSharp` (อ่านขนาด/ตรวจรูป)
- Test: `xunit`, `Moq`/`NSubstitute`, `Testcontainers.PostgreSql`, `Microsoft.AspNetCore.Mvc.Testing`
- SignalR มากับ ASP.NET Core แล้ว; ฝั่ง web ใช้ `@microsoft/signalr`

## 3. Configuration (`appsettings.json`)
```json
{
  "ConnectionStrings": { "Default": "Host=db;Port=5432;Database=messenger;Username=messenger;Password=CHANGE_ME" },
  "Jwt": { "Issuer": "messenger", "Audience": "messenger-web", "Key": "<>=32 chars secret via env>", "AccessMinutes": 15, "RefreshDays": 7 },
  "Chat": { "UnsendWindowHours": 24, "MaxMessageLength": 2000, "PageSize": 30 },
  "Storage": { "UploadPath": "/data/uploads", "MaxImageBytes": 5242880 }
}
```
Secret (Jwt:Key, DB password) ส่งผ่าน environment variable — ห้าม commit ค่าจริง

## 4. Docker Compose
```yaml
services:
  db:
    image: postgres:16
    environment:
      POSTGRES_DB: messenger
      POSTGRES_USER: messenger
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes: [pgdata:/var/lib/postgresql/data]
    healthcheck: { test: ["CMD-SHELL","pg_isready -U messenger"], interval: 5s, retries: 10 }
  api:
    build: ./src
    environment:
      ConnectionStrings__Default: Host=db;Database=messenger;Username=messenger;Password=${DB_PASSWORD}
      Jwt__Key: ${JWT_KEY}
      Storage__UploadPath: /data/uploads
    volumes: [uploads:/data/uploads]
    depends_on: { db: { condition: service_healthy } }
  web:
    build: ./web          # nginx serve dist + proxy /api และ /hubs ไป api:8080
    ports: ["80:80"]
    depends_on: [api]
volumes: { pgdata: {}, uploads: {} }
```
API ทำ `Database.Migrate()` ตอนสตาร์ต (เฉพาะ dev/compose) เพื่อสร้าง schema และ seed sticker

## 5. คำสั่งเริ่มต้น
```bash
dotnet new sln -n Messenger
dotnet new webapi -o src/Messenger.Api
dotnet new classlib -o src/Messenger.Domain
dotnet new classlib -o src/Messenger.Data
dotnet ef migrations add Initial -p src/Messenger.Data -s src/Messenger.Api
dotnet ef database update -p src/Messenger.Data -s src/Messenger.Api
```

## 6. Web (Presentation) – หน้าจอ
| หน้า | ฟังก์ชัน |
| :--- | :--- |
| Login / Register | UC-01, UC-02 |
| Main layout | Sidebar (แท็บ แชท / เพื่อน / คำขอ) + พื้นที่แชท |
| Chat list | ห้องล่าสุด, unread badge, ค้นหา |
| Chat room | ข้อความ (bubble), โหลดเพิ่มเมื่อเลื่อนขึ้น, ช่องพิมพ์, ปุ่มแนบรูป/sticker picker, เมนู "ยกเลิกการส่ง", typing indicator, read receipt |
| Friends | รายชื่อ+ออนไลน์, ค้นหา/เพิ่มเพื่อน, คำขอเข้า/ออก, ลบ/บล็อก |
| Group | สร้างกลุ่ม, จัดการสมาชิก, ออกจากกลุ่ม |
| Profile | แก้ชื่อ/สถานะ/รูป |

State: React Query (REST cache) + SignalR event อัปเดต cache; เก็บ access token ใน memory, refresh token ใน httpOnly cookie (หรือ localStorage ถ้าเน้นเรียบง่าย — ระบุในรายงานว่าเป็น trade-off)

## 7. แผนพัฒนาแบบ Iteration
| Iteration | ขอบเขต | ผลลัพธ์ที่ตรวจรับ |
| :--- | :--- | :--- |
| 1 | Solution, DB, Auth, Docker | สมัคร/ล็อกอินได้ |
| 2 | **Friend list** (ค้นหา, คำขอ, ยอมรับ, ลบ) | Requirement ขั้นต่ำ ข้อ 2 |
| 3 | **ส่ง-รับข้อความ 1:1** + SignalR + ประวัติ | Requirement ขั้นต่ำ ข้อ 1 |
| 4 | Unread/Read receipt, Unsend | |
| 5 | รูปภาพ + Sticker | |
| 6 | กลุ่มสนทนา, บล็อก, Presence/Typing | |
| 7 | Test, เก็บรายละเอียด UI, เอกสาร, รายงานการใช้ AI | พร้อมส่ง |

## 8. กลยุทธ์ทดสอบ
- **Unit (Domain):** FriendService (ซ้ำ/บล็อก/ยอมรับอัตโนมัติ), MessageService (ไม่ใช่เพื่อน → Forbidden, unsend ไม่ใช่เจ้าของ/เลยเวลา), ConversationService (สิทธิ์ Admin, Owner ออกกลุ่ม)
- **Integration:** Testcontainers PostgreSQL + WebApplicationFactory ทดสอบ flow register → friend → send → history → unsend
- **Manual E2E checklist:** เปิดสองเบราว์เซอร์/สองบัญชี ตรวจ realtime ทุก event ในตาราง SignalR

### Acceptance Checklist
- [ ] สมัคร/ล็อกอิน 2 บัญชี
- [ ] เพิ่มเพื่อน → ยอมรับ → ปรากฏใน Friend list ทั้งสองฝั่ง
- [ ] ส่งข้อความหากัน เห็นทันทีโดยไม่ refresh, รีเฟรชแล้วประวัติยังอยู่
- [ ] คนที่ไม่ใช่เพื่อนส่งข้อความหากันไม่ได้ (API คืน 403)
- [ ] ส่งรูป/Sticker ได้
- [ ] Unsend แล้วอีกฝั่งเห็น placeholder ทันที และไฟล์ถูกลบ
- [ ] สร้างกลุ่ม ≥ 3 คน ส่งข้อความถึงทุกคน, เพิ่ม/ลบสมาชิกได้

## 9. Security Checklist
- [ ] Authorize ทุก endpoint (`[Authorize]` เป็น default), ตรวจ membership ทุกครั้ง (ป้องกัน IDOR)
- [ ] ไม่ log รหัสผ่าน/token; ตั้ง rate limit ที่ `/auth/*`
- [ ] CORS จำกัด origin; HTTPS + HSTS ใน production
- [ ] ชื่อไฟล์ที่เก็บเป็น GUID, ตรวจ content-type + magic bytes, ส่งไฟล์ด้วย `X-Content-Type-Options: nosniff`
- [ ] EF parameterized query (ไม่ต่อ string SQL)

## 10. รายงานการใช้ AI (ตามเงื่อนไขโจทย์)
> กรอกให้ตรงความจริงเมื่อส่งงาน

| ส่วนที่ใช้ AI ช่วย | สิ่งที่ AI ทำ | ผลลัพธ์ / สิ่งที่ผู้พัฒนาตรวจแก้เอง |
| :--- | :--- | :--- |
| เอกสารออกแบบ (`/docs`) – Class/Use case/C4/ERD/API | ร่างจาก Requirement ในโจทย์ | ทีมทบทวนและปรับ design ให้ตรงที่ต้องการ: _(ระบุ)_ |
| _(โค้ดส่วนต่างๆ)_ | | _(ระบุ)_ |

ข้อควรปฏิบัติ: อ่านและเข้าใจโค้ดที่ AI สร้างทุกส่วน, ทดสอบเอง, และบันทึกจุดที่ AI ผิดพลาด (hallucination) ที่พบ
