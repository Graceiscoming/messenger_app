# 03 – สถาปัตยกรรมระบบ (C4 Model)

ใช้ C4 Model 4 ระดับ: Context → Container → Component → Deployment (Code level ดูที่ [Class Diagram](04-class-diagram.md))

## Level 1 – System Context

```mermaid
C4Context
    title System Context – Messenger App
    Person(user, "ผู้ใช้ (User)", "สมัคร เพิ่มเพื่อน ส่งข้อความ สร้างกลุ่ม")
    System(msg, "Messenger App", "ระบบแชท 1:1 และกลุ่ม พร้อมรูปภาพ/Sticker/Unsend")
    System_Ext(smtp, "Email Service (optional)", "ส่งอีเมลรีเซ็ตรหัสผ่าน (ขยายในอนาคต)")
    Rel(user, msg, "ใช้งานผ่านเว็บเบราว์เซอร์", "HTTPS / WebSocket")
    Rel(msg, smtp, "ส่งอีเมล", "SMTP")
```

## Level 2 – Container

```mermaid
C4Container
    title Container Diagram – Messenger App
    Person(user, "ผู้ใช้", "ใช้งานผ่านเบราว์เซอร์")

    System_Boundary(sys, "Messenger App") {
        Container(spa, "Web SPA", "React + TypeScript (Vite)", "Presentation Layer: UI แชท, เพื่อน, กลุ่ม")
        Container(api, "Messenger API", "ASP.NET Core 8 Web API + SignalR", "Domain Layer: REST API, business rules, realtime hub, JWT auth")
        Container(dal, "Data Access", "EF Core 8 + Npgsql", "Data Access Layer: DbContext, Repositories, Migrations (อยู่ใน process เดียวกับ API)")
        ContainerDb(db, "Database", "PostgreSQL 16", "Users, Friendships, Conversations, Messages, Stickers")
        Container(files, "File Storage", "Docker volume / local disk", "รูปโปรไฟล์ และรูปในแชท")
    }

    Rel(user, spa, "เปิดใช้งาน", "HTTPS")
    Rel(spa, api, "เรียก REST API (JSON)", "HTTPS + JWT")
    Rel(spa, api, "รับ event realtime", "WebSocket (SignalR)")
    Rel(api, dal, "เรียกใช้", "C# method call")
    Rel(dal, db, "อ่าน/เขียน", "SQL over TCP 5432")
    Rel(api, files, "บันทึก/อ่าน/ลบไฟล์", "File I/O")
```

## Level 3 – Component (Messenger API)

```mermaid
flowchart TB
    SPA[Web SPA]

    subgraph API["Messenger API (ASP.NET Core 8)"]
        direction TB
        subgraph PRES["Presentation Boundary (API)"]
            AuthC[AuthController]
            UserC[UsersController]
            FriendC[FriendsController]
            ConvC[ConversationsController]
            MsgC[MessagesController]
            FileC[FilesController]
            StickerC[StickersController]
            Hub[ChatHub - SignalR]
            MW[Middleware: JWT Auth,<br/>Exception Handler, Validation]
        end
        subgraph DOM["Domain Layer (Services + Entities)"]
            AuthS[AuthService]
            UserS[UserService]
            FriendS[FriendService]
            ConvS[ConversationService]
            MsgS[MessageService]
            FileS[FileStorageService]
            Notif[IRealtimeNotifier]
            Ent[(Domain Entities<br/>& Business Rules)]
        end
        subgraph DAL["Data Access Layer (EF Core)"]
            Ctx[MessengerDbContext]
            Repo[Repositories<br/>User / Friendship / Conversation / Message]
        end
    end

    DB[(PostgreSQL)]
    FS[(File Storage)]

    SPA -->|REST| AuthC & UserC & FriendC & ConvC & MsgC & FileC & StickerC
    SPA <-->|WebSocket| Hub
    AuthC --> AuthS
    UserC --> UserS
    FriendC --> FriendS
    ConvC --> ConvS
    MsgC --> MsgS
    FileC --> FileS
    StickerC --> MsgS
    Hub --> ConvS
    MsgS --> Notif
    FriendS --> Notif
    ConvS --> Notif
    Notif -.implemented by.-> Hub
    AuthS & UserS & FriendS & ConvS & MsgS --> Repo
    AuthS & UserS & FriendS & ConvS & MsgS --- Ent
    Repo --> Ctx --> DB
    FileS --> FS
```

### หน้าที่ของแต่ละ Component

| Component | หน้าที่ |
| :--- | :--- |
| Controllers | รับ HTTP, map DTO, เรียก Service, คืน status code (ไม่มี business logic) |
| ChatHub | จัดการ WebSocket: join group ของห้อง/ผู้ใช้, typing, presence |
| IRealtimeNotifier | Abstraction ให้ Domain ส่ง event โดยไม่ผูกกับ SignalR (ลด coupling, ทดสอบง่าย) |
| Services | Business rules ทั้งหมด (เช็คเป็นเพื่อน, สิทธิ์ในกลุ่ม, กฎ unsend ฯลฯ) |
| Repositories + DbContext | Query/Persist ด้วย EF, Migration, ไม่มี business rule |
| FileStorageService | ตรวจชนิดไฟล์/ขนาด, ตั้งชื่อไฟล์ด้วย GUID, บันทึก/ลบ |

## Deployment

```mermaid
flowchart LR
    Browser[Browser<br/>React SPA]
    subgraph Host["Docker Compose Host"]
        Nginx["nginx (web)<br/>serve SPA + reverse proxy<br/>/api, /hubs"]
        Api["messenger-api<br/>ASP.NET Core :8080"]
        Pg[("postgres:16<br/>:5432")]
        Vol[/"volume: uploads"/]
        PgVol[/"volume: pgdata"/]
    end
    Browser -->|HTTPS 443/80| Nginx
    Nginx -->|"/api /hubs (WebSocket upgrade)"| Api
    Api -->|Npgsql| Pg
    Api --- Vol
    Pg --- PgVol
```

หมายเหตุ: ถ้าต้องการ scale API หลาย instance ให้เพิ่ม Redis backplane สำหรับ SignalR (นอกขอบเขตโจทย์)
