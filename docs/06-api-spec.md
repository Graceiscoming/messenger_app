# 06 – API Specification

Base URL: `/api/v1` · Format: JSON · Auth: `Authorization: Bearer <accessToken>` (ยกเว้น register/login/refresh)
Error format (RFC 7807 ProblemDetails): `{ "type", "title", "status", "detail" }`

## REST Endpoints

### Auth
| Method | Path | คำอธิบาย | Success |
| :--- | :--- | :--- | :--- |
| POST | `/auth/register` | สมัครสมาชิก | 201 AuthResult |
| POST | `/auth/login` | เข้าสู่ระบบ | 200 AuthResult |
| POST | `/auth/refresh` | ต่ออายุ token | 200 AuthResult |
| POST | `/auth/logout` | เพิกถอน refresh token | 204 |

### Users
| Method | Path | คำอธิบาย |
| :--- | :--- | :--- |
| GET | `/users/me` | โปรไฟล์ตนเอง |
| PUT | `/users/me` | แก้ displayName, statusMessage |
| PUT | `/users/me/avatar` | อัปโหลดรูปโปรไฟล์ (multipart) |
| GET | `/users/search?username=abc` | ค้นหา (prefix match, ไม่รวมตนเอง/ผู้ที่บล็อกเรา) |
| POST | `/users/{id}/block` / DELETE | บล็อก / เลิกบล็อก |

### Friends
| Method | Path | คำอธิบาย |
| :--- | :--- | :--- |
| GET | `/friends` | รายชื่อเพื่อน (พร้อม isOnline, conversationId) |
| DELETE | `/friends/{friendId}` | ลบเพื่อน |
| GET | `/friends/requests` | `{ incoming: [], outgoing: [] }` |
| POST | `/friends/requests` | body `{ "username": "bob" }` ส่งคำขอ |
| POST | `/friends/requests/{id}/accept` | ตอบรับ |
| POST | `/friends/requests/{id}/decline` | ปฏิเสธ (ผู้รับ) |
| DELETE | `/friends/requests/{id}` | ยกเลิกคำขอ (ผู้ส่ง) |

### Conversations
| Method | Path | คำอธิบาย |
| :--- | :--- | :--- |
| GET | `/conversations` | รายการห้อง (lastMessage, unreadCount) |
| POST | `/conversations/direct` | `{ friendId }` → เปิด/สร้างห้อง 1:1 |
| POST | `/conversations/group` | `{ name, memberIds[] }` |
| GET | `/conversations/{id}` | รายละเอียดห้อง + สมาชิก |
| PUT | `/conversations/{id}` | เปลี่ยนชื่อ/รูปกลุ่ม (Admin+) |
| POST | `/conversations/{id}/members` | `{ userIds[] }` เพิ่มสมาชิก (Admin+) |
| DELETE | `/conversations/{id}/members/{userId}` | ลบสมาชิก (Admin+) |
| PUT | `/conversations/{id}/members/{userId}/role` | `{ role }` (Owner) |
| POST | `/conversations/{id}/leave` | ออกจากกลุ่ม |
| POST | `/conversations/{id}/read` | `{ messageId }` อัปเดตอ่านแล้ว |

### Messages
| Method | Path | คำอธิบาย |
| :--- | :--- | :--- |
| GET | `/conversations/{id}/messages?beforeId=&limit=30` | ประวัติ (ใหม่→เก่า) |
| POST | `/conversations/{id}/messages` | ส่งข้อความ |
| DELETE | `/messages/{id}` | Unsend (เจ้าของเท่านั้น) |

ตัวอย่าง body ส่งข้อความ
```json
{ "type": "Text", "content": "สวัสดี", "replyToMessageId": null }
{ "type": "Image", "attachmentId": "3f2c...-guid", "content": "caption (optional)" }
{ "type": "Sticker", "stickerId": 5 }
```

### Files / Stickers
| Method | Path | คำอธิบาย |
| :--- | :--- | :--- |
| POST | `/files/images` | multipart `file` → `{ attachmentId, url, width, height }` |
| GET | `/files/{attachmentId}` | ดึงไฟล์ (ต้องเป็นสมาชิกห้องที่มีข้อความนั้น หรือเจ้าของ) |
| GET | `/stickers` | รายการ sticker pack ทั้งหมด |

### Notes (เพิ่มภายหลัง — ดู decisions.md)
| Method | Path | คำอธิบาย |
| :--- | :--- | :--- |
| GET | `/notes` | โน้ตที่ยังไม่หมดอายุของตัวเอง (มาก่อน) และของเพื่อน → `NoteDto[]` |
| PUT | `/notes/me` | `{ content }` 1–60 ตัวอักษร ลง/แทนที่โน้ตของตัวเอง (อยู่ 24 ชม.) → `NoteDto` |
| DELETE | `/notes/me` | ลบโน้ตของตัวเอง → 204 |
| POST | `/notes/{id}/replies` | `{ content }` ตอบกลับโน้ตของเพื่อน = ส่งข้อความในแชท 1:1 → 201 `MessageDto` (มี `noteReply`) |

`NoteDto = { id, user: UserDto, content, createdAt, expiresAt }` · `MessageDto.noteReply = { content } | null`

### HTTP Status ที่ใช้
200/201/204 สำเร็จ · 400 validation · 401 ไม่ได้ล็อกอิน · 403 ไม่มีสิทธิ์ (ไม่ใช่เพื่อน/สมาชิก/เจ้าของ) · 404 ไม่พบ · 409 ซ้ำ/ขัดสถานะ (เช่น เลยเวลา unsend) · 413 ไฟล์ใหญ่เกิน

## SignalR Hub: `/hubs/chat`
เชื่อมต่อด้วย `?access_token=<jwt>` เมื่อ connect เซิร์ฟเวอร์เพิ่ม connection เข้า group `user:{userId}` และ `conv:{id}` ของทุกห้องที่เป็นสมาชิก

**Server → Client**
| Event | Payload | เมื่อใด |
| :--- | :--- | :--- |
| `MessageReceived` | MessageDto | มีข้อความใหม่ |
| `MessageUnsent` | `{ conversationId, messageId }` | มีการ unsend |
| `MessageRead` | `{ conversationId, userId, messageId }` | มีคนอ่าน |
| `ConversationChanged` | ConversationDto | สร้างห้อง/สมาชิกเปลี่ยน/ชื่อเปลี่ยน |
| `FriendRequestReceived` | FriendshipDto | มีคำขอใหม่ |
| `FriendRequestAccepted` | FriendshipDto | คำขอถูกยอมรับ |
| `FriendRemoved` | `{ userId }` | ถูกลบเพื่อน |
| `PresenceChanged` | `{ userId, isOnline }` | ออนไลน์/ออฟไลน์ (ส่งให้เพื่อน) |
| `UserTyping` | `{ conversationId, userId }` | กำลังพิมพ์ |
| `NotesChanged` | `{ userId }` | เพื่อน (หรือตัวเองอีกเครื่อง) ลง/ลบโน้ต → client ดึง `GET /notes` ใหม่ |

**Client → Server:** `Typing(conversationId)`

## Validation สรุป
| ฟิลด์ | กฎ |
| :--- | :--- |
| username | `^[a-zA-Z0-9_]{3,20}$` |
| password | ≥ 8 ตัว, มีตัวอักษรและตัวเลข |
| displayName | 1–50 ตัว |
| message content | 1–2000 ตัว (ไม่ใช่ whitespace ล้วน) |
| group name | 1–50 ตัว |
| image | jpeg/png/webp/gif, ≤ 5 MB, ตรวจ magic bytes ไม่เชื่อ extension อย่างเดียว |
