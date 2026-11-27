# เอกสารออกแบบระบบ Messenger App (CP353 โจทย์ A)

เอกสารชุดนี้ออกแบบระบบแชทแบบ Line/Discord ให้ครอบคลุม Requirement ขั้นต่ำ (ส่ง-รับข้อความ, Friend list) และฟีเจอร์เพิ่มเติม (รูปภาพ/Sticker, กลุ่มสนทนา, Unsend) โดยใช้ Technology ที่กำหนด

| ไฟล์ | เนื้อหา |
| :--- | :--- |
| [01-requirements.md](01-requirements.md) | Requirement, Use case specification (ข้อความอธิบาย), Business rules, Non-functional |
| [02-use-case-diagram.md](02-use-case-diagram.md) | Use Case Diagram |
| [03-architecture-c4.md](03-architecture-c4.md) | C4 Model: Context, Container, Component, Deployment |
| [04-class-diagram.md](04-class-diagram.md) | Class Diagram: Domain model, Service layer, Data access |
| [05-database-erd.md](05-database-erd.md) | ER Diagram และ schema PostgreSQL |
| [06-api-spec.md](06-api-spec.md) | REST API + SignalR events |
| [07-sequence-diagrams.md](07-sequence-diagrams.md) | Sequence ของ flow สำคัญ |
| [08-implementation-guide.md](08-implementation-guide.md) | โครงสร้างโปรเจกต์, Config, Docker, แผนพัฒนา, Test, รายงานการใช้ AI |

> Diagram ทั้งหมดเขียนด้วย **Mermaid** — แสดงผลอัตโนมัติบน GitHub และ VS Code (ติดตั้ง extension "Markdown Preview Mermaid Support")

## สรุปการตัดสินใจออกแบบ (Design Decisions)

| หัวข้อ | การตัดสินใจ | เหตุผล |
| :--- | :--- | :--- |
| Presentation | React + TypeScript (Vite) SPA | อิสระตามโจทย์, ทำ realtime UI ง่าย |
| Domain | ASP.NET Core 8 Web API (REST) + SignalR | ตามโจทย์; SignalR สำหรับ push ข้อความทันที |
| Data Access | EF Core 8 + Npgsql (Code-First Migrations) | ตามโจทย์ |
| Database | PostgreSQL 16 | ตามโจทย์ |
| Auth | JWT access token + refresh token, รหัสผ่านแฮชด้วย ASP.NET `PasswordHasher` (PBKDF2) | มาตรฐาน, stateless |
| Conversation | โมเดลเดียว `Conversation` รองรับทั้ง Direct และ Group | ลดความซ้ำซ้อน, Unsend/รูป/Sticker ใช้ร่วมกัน |
| Unsend | Soft delete (`UnsentAt`) และซ่อนเนื้อหาตอนส่งกลับ | ตรวจสอบย้อนหลังได้, แจ้ง "ข้อความถูกยกเลิก" |
| รูปภาพ | เก็บไฟล์ใน storage (volume) เก็บ metadata ใน DB | ไม่ทำให้ DB บวม |
| Sticker | Sticker pack ที่ระบบ seed ไว้ (ภาพ static) | ทำง่าย ขยายได้ |
