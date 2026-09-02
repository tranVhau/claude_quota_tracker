# Claude Code Quota Tracker

Windows 11 tray app theo dõi % quota Claude Code (session 5h / week 7d) và thời gian reset. Xem `docs/plan.md` (nếu bạn copy file plan gốc vào đây) để biết đầy đủ bối cảnh thiết kế.

## Yêu cầu

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download) với workload **.NET Desktop Development** (WPF + Windows Forms) — nếu dùng Visual Studio 2022, chọn workload ".NET desktop development" khi cài.
- Claude Code **v2.1.251 trở lên**, đăng nhập tài khoản **Pro/Max** (bắt buộc để có `rate_limits` trong statusLine — xem giải thích trong plan doc, mục 0).

⚠️ **Không build được trên Linux/macOS** — WPF và Windows Forms chỉ chạy trên Windows. Project này được viết trên môi trường không phải Windows nên **chưa được compile/test thật** — hãy mở bằng Visual Studio 2022 (hoặc `dotnet build`) trên máy Windows trước khi chạy, và báo lại nếu gặp lỗi biên dịch để mình sửa.

## Build & chạy

```powershell
cd ClaudeQuotaTracker
dotnet build
dotnet run --project src/ClaudeQuotaTracker
```

Hoặc mở `ClaudeQuotaTracker.sln` bằng Visual Studio 2022 và nhấn F5.

## Kết nối với Claude Code (đăng ký statusLine)

Cách 1 — **trong app**: mở Settings → tab Advanced → "Register statusLine". App sẽ tự ghi (và backup file cũ) vào `~/.claude/settings.json`.

Cách 2 — **thủ công**: thêm vào `~/.claude/settings.json`:

```json
{
  "statusLine": {
    "type": "command",
    "command": "powershell -NoProfile -ExecutionPolicy Bypass -File \"C:/path/to/ClaudeQuotaTracker/bin/Debug/net8.0-windows/bridge/statusline-bridge.ps1\"",
    "refreshInterval": 300
  }
}
```

Sau khi đăng ký, mở 1 session `claude` bất kỳ và gõ 1 prompt — `%AppData%\ClaudeQuotaTracker\snapshot.json` sẽ được tạo, tray icon sẽ tự cập nhật.

## Test nhanh không cần chờ Claude Code

Tự tạo file mẫu để xem UI hoạt động, không cần gọi API thật:

```powershell
$dir = "$env:APPDATA\ClaudeQuotaTracker"
New-Item -ItemType Directory -Force -Path $dir | Out-Null
@{
    updatedAt = (Get-Date).ToUniversalTime().ToString('o')
    fiveHour  = @{ usedPercentage = 72; resetsAt = (Get-Date).AddHours(2).ToUniversalTime().ToString('o') }
    sevenDay  = @{ usedPercentage = 41; resetsAt = (Get-Date).AddDays(3).ToUniversalTime().ToString('o') }
} | ConvertTo-Json | Set-Content "$dir\snapshot.json"
```

## Cấu trúc project

```
ClaudeQuotaTracker/
├─ ClaudeQuotaTracker.sln
├─ src/ClaudeQuotaTracker/
│  ├─ App.xaml(.cs)              điểm khởi động, ShutdownMode=OnExplicitShutdown
│  ├─ AppSettings.cs             model cấu hình, load/save %AppData%\ClaudeQuotaTracker\config.json
│  ├─ SnapshotModel.cs           model khớp snapshot.json
│  ├─ SnapshotStore.cs           đọc + FileSystemWatcher (debounce 250ms)
│  ├─ IconRenderer.cs            vẽ icon dual-ring bằng System.Drawing
│  ├─ TrayIconManager.cs         NotifyIcon, context menu, mở popup
│  ├─ TriggerScheduler.cs        công thức tính giờ trigger tối ưu + gọi schtasks.exe
│  ├─ AutoStartManager.cs        registry Run key
│  ├─ PopupWindow.xaml(.cs)      popup click trái
│  └─ SettingsWindow.xaml(.cs)   dialog Settings (General/Advanced/Triggers)
└─ bridge/
   └─ statusline-bridge.ps1      script đăng ký làm statusLine của Claude Code
```

## Việc còn để ngỏ (TODO)

- `TriggerScheduler.RunSchtasks` hiện nuốt lỗi im lặng — nên hiển thị lỗi thật (ExitCode/stderr) lên tab Advanced thay vì chỉ log.
- `IconRenderer` chỉ vẽ 1 kích thước 32×32 — Windows sẽ tự scale, nhưng vẽ thêm bản 16×16 sẽ nét hơn ở taskbar mặc định.
- `PopupWindow.OpenClaudeCode_Click` giả định có Windows Terminal (`wt.exe`); đã có fallback PowerShell thường nhưng chưa test đường dẫn nào phổ biến hơn trên máy thật.
- Chưa có unit test cho `TriggerScheduler.Calculate` — nên thêm test cho case `S mod 5 == 0` và ca qua đêm trước khi tin tưởng hoàn toàn.
- Đóng gói installer (MSI/NSIS) chưa làm — hiện chỉ có hướng dẫn `dotnet build`.
