# 离线验证补洞记录（2026-10-06）

基于 GitHub main `1f5f675cbbaddb7f1ebfdfb2d23aa27cb804b759`。用户授权云端修补并提交。

## 改动

- 生产 C#、游戏命令、状态机及部署脚本均未修改。
- 新增无 npm 依赖的 Node.js 检查器，由云端源码检查和 Windows 生产 DLL 反编译检查共用；完整入口新增 Node.js 20+ 依赖。
- Mission 接线要求两个调用均为直接语句，顺序正确，且中间仅含现有权重缓存清理与性能分段语句；错误分支、重复调用或提前退出不再只凭文字顺序通过。
- 四个候选 CalculateCurrentOrder 检查完整简单方法体：仅规定的准备命令与最终 false 标志；额外 helper、条件 false 或后续 true 都失败。
- 检查实际 MCM bool 属性读写目标与 value 转发、模式属性双路径同步、Dropdown 使用的数组及 A/B/C/D/E 五项顺序。生产 DLL 的 MCM 类在临时目录旁放置安装的依赖供反编译，不加载 UI、不改生产输出。
- 95 个已有测试经 RunTest 执行，真实完成后输出组状态与检查次数。独立 RequiredTestGroups.json 同时约束静态注册和回归日志；漏跑、删除、重复、失败、空组不能由其他 PASS 抵消。所有原有断言保留。
- 完整入口新增检查器自测、回归完整性阶段；JS/JSON 纳入源码输入 SHA。原静态检查保留，并收窄部分过强的通过文案。
- RunMutationChecks.ps1 复制当前工作区源输入到独立临时 Git 快照，排除旧 bin/obj；先验正常副本，再验十个单独破坏副本。两次构建成功、预期阶段失败、退出 1 均为检出条件；环境缺依赖或编译失败不算检出。

## 验收与证据边界

云端执行：35 项检查器自测全部通过，当前源码契约通过，diff 检查通过。自测覆盖正常源码/反编译形态、错误分支、候选标志、MCM 桥接/数组、回归完成记录，以及 PowerShell 5.1 UTF-16LE 与 UTF-8 日志编码。

对实际源码的五个独立临时副本运行同一检查器 CLI：错误预占位分支、候选 false 后 true、观测属性恒写 false、删除 E 选项、遗漏释放诊断测试组，均退出 1；正常副本退出 0。这是实际源码变体的源码检查证据，不是编译变体或完整套件证据。

云端缺少 .NET SDK、PowerShell、ilspycmd 与本机游戏 DLL，未执行 C# 编译、真实 DLL 反编译、完整 Windows 套件或完整变异脚本。没有部署，也没有将 CURRENT_STATE 的已验证基线推进。本提交是测试机制补丁，不构成运行期或完整离线质检通过证据。

新检查器只支持这里明确批准的简单代码形态，遇到等价但不同的反编译输出会显式失败，需在查看实际产物后补充正反样本；不能退回仅按子串存在判定。更一般的分支/调用图、Harmony 挂载及引擎行为仍需对应证据。

本机完整验收：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "OfflineTests\RunOfflineTests.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "OfflineTests\RunMutationChecks.ps1"
```

前者必须退出 0，所有阶段 PASS，报告 Runtime=NOT_RUN。后者要求正常副本 PASS，十个破坏副本各自在预期阶段失败，最终脚本退出 0。缺少依赖一律失败，不跳过、不把云端检查冒充完整 PASS。

## 约束

已读取 repo AGENTS、INDEX、PAIPOS Lite v0.1、CURRENT_STATE、CONSTITUTION 与测试指南。外置生命周期登记表位于仓库外 Windows 路径，云端不可访问，记录 BOOTSTRAP_FAILURE（登记项未核验）；按用户显式云端修补/提交授权继续，仅修改验证与说明文件。没有修改宪法、冻结令或部署许可，也没有自动调度其他 Harness。
