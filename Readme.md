# MyQrCodeScanner v1.52-Fork

> 基于 \[1357310795/QrCodeScanner](https://github.com/1357310795/QrCodeScanner) 的增强分支。
> 上游基础版本为 a093462f45bd57aa82bdd92afa3e74a4c030d73c，主窗口版本号显示为 v1.52-Fork。

本项目保留上游的四种使用模式和三种识别引擎，并在此基础上增强了扫描枪窗口行为、后处理规则和正则输入体验。

## 上游功能

* 截图识别
* 摄像头识别
* 本地图片识别
* 扫描枪模式
* Zbar、ZXing、微信 OpenCV 三种识别引擎
* 多码同扫
* Material Design 主题与暗黑模式

## 本 Fork 新增功能

### 1\. 扫描枪窗口关闭与最小化

“简易条码扫描”窗口右上角按钮的行为已调整：

* 点击 **×**：关闭扫描枪窗口，停止摄像头工作，并返回主窗口；
* 点击 **−**：读取“功能设置 → 最小化时隐藏到托盘区”：

  * 开启时最小化到托盘；
  * 关闭时正常最小化到任务栏；
* 删除窗口顶部关于 × / − 的说明文字。

### 2\. 全局后处理规则

“设置 → 后处理设置”改为全局规则模式：

* 最上方有四个模块复选框：

  * 截屏识别
  * 摄像头识别
  * 本地图片识别
  * 扫描枪
* 所有勾选模块共享同一份规则列表，不再为每个模块分别保存规则；
* 支持“新增规则”和“删除规则”，规则编号自动更新；
* 每条规则头部显示“规则 N:”，冒号后直接是名称/描述输入框，不另设标签；
* 名称只作备注，不参与匹配，保存为 ruleN\_name。

规则按照列表顺序执行：

1. 规则从上到下依次尝试；
2. 第一条匹配成功的规则立即返回结果并停止；
3. 全部规则未匹配时返回原始扫描文本；
4. 未勾选的模块只扫描，不执行正则；
5. 空正则、非法正则、match 不存在、捕获组不存在时跳过该规则，继续下一条。

### 3\. 正则输入与索引增强

* 正则输入框启用系统输入法，可以直接输入中文正则；
* 正则符号（括号、竖线、星号等）建议切换到英文输入状态输入；
* match 和捕获组输入框禁用输入法，只接受数字和 /；
* match 下标从 **0** 开始：

  * 0 表示第 1 个匹配；
  * 1 表示第 2 个匹配；
  * 留空等同于 0；
* 捕获组索引从 **0** 开始：

  * 0 表示整个匹配；
  * 1 表示第 1 个捕获组；
* match 和捕获组都支持用 / 分隔多个值。

## 多 match / 捕获组输出

match 和捕获组按位置配对：

&#x20;   match: 0/2
    group: 1/2


表示：

&#x20;   第 1 个 match 的第 1 个捕获组
    +
    第 3 个 match 的第 2 个捕获组


两段结果按顺序无缝拼接，不添加分隔符。

### 示例

&#x20;   正则：\\d,(\\d{10}),(\\d{8}),\\d
    match：0/2
    group：1/2


输入：

&#x20;   1,1234567890,20251231,9;2,2345678901,20260101,8;3,3456789012,20260202,7


输出：

&#x20;   123456789020260202


### 约束

* match 和捕获组的项数必须相同；
* 点击“应用”时项数不一致会弹出错误提示，并忽略本次应用；
* 任意一个 match 不存在，或对应捕获组不存在，整条规则失败，继续尝试下一条规则；
* 捕获组存在但没有参与匹配时，该段输出空字符串。

## 设置保存位置

后处理设置保存在：

&#x20;   %APPDATA%\\MyQrCodeScanner\\Settings.ini


对应 \[postprocess] 段，版本 2：

&#x20;   \[postprocess]
    version=2
    module\_screenshot=False
    module\_camera=False
    module\_imagefile=False
    module\_scannergun=True
    rules=1
    rule0\_name=订单号
    rule0\_pattern=^ORDER: \*(\[0-9]+)$
    rule0\_match=0
    rule0\_group=1


旧版按模块保存的 version 1 设置会在首次加载时迁移为全局规则。

## 构建

环境要求：

* Windows
* .NET Framework 4.7.2 或更高版本
* 可构建 net472-windows 的 .NET SDK
* WPF

  dotnet restore MyQrCodeScanner.sln
dotnet build MyQrCodeScanner.sln -c Release

生成目录：

&#x20;   MyQrCodeScanner/bin/Release/net472-windows/


运行时需要同时保留：

* MyQrCodeScanner.exe
* 同目录 DLL
* MyQrCodeScanner.exe.config
* data/ 文件夹

不能只复制 exe。

## 已知限制

* 当前版本基于上游 v1.52 的代码，尚未包含剪贴板写入异常的重试保护。扫描成功后如果剪贴板正被其他程序占用，可能抛出 COMException 并退出。
* /g 固定展示，不提供 i、m 等额外 flags。
* 正则使用 .NET System.Text.RegularExpressions.Regex，常用 JavaScript 写法兼容，但不是浏览器/Node 的 JS 引擎。
* 所有规则均未匹配时保留原文，避免误清空扫描结果。

## 大模型辅助开发

本 Fork 的代码修改、文档整理和测试用例在 **DeepSeek 大模型（deepseek-flash）** 辅助下完成。所有改动均经过本地编译、测试和人工审查；该模型仅用于开发阶段，不参与程序运行时的识别或后处理。

## 上游与致谢

* 上游项目：[1357310795/QrCodeScanner](https://github.com/1357310795/QrCodeScanner)
* 上游基础提交：a093462f45bd57aa82bdd92afa3e74a4c030d73c
* 本 Fork 的修改说明另见 [FORK.md](FORK.md)

感谢上游作者及所有依赖项目。

