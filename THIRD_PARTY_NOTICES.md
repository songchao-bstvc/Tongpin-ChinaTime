# 第三方开源组件

同频由 Chao Song 维护。本项目的原创代码与文档采用 [MIT License](LICENSE)。以下第三方代码保留原作者版权及许可，不因本项目发行而改变归属。

| 组件 | 来源与固定版本 | 本项目中的位置 | 许可证 |
| --- | --- | --- | --- |
| DesktopClock | [danielchalmers/DesktopClock](https://github.com/danielchalmers/DesktopClock/tree/4e8d5d4c2e3b48fd2c043ebd16ffbd1f830c179a)，commit `4e8d5d4c2e3b48fd2c043ebd16ffbd1f830c179a` | `src/DesktopClock/DateTimeUtil.cs`、`SystemClockTimer.cs`，原样复用 | [MIT · Daniel Chalmers](licenses/DesktopClock-MIT.txt) |
| lunar-csharp | [6tail/lunar-csharp](https://github.com/6tail/lunar-csharp/tree/9f00a107d896c477aba20ada25b3becc8de9007f)，v1.6.8，commit `9f00a107d896c477aba20ada25b3becc8de9007f` | `vendor/lunar-csharp/`，原样保留 C# 源文件 | [MIT · 6tail](licenses/lunar-csharp-MIT.txt) |

农历、节气、传统节日及法定休班资料来自 lunar-csharp。v1.6.8 的法定放假与调休资料覆盖到 2026 年，后续年份需更新。

任务栏定位、透明外观、日历交互、实时卡片、私人备忘、提醒与加密备份由本项目实现。本项目是独立应用，不是上述项目的官方发行版。

分发二进制或修改版时，请同时保留根目录 LICENSE 与 `licenses/` 内两份许可。运行时依赖 Windows / .NET Framework；这些系统组件不随本仓库重新分发。
