# CP353 Project: Messenger App (โจทย์ A)

> **ตัวอย่างอ้างอิง:** Line, Discord

---

## โจทย์และ Requirement

### Requirement ขั้นต่ำ
- ส่ง-รับ Message ระหว่างบุคคลได้
- ระบบ Friend List

### ฟีเจอร์ที่อาจเพิ่มเติม (Optional)
- การส่งรูปภาพ และ/หรือ Sticker
- การสร้างกลุ่มสนทนา (Group Chat)
- การ Unsend ข้อความ
- อื่นๆ แล้วแต่จะออกแบบเพิ่มเติม

---

## Technology ที่กำหนดให้ใช้

| Layer | Technology |
| :--- | :--- |
| **Presentation Layer** | ใช้อะไรทำก็ได้ |
| **Domain Layer** | ASP.NET (REST API) |
| **Data Access Layer** | Entity Framework (EF) |
| **Database** | PostgreSQL |

---

## แนวทางการใช้ AI

- **ควบคุม Design ด้วยตัวเอง:** อนุญาตให้ใช้ AI ช่วยได้ แต่ต้องคุม Design ให้เป็นไปตามต้องการ
- **เข้าใจโค้ดทุกส่วน:** ต้องเข้าใจว่าโค้ดส่วนใดทำหน้าที่อะไร เผื่อกรณีที่ต้องแก้ไขเอง (เนื่องจาก AI อาจเกิดความผิดพลาด / AI Hallucination ได้)
- **รายงานการใช้งาน:** ให้แจ้งด้วยว่าใช้ AI ช่วยในส่วนใดบ้าง และผลลัพธ์ที่ได้เป็นอย่างไร

---

## วิธีรัน

### 1. รันทั้งระบบด้วย Docker Compose
ต้องติดตั้ง Docker Desktop และเปิดให้ทำงานอยู่

```bash
cp .env.example .env        # Windows: copy .env.example .env
```

แก้ค่าใน `.env` ให้ครบ:
- `DB_PASSWORD` รหัสผ่านฐานข้อมูล
- `JWT_KEY` ต้องยาวอย่างน้อย 32 ตัวอักษร (วิธีสร้างอยู่ในไฟล์ `.env.example`)

จากนั้นรัน:

```bash
docker compose up --build
```

เปิดเว็บที่ http://localhost (ฐานข้อมูลและไฟล์อัปโหลดเก็บใน volume `pgdata` และ `uploads`)
หยุดระบบด้วย `docker compose down` (เพิ่ม `-v` เพื่อลบข้อมูลทั้งหมด)

### 2. รันแบบ Local Dev (แก้โค้ดแล้วเห็นผลทันที)
ใช้ Docker รันเฉพาะ PostgreSQL แล้วรัน API กับเว็บด้วยตัวเอง

```powershell
# 1) เริ่มฐานข้อมูลที่ localhost:5432 (PowerShell)
powershell -ExecutionPolicy Bypass -File scripts/dev-db.ps1
# หรือบน Git Bash / macOS / Linux
bash scripts/dev-db.sh
```

สคริปต์ใช้ `DB_PASSWORD` จาก `.env` ถ้ามี ถ้าไม่มีจะใช้ค่าเริ่มต้น `messenger_dev`

```powershell
# 2) API ที่ http://localhost:5080 (terminal แรก)
cd src/Messenger.Api
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=messenger;Username=messenger;Password=messenger_dev"
$env:Jwt__Key = "<ค่า JWT_KEY อย่างน้อย 32 ตัวอักษร>"
dotnet run --urls http://localhost:5080
```

```bash
# 3) เว็บ (terminal ที่สอง)
cd web
npm install
npm run dev
```

> หมายเหตุ: `dev-db` ใช้ container ชื่อ `messenger-db-dev` แยกจาก compose จึงรันพร้อมกันได้ แต่ทั้งคู่ใช้พอร์ต 5432 บนเครื่องเดียวกัน ให้รันเพียงอย่างเดียวในแต่ละครั้ง
> ถ้าเปลี่ยน `DB_PASSWORD` หลังจากสร้าง volume แล้ว ต้องลบ volume เดิมก่อน (`docker volume rm messenger-pgdata-dev`) เพราะรหัสผ่านถูกตั้งครั้งแรกตอนสร้าง
