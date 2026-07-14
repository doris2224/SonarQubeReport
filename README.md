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
    "UnconfirmedStatuses": [ "CLOSED", "RESOLVED" ],
    "HotspotStatus": "TO_REVIEW"
  }
}
```

| 欄位 | 說明 |
|---|---|
| `BaseUrl` | SonarQube Server 位址，對應需求「4. SonarQubeURL：1.2.3.4:1234」 |
| `PageSize` | 每次 API 呼叫抓取的筆數，上限 500（SonarQube API 限制） |
| `UnconfirmedStatuses` | issue 的 `status` 落在此清單者會被分類到「Unconfirmed」頁籤，其餘進「Issues」頁籤（見下方「分頁規則」說明） |
| `HotspotStatus` | Security Hotspots 篩選狀態，`TO_REVIEW`＝待審查，`ALL`＝不篩選 |

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

| 頁籤 | 資料來源 | 說明 |
|---|---|---|
| **All** | `/api/issues/search`（不篩選狀態） | 29 欄，1:1 對應範例檔「All」頁籤欄位順序，含完整原始資料 |
| **Issues** | 同上，`status` 不在 `UnconfirmedStatuses` 清單 | Rule / Message / Type / Severity / Language / File / Line / Effort / Status / Comments |
| **Unconfirmed** | 同上，`status` 在 `UnconfirmedStatuses` 清單 | 欄位同「Issues」 |
| **Security Hotspots** | `/api/hotspots/search` + `/api/rules/show` | Rule / Message / Category / Priority / Severity / Language / File / Line / Status / Resolution / Comments |

### 已記錄的假設（請依貴公司實際流程調整）

因為範例檔本身沒有附上產生規則，以下判斷為根據欄位內容推論所得的合理假設，
若與貴公司實際定義不同，調整 `appsettings.json` 或程式碼中對應邏輯即可：

1. **Issues / Unconfirmed 的分流規則**：以 issue 的 `status` 欄位是否落在 `UnconfirmedStatuses`
   （預設 `CLOSED`、`RESOLVED`）判斷，而非另外呼叫一次帶篩選條件的 API——這樣「All」頁籤才能保證是
   真正未經篩選的完整資料，「Issues」「Unconfirmed」則是同一份資料的子集合，不會有兩邊資料兜不起來的問題。
2. **Language 欄位**：SonarQube 的 issue 物件本身不一定含語言欄位，改用 `/api/issues/search` 回應中的
   `components[].language` 反查；Security Hotspots 則因 API 未回傳語言，改用該筆 hotspot 對應規則的
   `/api/rules/show` 結果（`langName`／`lang`）補上，此查詢同時也用來補上 Hotspot 的 `Severity`
   （hotspot 本身只有 `vulnerabilityProbability`，沒有傳統的 severity）。
3. **Security Hotspots 的 Comments 欄位**：SonarQube 的 `/api/hotspots/search` 不會回傳留言串，
   此欄位固定留空以維持與範例檔相同的欄位配置；如需留言內容，需改呼叫
   `/api/hotspots/show?hotspot=<key>` 逐筆查詢（會大幅增加 API 呼叫次數，故未預設啟用）。
4. **「All」頁籤中的巢狀欄位**（`flows`、`impacts`、`textRange`、`comments`、`tags` 等）：
   範例檔是類似 Java Map `toString()` 的格式（例如 `[{locations=[...]}]`），本程式改以標準 **JSON**
   字串輸出（例如 `[{"locations":[...]}]`），資訊完全對等、只是序列化格式不同，可讀性更好、也方便
   之後用程式二次解析。

## 關於 SonarQube 的「10000 筆」查詢上限

SonarQube 的 `/api/issues/search` 有一個**寫死在伺服器端**的限制：不管怎麼分頁，`p * ps` 最多只能查到第
10000 筆，超過就會回 `400 Bad Request`，訊息類似：

```
{"errors":[{"msg":"Can return only the first 10000 results. 10500th result asked."}]}
```

這不是分頁邏輯寫錯，而是 SonarQube API 本身的限制（sonar-cnes-report 等其他工具在同樣情況下也會遇到相同錯誤，
見 [cnescatlab/sonar-cnes-report#354](https://github.com/cnescatlab/sonar-cnes-report/issues/354)）。

本工具的因應方式：當某個專案的 issue 總數超過一萬筆時，`SonarQubeClient.SearchAllIssuesAsync` 會自動改成
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

## 常見問題

- **回傳 401**：確認 Token 是否正確、是否有到期，以及是否具備該專案的檢視權限。
- **回傳 404 / 空白報表**：確認 `sonarQubeProjects` 的專案 Key 是否正確（區分大小寫），
  以及 `appsettings.json` 的 `BaseUrl` 是否正確（含 `http://` 或 `https://`）。
- **公司內網無法連線 nuget.org**：建置環境需能存取 NuGet（或改用內部 NuGet 鏡像）以還原 `ClosedXML` 套件。
