# 离线测试修复验证记录（2026-10-02）

用户需求：按离线测试审查提出的顺序修复漏检、诊断异常边界和测试入口。

## 范围与约束

- 工作区 HEAD：`9a0d4c4d67e0ce117d90f63320fdad0cb5dc4f08`。接手时已有 AGENTS、INDEX、PAIPOS、RouteAOrderAdapter、RouteAControl、Program 的未提交改动；这些改动保留，本报告不将整个 HEAD 差分归为本轮补丁。
- 本轮生产代码修改仅为 `RouteAControl.Release` 的两个诊断回调分别捕获异常。未新增 setter、战术或控制状态，未改变正常释放顺序及退出原因。
- 其余修改为测试、静态检查、统一运行入口、测试指南及该指南登记表条目的摘要/更新时间。未部署、未提交，现网和运行期状态不由本报告更新。

## 修复内容

- 调用顺序检查先确认两个调用存在；Mission 调用限定到 `OnMissionTick`，同时核对编译产物。缺失、反序的调用有反向自检。
- 编译产物检查核对 BeginSession 的两条探针接线、适配器失败选中后的早退，以及三条 Harmony prefix 参数名与本机游戏 setter 签名的一致性。检查不依赖手写参数映射表，也不依赖反编译局部变量名。
- 回归测试覆盖两条释放回调正常、分别失败、同时失败、关闭采集、重复释放、Mission 切换、交付超时及真实日志输出。取消无条件 `Check(true)` 计数，保留既有真实回归。
- `OfflineTests/RunOfflineTests.ps1` 统一执行生产 Release 重新构建、测试 Debug 重新构建、回归、静态契约。构建警告视为错误；失败退出 1；阶段状态/计数/日志和源码、生产 DLL、测试 EXE、游戏 DLL 的 SHA 记录在 result.json。构建产物目录不参与源码摘要。
- 完整离线验收以退出码 0 和 `OFFLINE SUITE PASS` 为准；独立运行旧 C# 回归的 `ALL PASS` 不替代完整入口。

## 自检证据

正常版本完整入口退出 0：生产与测试构建均零错误、零警告；回归 **1394 PASS / 0 FAIL**；静态契约 **30 PASS**。

- 完整报告：`OfflineTests/bin/verification/6f6dd52d9ef94d16957cfebf52034d35/result.json`
- 源码输入 SHA256：`81E62603D399692C85AB0329DBD1DBA9FDC33E11D77006FBE26821059463D40F`
- 生产 DLL SHA256：`84E1DEB5CA744995F22666CC52C635C62F6E2788E0B7726F66C2609E07C3E551`
- 本机游戏 DLL SHA256：`19387F31557FF840D14F378F6BBDF1D58FCFF406AB9FF2294DFBB3E49A50B87E`

故意破坏均在临时副本中进行；每个副本的生产与测试构建都成功，随后由对应检查失败，完整入口退出 1。

| 故意破坏 | 检出阶段 |
|---|---|
| 注释掉实时归属探针接线 | 回归；单独运行编译产物检查也失败 |
| 修改 MovementPrefix 参数名，映射表不变 | 静态契约 |
| 删除 Mission 中 Route A 预占位调用 | 静态契约 |
| 恢复修复前无异常隔离的释放代码 | 回归 |
| 保留 TrySelect 调用但删除失败早退 | 静态契约 |

反向验证明细：`C:\Users\Administrator\AppData\Local\Temp\CombatBrain-repair-detection-8e57f5ef79284da6840028fee70f269f\detection-results.json`。该文件所指向的各副本保存失败阶段日志与报告。完成上述验证后，只追加了构建目录的显式排除条件；最终正常版已重新完整运行通过。

## 未验证与独立质检交接

本轮属于开发自检，独立质检尚未执行。按照 PAIPOS Lite，由用户手动切换质检 Harness；接手者应读取项目 bootstrap，而非将本报告视为现行规则。

独立质检重点：诊断异常是否始终不阻断释放/恢复及超时暂停；结构检查是否对应真实编译产物、遇到缺失或改错接线能失败；统一入口是否完整执行、错误退出及证据绑定是否可信。可使用上述正常报告与五组反向样本复核，不必重建历史审计材料。

编译产物结构检查不证明任意控制流或游戏内行为。Harmony 实际挂载、引擎调度、游戏内运行期验收均未执行；报告明确为 `Runtime=NOT_RUN`。项目冻结令及部署授权边界继续适用。

本轮知识源：repo AGENTS、知识 INDEX、ACTIVE 登记的 PAIPOS_LITE v0.1、CURRENT_STATE、测试指南，以及 manager 的 Prompt 题头元数据规范。未加载历史 handoff、旧 CONTEXT 或 Master Spec 全文。
