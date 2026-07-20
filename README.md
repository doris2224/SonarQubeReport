# SonarQubeReport

以 .NET 8.0 撰寫的主控台工具，連線 SonarQube Server，將指定專案的分析結果匯出成 `.xlsx` 報表，
頁籤配置比照 `SonarQubeReportExample.xlsx`：**All / Issues / Unconfirmed / Security Hotspots**。

## 專案結構

```
SonarQubeReport/
├─ SonarQubeReport.csproj      # net8.0 主控台專案，相依套件：ClosedXML（產出 .xlsx）
├─ appsettings.json            # SonarQube 連線設定（Base URL、分頁大小、狀態篩選規則）
├─ Program.cs                  # 進入點，解析參數並串接下載/產表流程
├─ AppConfig.cs                # 讀取 appsettings.json
├─ Models/
│  ├─ IssueModels.cs           # 對應 /api/issues/search 回應
│  └─ HotspotModels.cs         # 對應 /api/hotspots/search、/api/rules/show 回應
├─ Services/
│  ├─ SonarQubeClient.cs       # 呼叫 SonarQube Web API（含分頁、認證、規則查詢快取）
│  └─ ExcelReportBuilder.cs    # 用 ClosedXML 寫出 4 個頁籤
└─ run-report.cmd              # cmd 呼叫範例
```

## 環境需求

- .NET 8.0 SDK（開發/建置用）
- 可存取目標 SonarQube Server 的網路
- 一組具備「檢視問題 / 執行分析」權限的 SonarQube User Token

## 建置

```bash
cd SonarQubeReport
dotnet restore
dotnet build -c Release
```

## 發佈成單一 exe 檔（對應需求「6. .NET8.0 產出專案名稱為 SonarQubeReport 的 EXE 檔案」）

```bash
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o .\publish
```

發佈後 `.\publish\` 資料夾內會有 `SonarQubeReport.exe` 與 `appsettings.json`，
兩者需放在同一個資料夾（exe 啟動時會讀取同目錄下的 `appsettings.json`）。

若目標主機沒有安裝 .NET Runtime，請保留 `--self-contained true`（上面指令已包含）；
若目標主機已安裝 .NET 8 Runtime，可移除 `--self-contained true` 來縮小發佈體積。

## 設定 appsettings.json

```json
{
  "SonarQube": {
    "BaseUrl": "http://1.2.3.4:1234",
    "PageSize": 500,
    "HotspotStatus": "TO_REVIEW"
  }
}
```

| 欄位 | 說明 |
|---|---|
| `BaseUrl` | SonarQube Server 位址，對應需求「4. SonarQubeURL：1.2.3.4:1234」 |
| `PageSize` | 每次 API 呼叫抓取的筆數，上限 500（SonarQube API 限制） |
| `HotspotStatus` | Security Hotspots 篩選狀態，`TO_REVIEW`＝待審查，`ALL`＝不篩選 |

> `UnconfirmedStatuses` 設定已移除：Issues / Unconfirmed 的分流改成直接向 SonarQube 送兩次
> `/api/issues/search`（`resolved=false` / `resolved=true`），不再由本地設定檔控制，詳見下方
> 「報表內容與範例檔的對應關係」。

## 使用方式

對應需求「7. 可用 cmd 呼叫」：

```cmd
SonarQubeReport.exe "%sonarQubeToken%" "%sonarQubeProjects%" "%reportPath%"
```

- `sonarQubeToken`：SonarQube User Token
- `sonarQubeProjects`：專案 Key，多專案以逗號分隔（例如 `Test` 或 `Test1,Test2`）
- `reportPath`：輸出路徑。給完整檔名（`.xlsx` 結尾）就直接存成該檔案；給資料夾路徑則自動產生檔名
  `<專案>_SonarQubeReport_<yyyyMMddHHmmss>.xlsx`

範例（對應需求「3. 專案名稱：Test」「5. SonarQubeURL專案：http://1.2.3.4:1234/dashboard?id=Test」）：

```cmd
SonarQubeReport.exe "squ_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" "Test" "C:\Reports\Test.xlsx"
```

執行時主控台會列出對應專案的 SonarQube 儀表板連結（`{BaseUrl}/dashboard?id={project}`），方便交叉核對。

也可參考 `run-report.cmd`，把三個變數改成你的實際值後直接雙擊執行。

## 報表內容與範例檔的對應關係

以下欄位與分頁規則已對照 `sonar-cnes-report-5.0.4.jar` 的實際輸出（原始碼：
[cnescatlab/sonar-cnes-report](https://github.com/cnescatlab/sonar-cnes-report)）調整，兩邊產出應一致：

| 頁籤 | 資料來源 | 說明 |
|---|---|---|
| **All** | `/api/issues/search?resolved=false`（原始 JSON 全欄位） | 32 欄，1:1 對應 sonar-cnes-report 的「All」頁籤欄位順序，含 SonarQube v26.7 新增的 `internalTags`／`fromSonarQubeUpdate`／`linkedTicketStatus` |
| **Issues** | 同上（`resolved=false`） | Rule / Message / Type / Severity / Language / File / Line / Effort / Status / Comments |
| **Unconfirmed** | `/api/issues/search?resolved=true` | 欄位同「Issues」；`resolved=true` 代表已被標記解決（fixed／wontfix／false-positive 等），不是用 `status` 欄位在本地端分類 |
| **Security Hotspots** | `/api/hotspots/search` + `/api/hotspots/show`（逐筆取留言）+ `/api/rules/show`（補 Severity／Language） | Rule / Message / Category / Priority / Severity / Language / File / Line / Status / Resolution / Comments |

### 已對齊 sonar-cnes-report 的關鍵邏輯

1. **Issues / Unconfirmed 的分流規則**：改成對 `/api/issues/search` 送**兩次**請求，分別帶
   `resolved=false`（→ Issues／All）與 `resolved=true`（→ Unconfirmed），在 SonarQube 伺服器端
   篩選，不是抓一次全部再用本地端的 `status` 欄位判斷。`status`（OPEN/CONFIRMED/CLOSED...）跟
   `resolved`（是否已標記解決）是兩個不同語意的欄位，用 `status` 分類會跟 sonar-cnes-report 兜不起來。
   同時「All」頁籤不再是 Issues＋Unconfirmed 的聯集，而是跟「Issues」共用同一份 `resolved=false`
   資料（只是輸出全部原始欄位）——這點也是對照 sonar-cnes-report 原始碼確認的行為。
2. **Language 欄位**：改用 `/api/issues/search` 回應中的 **`rules[]`**（因為請求帶了
   `additionalFields=_all`）、以 issue 的 `rule` 欄位對應 `rules[].key`，取 `langName` 填入，
   不是用 `components[].language` 反查。Security Hotspots 則沿用原本的做法：以該筆 hotspot
   對應規則的 `/api/rules/show` 結果（`langName`／`lang`）補上，此查詢同時也補上 Hotspot 的
   `Severity`（hotspot 本身只有 `vulnerabilityProbability`，沒有傳統的 severity）。
3. **Security Hotspots 的 Category 欄位**：`securityCategory` 是內部代碼（如 `auth`），已比照
   sonar-cnes-report 的對照表轉成顯示名稱（如 `Authentication`），完整清單見
   `ExcelReportBuilder.SecurityCategoryNames`。
4. **Security Hotspots 的 Comments 欄位**：改為對每一筆 hotspot 呼叫
   `/api/hotspots/show?hotspot=<key>` 取得留言串（`comment[]`），會增加 API 呼叫次數
   （hotspot 數量大時執行時間會拉長，屬正常現象）。
5. **「All」頁籤中的巢狀欄位**（`flows`、`impacts`、`textRange`、`comments`、`tags` 等）：
   本程式輸出標準 **JSON** 字串（例如 `[{"locations":[...]}]`），跟 sonar-cnes-report 用類似 Java
   Map `toString()` 的格式（例如 `[{locations=[...]}]`）不同，但資訊完全對等，只是序列化格式
   不同；如需逐字比對兩邊檔案，比對前建議先正規化這幾欄再比。

## 關於 SonarQube 的「10000 筆」查詢上限

SonarQube 的 `/api/issues/search` 有一個**寫死在伺服器端**的限制：不管怎麼分頁，`p * ps` 最多只能查到第
10000 筆，超過就會回 `400 Bad Request`，訊息類似：

```
{"errors":[{"msg":"Can return only the first 10000 results. 10500th result asked."}]}
```

這不是分頁邏輯寫錯，而是 SonarQube API 本身的限制（sonar-cnes-report 等其他工具在同樣情況下也會遇到相同錯誤，
見 [cnescatlab/sonar-cnes-report#354](https://github.com/cnescatlab/sonar-cnes-report/issues/354)）。

本工具的因應方式：當某一次查詢（`resolved=true` 或 `resolved=false`）的 issue 總數超過一萬筆時，
`SonarQubeClient.SearchIssuesAsync` 會自動改成
依「issue 類型 (`types`) → 嚴重度 (`severities`) → 建立日期區間 (`createdAfter`/`createdBefore`，二分切分)」
逐層遞迴查詢，確保每一段查詢的結果數都在一萬筆以內，最後再合併成完整清單。對使用者來說完全透明，不需要額外設定；
專案 issue 數量越多，執行時間會等比例拉長（因為需要更多次 API 呼叫）。

若同樣的錯誤出現在 Security Hotspots（`/api/hotspots/search`）上，代表單一專案的 hotspot 數也超過了一萬筆，
可以用同樣的切分邏輯（依 `status`/建立日期）處理，目前程式尚未對 hotspots 做這層防護（一般專案的 hotspot
數遠低於 issue 數，故優先處理 issues），有需要的話可以參考 `SonarQubeClient.cs` 裡 issues 的作法比照擴充。

## 關於 Excel 單一儲存格 32,767 字元上限

Excel 檔案格式本身規定：**每個儲存格最多只能放 32,767 個字元**，超過就沒辦法寫入。少數 issue（尤其是
`flows` 欄位——牽涉多個程式碼位置的追蹤鏈——轉成 JSON 字串後可能非常長）可能會超過這個上限。

本工具在寫入任何儲存格前都會先做長度檢查（`ExcelReportBuilder.Clamp`），超過上限的內容會被截斷，
並在結尾加上「…[內容過長，已截斷；完整內容請至 SonarQube 網頁查看]」的提示文字，確保不會因為少數
幾筆超長資料讓整份報表產出失敗。若需要看完整內容，建議直接到 SonarQube 網頁上查看該筆 issue。

## 認證方式

預設採用 HTTP Basic Auth（帳號＝User Token、密碼留空），新舊版 SonarQube 皆相容。
若貴公司環境已改用 Bearer Token 驗證，請打開 `Services/SonarQubeClient.cs`，
依註解切換成 `Authorization: Bearer <token>` 即可。

## 已知待確認事項

修正前比對兩邊 xlsx，Security Hotspots 頁籤筆數仍有落差（例如一次比對是 52 筆 vs 58 筆），
但目前讀原始碼並未找到會造成筆數差異的邏輯（分頁、`status` 篩選寫法都相同）。比較可能的原因是
兩次報表不是在完全相同的時間點對同一個 SonarQube 分析結果跑的（hotspot 審查狀態在兩次執行之間
變動）。建議修正部署後，兩個工具在同一時間點對同一次分析結果重新各跑一次、再比對一次筆數，
若落差仍然存在再進一步排查。

## 常見問題

- **回傳 401**：確認 Token 是否正確、是否有到期，以及是否具備該專案的檢視權限。
- **回傳 404 / 空白報表**：確認 `sonarQubeProjects` 的專案 Key 是否正確（區分大小寫），
  以及 `appsettings.json` 的 `BaseUrl` 是否正確（含 `http://` 或 `https://`）。
- **公司內網無法連線 nuget.org**：建置環境需能存取 NuGet（或改用內部 NuGet 鏡像）以還原 `ClosedXML` 套件。
