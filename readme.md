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
