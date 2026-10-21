# MyQrCodeScanner v1.52-Fork.2

Fork of [1357310795/QrCodeScanner](https://github.com/1357310795/QrCodeScanner), maintained at
[JingShenFeiZhai/QrCodeScanner](https://github.com/JingShenFeiZhai/QrCodeScanner), based on upstream
commit a093462f45bd57aa82bdd92afa3e74a4c030d73c. The version label in the main window is v1.52-Fork.2.

## Changes in this fork

### 1. Scanner-gun window close / minimize

- Clicking the title-bar **×** of **简易条码扫描** now closes scanner mode, stops the camera worker,
  and returns to the main window. It no longer hides the window in the tray.
- Clicking the title-bar **−** now reads the existing **功能设置 → 最小化时隐藏到托盘区** option:
  - enabled: minimize to the notification tray;
  - disabled: normal minimize to the taskbar.

### 2. Global post-processing rules

A new **后处理设置** tab is available in Settings.

- At the top, four checkboxes select which modules use regex matching:
  - 截屏识别 / Screen capture
  - 摄像头识别 / Camera
  - 本地图片识别 / Local image
  - 扫描枪 / Scanner gun
- Below them is one shared, ordered list of regex rules. The same list is used by every checked
  module. Rules are not duplicated per module.
- **新增规则 / Add rule** appends a rule; **删除规则 / Delete rule** removes that row. Rule numbers
  are updated automatically.
- Each rule header is "Rule N:" followed by an optional free-text name/description box. The name
  is only a label and does not affect matching.
- The pattern is entered without the delimiters: the UI displays the fixed leading / and trailing
  /g around the text box.
- Rules are tried from top to bottom against the original scan text. The **first rule that matches**
  returns its selected value immediately and later rules are skipped. If every rule fails, the
  original scan text is returned.

The settings page is designed for high-DPI displays and 200% Windows scaling: fields have explicit
labels, fixed row heights and vertical spacing. The regex pattern field enables the system IME,
so Chinese characters can be part of a pattern; regex punctuation should still be typed in the
English input state. The match and capture-group fields keep the IME disabled because they only
accept digits. Each pattern row also has a **Paste** button that fills the field from the clipboard;
Ctrl+V and first-click focus are handled explicitly.

If at least one rule actually matches, the post-scan prompt changes to the regex-match prompt. If
all rules fail and the original text is used, the normal scan prompt is kept.

Rule semantics:

- Match indexes are 0-based. Empty means 0 and selects the first match. Multiple indexes are
  separated by / (for example 0/2).
- Capture-group indexes are 0-based with 0 meaning the whole match. Multiple indexes are separated
  by / (for example 1/2).
- Match and capture-group lists are paired by position. They must contain the same number of items;
  Apply rejects a rule with mismatched counts.
- The selected group values are concatenated in list order with no separator.
- Empty patterns are skipped. Invalid patterns, missing matches and missing groups fail that rule,
  allowing the next rule to be tried.
- If the selected capture group did not participate in the match, the output is an empty string.
- Multiple codes returned by a multi-code scan are processed individually.
- Matching uses .NET System.Text.RegularExpressions.Regex, which accepts normal JavaScript-style
  patterns including capture groups. The fixed /g is represented by the global match collection and
  the 0-based match index.
- Modules, rule order, rule names and rules are persisted under the [postprocess] section of Settings.ini.
  Version 1 per-mode settings are migrated to the shared list on first load.

Examples:

| Input | Pattern | Match | Group | Output |
|---|---|---|---|---|
| ORDER: 12345 | ^ORDER: *([0-9]+)$ | empty | 1 | 12345 |
| A1 B2 C3 | ([A-Z])([0-9]) | 1 | 0 | B2 |
| x1 y2 z3 | [a-z]([0-9]) | 2 | 1 | 3 |
| 1,1234567890,20251231,9;2,...;3,3456789012,20260202,7 | \d,(\d{10}),(\d{8}),\d | 0/2 | 1/2 | 123456789020260202 |

## Build

Requirements: Windows, .NET Framework 4.7.2+ runtime, and a .NET SDK that can build net472
(this fork was built with .NET SDK 8.0.426).

    dotnet restore MyQrCodeScanner.sln
    dotnet build MyQrCodeScanner.sln -c Release

The runnable application is produced in:

MyQrCodeScanner/bin/Release/net472-windows/

The .exe needs the DLLs and data/ folder next to it; copy the whole output folder, not only the
executable.
