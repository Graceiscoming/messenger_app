# 07 – Sequence Diagrams

## 1. เข้าสู่ระบบและเชื่อม Realtime
```mermaid
sequenceDiagram
    actor U as User
    participant SPA
    participant API as AuthController/AuthService
    participant DB as PostgreSQL
    participant Hub as ChatHub
    U->>SPA: กรอก username/password
    SPA->>API: POST /auth/login
    API->>DB: SELECT user by username
    API->>API: verify hash (PasswordHasher)
    API->>DB: INSERT refresh_token
    API-->>SPA: 200 {accessToken, refreshToken}
    SPA->>Hub: connect /hubs/chat?access_token=JWT
    Hub->>DB: โหลดห้องที่เป็นสมาชิก
    Hub->>Hub: AddToGroup user:{id}, conv:{id}...
    Hub-->>SPA: connected (+ PresenceChanged ไปยังเพื่อน)
```

## 2. ส่งคำขอเป็นเพื่อนและตอบรับ
```mermaid
sequenceDiagram
    actor A as Alice
    actor B as Bob
    participant API as Friends/Conversation Service
    participant DB
    participant RT as SignalR
    A->>API: POST /friends/requests {username: bob}
    API->>DB: ตรวจ block / friendship เดิม
    API->>DB: INSERT friendship (Pending)
    API->>RT: NotifyFriendEvent(Bob, Received)
    RT-->>B: FriendRequestReceived
    B->>API: POST /friends/requests/{id}/accept
    API->>DB: UPDATE status=Accepted
    API->>DB: INSERT conversation Direct (ถ้ายังไม่มี, direct_key)
    API->>RT: NotifyFriendEvent(Alice, Accepted) + ConversationChanged
    RT-->>A: FriendRequestAccepted
    API-->>B: 200
```

## 3. ส่งข้อความ (รวมรูปภาพ)
```mermaid
sequenceDiagram
    actor A as Alice
    participant SPA
    participant API as Files/Messages Controller
    participant S as MessageService
    participant DB
    participant RT as IRealtimeNotifier (SignalR)
    actor B as Bob (ออนไลน์)
    opt ส่งรูปภาพ
        A->>SPA: เลือกรูป
        SPA->>API: POST /files/images (multipart)
        API->>API: ตรวจชนิด/ขนาด/magic bytes, บันทึกไฟล์
        API->>DB: INSERT attachment
        API-->>SPA: {attachmentId}
    end
    SPA->>API: POST /conversations/{id}/messages
    API->>S: SendAsync(me, convId, dto)
    S->>DB: โหลด conversation + members
    S->>S: ตรวจเป็นสมาชิก, (1:1) เป็นเพื่อน & ไม่ถูกบล็อก, validate
    S->>DB: INSERT message #59; UPDATE conversation.last_message_*
    S->>RT: NotifyMessageReceived(memberIds, dto)
    RT-->>B: MessageReceived
    S-->>API: MessageDto
    API-->>SPA: 201 MessageDto
```

## 4. Unsend ข้อความ
```mermaid
sequenceDiagram
    actor A as Alice (ผู้ส่ง)
    participant API as MessagesController
    participant S as MessageService
    participant F as FileStorageService
    participant DB
    participant RT as SignalR
    actor B as Bob
    A->>API: DELETE /messages/{id}
    API->>S: UnsendAsync(me, id)
    S->>DB: SELECT message
    alt ไม่ใช่เจ้าของ
        S-->>API: Forbidden → 403
    else เลยเวลา 24 ชม. / unsent แล้ว
        S-->>API: Conflict → 409
    else ผ่านเงื่อนไข
        S->>S: message.Unsend() (UnsentAt=now, Content=null)
        S->>DB: UPDATE message
        opt มีรูปแนบ
            S->>F: DeleteAsync(attachmentId)
        end
        S->>RT: NotifyMessageUnsent(convId, id)
        RT-->>B: MessageUnsent
        API-->>A: 204
    end
```

## 5. สร้างกลุ่มและเพิ่มสมาชิก
```mermaid
sequenceDiagram
    actor O as Owner
    participant API as ConversationService
    participant F as FriendService
    participant DB
    participant RT as SignalR
    O->>API: POST /conversations/group {name, memberIds}
    API->>F: ตรวจว่าทุกคนเป็นเพื่อนของ Owner
    API->>DB: INSERT conversation(Group) + members (Owner, Member...)
    API->>DB: INSERT message(System "Owner สร้างกลุ่ม")
    API->>RT: ConversationChanged → สมาชิกทุกคน (เข้า group conv:{id})
    API-->>O: 201 ConversationDto
    O->>API: POST /conversations/{id}/members {userIds}
    API->>API: CanManageMembers(me)?
    API->>DB: INSERT members + System message
    API->>RT: ConversationChanged
```

## 6. อ่านข้อความ (Read receipt)
```mermaid
sequenceDiagram
    actor B as Bob
    participant API as ConversationService
    participant DB
    participant RT as SignalR
    actor A as Alice
    B->>API: POST /conversations/{id}/read {messageId}
    API->>DB: UPDATE member.last_read_message_id = max(เดิม, messageId)
    API->>RT: NotifyRead(convId, Bob, messageId)
    RT-->>A: MessageRead
```
