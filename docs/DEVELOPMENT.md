# 开发与验证

## 技术结构

Windows x64 · C# · WPF · .NET Framework 4.8。源码无需 NuGet 下载，第三方代码按固定 commit 放在仓库中。

| 路径 | 用途 |
| --- | --- |
| `src/ClockWindow.cs`、`Native.cs` | 透明时钟、Windows 任务栏与屏幕定位 |
| `src/CalendarWindow.cs`、`Dialogs.cs` | 中文日历、备忘与提醒 |
| `src/Theme.cs`、`SettingsWindow.cs` | 自适应主题与设置预览 |
| `src/LiveWindow.cs`、`LiveData.cs` | 实时卡片与下一条提醒 |
| `src/Core.cs` | 中国时区、重复规则、存储与备份 |
| `src/SelfTest.cs` | 24 项隔离自检 |
| `src/DesktopClock/`、`vendor/lunar-csharp/` | 固定版本 MIT 上游模块 |

## 构建

在 Windows x64 上安装 PowerShell 7.2 或更高版本，并确保 .NET Framework 4.8 可用。`scripts/build.ps1` 使用 PowerShell 目录内的 Roslyn 程序集，引用 Windows Framework64 下的系统程序集。不是 Windows PowerShell 5.1。

```powershell
pwsh -NoProfile -File scripts/build.ps1
pwsh -NoProfile -File scripts/test.ps1
pwsh -NoProfile -File scripts/package.ps1
```

编译器随 PowerShell 版本变化；不同构建环境的二进制散列可能不同。发行包以发行页的 SHA-256 为准，不宣称字节级可复现构建。GitHub Actions 在 Windows Server 2022 runner 上执行同一流程；这只验证构建与核心逻辑，不表示桌面体验支持 Windows Server。

## 自检与界面验证

24 项自检覆盖：中国跨日、美国夏令时、中国节日与 ISO 周数、月末/闰年/工作周重复、提醒到期和遗漏合并、延后跨日、完成/回收站过滤、DPAPI 存取及自动备份、密码错误与篡改拒绝、备份合并、旧数据显示默认值、下一条提醒、屏幕边界、无效数值、纪念日与主题颜色可变性。

测试会在 `artifacts/` 下新建独立目录，不接触正式备忘；输出不含私人内容的 `artifacts/self-test.json`。不要把整个测试数据目录作为公开附件。

需要交互验证时：

```powershell
.\build\ChinaTime.exe --data-dir "$env:TEMP\Tongpin-Demo" --qa-windows --capture-ui
```

该命令使用独立演示配置；`--qa-windows` 让测试窗口可被桌面工具发现，`--capture-ui` 导出可见窗口内容。测试日历/设置中按 F12 可重新导出。截图不包含 Windows DWM 合成背景。不要在演示配置中切换真实系统登录启动，以免覆盖正式启动项。

## v2.0.0 验证记录与边界

- 实机：Windows 11 23H2，4K 主屏，200% 缩放。
- 本地核心自检 24/24 通过；CI 结果请查看 Actions。
- 实机检查了透明像素、深浅色切换、设置取消回退、位置预设、拖动后重启恢复、卡片避让和任务栏副行轮换。
- 登录启动的注册表目标已经核对，尚未通过重启整台电脑验证。
- 尚未完整实测：Windows 10 / ARM64、多屏热插拔、各类第三方任务栏、休眠唤醒与全屏应用组合、跨账户迁移。

固定 UTC 输入用于跨日与夏令时测试，不需要修改电脑时间。请勿为了验收而改变其他应用依赖的系统时区。
