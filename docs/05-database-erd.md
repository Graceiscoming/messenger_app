# 05 – ER Diagram และ Database Schema (PostgreSQL)

```mermaid
erDiagram
    USERS ||--o{ REFRESH_TOKENS : has
    USERS ||--o{ FRIENDSHIPS : "requester"
    USERS ||--o{ FRIENDSHIPS : "addressee"
    USERS ||--o{ USER_BLOCKS : "blocker"
    USERS ||--o{ USER_BLOCKS : "blocked"
    USERS ||--o{ CONVERSATION_MEMBERS : joins
    CONVERSATIONS ||--|{ CONVERSATION_MEMBERS : has
    CONVERSATIONS ||--o{ MESSAGES : contains
    USERS ||--o{ MESSAGES : sends
    MESSAGES }o--o| ATTACHMENTS : has
    MESSAGES }o--o| STICKERS : uses
    MESSAGES }o--o| MESSAGES : "reply_to"
    STICKER_PACKS ||--|{ STICKERS : contains
    USERS ||--o{ ATTACHMENTS : uploads

    USERS {
        uuid id PK
        varchar username UK "lower-case unique"
        varchar display_name
        varchar password_hash
        varchar status_message
        varchar avatar_path
        timestamptz created_at
        timestamptz last_seen_at
        int failed_login_count
        timestamptz lockout_until
    }
    REFRESH_TOKENS {
        uuid id PK
        uuid user_id FK
        varchar token_hash UK
        timestamptz expires_at
        timestamptz revoked_at
    }
    FRIENDSHIPS {
        uuid id PK
        uuid requester_id FK
        uuid addressee_id FK
        smallint status "0=Pending 1=Accepted"
        timestamptz created_at
        timestamptz responded_at
    }
    USER_BLOCKS {
        uuid blocker_id PK,FK
        uuid blocked_id PK,FK
        timestamptz created_at
    }
    CONVERSATIONS {
        uuid id PK
        smallint type "0=Direct 1=Group"
        varchar name
        varchar avatar_path
        varchar direct_key UK "minId_maxId (Direct only)"
        uuid created_by_id FK
        timestamptz created_at
        bigint last_message_id
        timestamptz last_message_at
    }
    CONVERSATION_MEMBERS {
        uuid conversation_id PK,FK
        uuid user_id PK,FK
        smallint role "0=Member 1=Admin 2=Owner"
        timestamptz joined_at
        timestamptz left_at
        bigint last_read_message_id
    }
    MESSAGES {
        bigint id PK "identity"
        uuid conversation_id FK
        uuid sender_id FK
        smallint type "0=Text 1=Image 2=Sticker 3=System"
        varchar content
        uuid attachment_id FK
        int sticker_id FK
        bigint reply_to_message_id FK
        timestamptz created_at
        timestamptz unsent_at
    }
    ATTACHMENTS {
        uuid id PK
        uuid uploader_id FK
        varchar stored_file_name
        varchar original_file_name
        varchar content_type
        bigint size_bytes
        int width
        int height
        timestamptz uploaded_at
    }
    STICKER_PACKS {
        int id PK
        varchar name
        varchar thumbnail_path
    }
    STICKERS {
        int id PK
        int pack_id FK
        varchar code UK
        varchar image_path
    }
```

## Constraints และ Index

| ตาราง | Constraint / Index | วัตถุประสงค์ |
| :--- | :--- | :--- |
| users | `UNIQUE (lower(username))` | username ไม่ซ้ำ |
| friendships | `UNIQUE (LEAST(requester_id,addressee_id), GREATEST(requester_id,addressee_id))`; `CHECK (requester_id <> addressee_id)` | หนึ่งคู่ = หนึ่ง record |
| friendships | index `(addressee_id, status)` | ดึงคำขอที่รอตอบ |
| conversations | `UNIQUE (direct_key)` (nullable) | ห้อง 1:1 ไม่ซ้ำ |
| conversation_members | index `(user_id)` | ดึงห้องของผู้ใช้ |
| messages | index `(conversation_id, id DESC)` | cursor pagination ประวัติแชท |
| messages | `CHECK (type <> 0 OR content IS NOT NULL)` | ข้อความ text ต้องมีเนื้อหา |
| refresh_tokens | index `(user_id)`, `UNIQUE (token_hash)` | |

## Query ที่สำคัญ

```sql
-- ประวัติข้อความ (cursor)
SELECT * FROM messages
WHERE conversation_id = @cid AND (@before IS NULL OR id < @before)
ORDER BY id DESC LIMIT 30;

-- unread count ต่อห้อง
SELECT COUNT(*) FROM messages m
WHERE m.conversation_id = @cid AND m.id > COALESCE(@lastRead, 0)
  AND m.sender_id <> @me AND m.unsent_at IS NULL;
```

## Seed Data
- `sticker_packs` / `stickers`: seed อย่างน้อย 1 pack (8–16 รูป) ผ่าน `HasData()` ใน EF
