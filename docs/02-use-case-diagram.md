# 02 – Use Case Diagram

```mermaid
flowchart LR
    Guest([Guest])
    User([User])
    Admin([Group Owner / Admin])
    Sys([System])

    User -. สืบทอด .-> Guest
    Admin -. สืบทอดสิทธิ์จาก .-> User

    subgraph SYS[Messenger App]
        direction TB
        subgraph AUTH[บัญชีผู้ใช้]
            UC01((สมัครสมาชิก))
            UC02((เข้าสู่ระบบ / ออกจากระบบ))
            UC11((แก้ไขโปรไฟล์))
        end
        subgraph FRIEND[เพื่อน]
            UC03((ค้นหาผู้ใช้ & ส่งคำขอเพื่อน))
            UC04((ตอบรับ / ปฏิเสธคำขอ))
            UC05((ดู / ลบเพื่อน))
            UC10((บล็อกผู้ใช้))
        end
        subgraph CHAT[การสนทนา]
            UC07((ดูห้องแชท & ประวัติ))
            UC06((ส่งข้อความ))
            UC06a((ส่งรูปภาพ))
            UC06b((ส่ง Sticker))
            UC08((Unsend ข้อความ))
            UC12((สถานะออนไลน์ / กำลังพิมพ์))
            UC14((Read receipt))
        end
        subgraph GROUP[กลุ่ม]
            UC09a((สร้างกลุ่ม))
            UC09b((ออกจากกลุ่ม))
            UC09c((เพิ่ม / ลบสมาชิก))
            UC09d((เปลี่ยนชื่อ-รูปกลุ่ม / แต่งตั้ง Admin))
        end
        UC15((ลบไฟล์ของข้อความที่ Unsend))
        UC16((ล้าง Refresh token หมดอายุ))
    end

    Guest --- UC01
    Guest --- UC02
    User --- UC11
    User --- UC03
    User --- UC04
    User --- UC05
    User --- UC10
    User --- UC07
    User --- UC06
    User --- UC08
    User --- UC12
    User --- UC09a
    User --- UC09b
    Admin --- UC09c
    Admin --- UC09d

    UC06a -. extend .-> UC06
    UC06b -. extend .-> UC06
    UC06 -. include .-> UC03x[/ตรวจสิทธิ์ สมาชิก+เพื่อน/]
    UC07 -. include .-> UC14
    UC08 -. include .-> UC15
    UC03 -. include .-> UC03x
    Sys --- UC15
    Sys --- UC16
```

## ตารางความสัมพันธ์

| Use case | Actor | ความสัมพันธ์ |
| :--- | :--- | :--- |
| ส่งรูปภาพ, ส่ง Sticker | User | «extend» ส่งข้อความ |
| ส่งข้อความ | User | «include» ตรวจสิทธิ์ (เป็นสมาชิกห้อง + เป็นเพื่อน/ไม่ถูกบล็อก) |
| ดูห้องแชท & ประวัติ | User | «include» Read receipt |
| Unsend ข้อความ | User | «include» ลบไฟล์แนบ (System) |
| เพิ่ม/ลบสมาชิก, แต่งตั้ง Admin | Group Owner/Admin | ต้องมีบทบาท Owner/Admin ในกลุ่มนั้น |
