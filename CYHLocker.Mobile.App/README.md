# CYH Local SMS Mobile App (.NET MAUI)

**CYH Local SMS** ek modern, offline-capable mobile text messaging app hai jo direct phone ke **SIM card** ka upyog karke SMS bhejne aur receive karne ke liye banaya gaya hai.

---

## 🌟 Pramukh Features (Key Features)

1. **Direct SIM Messaging (Offline / No Internet Required)**:
   - Phone ke SIM card (SIM 1 / SIM 2) ke madhyam se direct SMS dispatch karta hai.
   - Android ke native `Android.Telephony.SmsManager` ka upyog karta hai.

2. **Real-time SMS Receiver**:
   - `SmsBroadcastReceiver` ke jariye incoming SMS messages ko real-time me receive karta hai aur live UI update karta hai bina reload kiye.

3. **Smart SMS Segment & Character Calculator**:
   - **GSM 7-bit standard**: 160 characters per single SMS; agar 160 se bada ho to 153 chars ke multi-part SMS me count karta hai.
   - **Unicode / Hindi standard**: Hindi ya emoji aane par automatically detect karke 70 characters / segment ka live counter dikhata hai.

4. **Conversations & Chat Threads**:
   - Contacts ke anusaar conversation grouping aur avatar generation.
   - Modern chat bubbles (Incoming left, Outgoing right with timestamp & status tick).
   - Fast search filter (by phone number, contact name, or message text).

5. **Quick Templates (Canned Responses)**:
   - Raste me driving karte samay ya meeting me jaldi reply bhejne ke liye 1-tap quick canned responses.
   - Custom templates add karne ki suvidha.

6. **Desktop & Testing Mode**:
   - Windows desktop par bhi chal sakta hai (test mode with simulator button `+ Test SMS` to simulate incoming texts without physical SIM).

---

## 🏗️ Architecture & Project Structure

- **Models**:
  - `SmsMessageItem.cs`: SMS data model (Sender, Recipient, Body, Timestamp, DeliveryStatus, SimSlot).
  - `SmsConversationItem.cs`: Grouped conversation thread with avatar initials & colors.
  - `SmsSendResult.cs`: Result envelope with status & error messages.
  - `SimCardInfo.cs`: Dual-SIM slot information.
- **Services**:
  - `ISmsService.cs`: Unified SMS interface.
  - `DefaultSmsService.cs`: Android native `SmsManager` + Cross-platform fallback implementation.
  - `ISmsStorageService.cs` & `SmsStorageService.cs`: Persistent local storage (`cyh_local_sms_database.json`).
- **Platforms/Android**:
  - `AndroidManifest.xml`: Permissions (`SEND_SMS`, `RECEIVE_SMS`, `READ_SMS`, `READ_PHONE_STATE`, `READ_CONTACTS`).
  - `SmsBroadcastReceiver.cs`: `android.provider.Telephony.SMS_RECEIVED` broadcast listener.
- **Helpers**:
  - `SmsSegmentCalculator.cs`: GSM 7-bit vs Unicode detection and character/segment estimation.
- **ViewModels (MVVM)**:
  - `ConversationsViewModel.cs`: Chats list, search filtering, delete, and test triggers.
  - `ChatViewModel.cs`: Active chat thread, auto-scrolling, quick templates, and sending.
  - `ComposeViewModel.cs`: Direct SMS compose to any number.
  - `SmsSettingsViewModel.cs`: SIM info, permission checks, stats (Total sent, received).
- **Views**:
  - `ConversationsPage.xaml`: Main threads view.
  - `ChatPage.xaml`: Chat bubble conversation view.
  - `ComposePage.xaml`: Dedicated new message composer.
  - `SettingsPage.xaml`: SIM card status & permissions center.

---

## 🚀 Run & Build Instructions

### Windows par run karne ke liye:
```bash
dotnet build CYHLocker.Mobile.App/CYHLocker.Mobile.App.csproj -f net10.0-windows10.0.19041.0
```
Executable location:
`CYHLocker.Mobile.App/bin/Debug/net10.0-windows10.0.19041.0/win-x64/CYHLocker.Mobile.App.exe`

### Android Mobile / Emulator par run karne ke liye:
```bash
dotnet build CYHLocker.Mobile.App/CYHLocker.Mobile.App.csproj -f net10.0-android
```
Android device par install karne ke liye:
```bash
dotnet build CYHLocker.Mobile.App/CYHLocker.Mobile.App.csproj -t:Run -f net10.0-android
```
