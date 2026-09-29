# Netch 现代化升级与演进路线规划 (Dev 分支)

本文档记录 Netch 在 `dev` 分支上的全套演进规划，明确各阶段的实施重点、技术架构与交付清单。

---

## 阶段一：Socks5 裸节点链路完善与导入格式补全（当前优先级：最高）

### 1.1 现状与痛点
- 用户主要使用 Socks5 裸节点配合 Redirector（进程模式）或 tun2socks（TUN模式），享受零本地核心开销的极速加速体验。
- **痛点一（严重 Bug）**：`Socks5Util.cs` 中注册的 `TypeName` 为 `"SOCKS"`，但 `ShareLink.cs` 中第 70 行写死为 `ServerHelper.GetUtilByTypeName("Socks5")`，直接导致抛出 `NotSupportedException`，解析流程崩溃。
- **痛点二（格式缺失）**：`Socks5Util.cs` 的 `UriScheme` 为空，仅支持旧版 `tg://socks?`，无法解析现代主流客户端通用格式：
  - `socks5://user:pass@host:port#remark`
  - `socks5://base64(user:pass)@host:port#remark`
  - `socks://host:port`
  - 带 URL-encode 特殊字符的密码和账号。

### 1.2 实施清单
- [x] 更新 `docs/DNS_OPTIMIZATION_DESIGN.md` 完善设计。
- [x] 修改 `Netch/Utils/ServerHelper.cs`：
  - 增强 `ServerUtilDictionary` 的容错性，支持大小写不敏感匹配，并显式为 `"Socks5"` 和 `"SOCKS"` 做双向别名兼容。
- [x] 修改 `Netch/Servers/Socks5/Socks5Util.cs`：
  - 声明 `UriScheme = new[] { "socks5", "socks" }`。
  - 重写 `ParseUri`：全面支持标准 RFC URI、Base64 认证串、Query 参数及 Telegram 格式。
  - 重写 `GetShareLink`：生成标准现代 `socks5://` 格式分享链接。
- [x] 校验 `ShareLink.cs`，确保粘贴与解析多节点无任何异常。

---

## 阶段二：底层进程模式 DNS 引擎极致重构（C++ `Redirector.bin`）（当前优先级：极高）

### 2.1 目标与架构
根据 [docs/DNS_OPTIMIZATION_DESIGN.md](file:///e:/Users/Husky/Documents/Default%20Project/netch/docs/DNS_OPTIMIZATION_DESIGN.md) 规划：
- **微秒级短路直出（Fast-Path）**：在 NetFilter 驱动回调处同步查 LRU 缓存，命中当场直出，0 线程创建开销。
- **常驻通道池（Channel Pool）**：4 通道并行长连接，彻底解决原生每次建立连接的 5-RTT 握手延迟与单通道互斥锁排队瓶颈。
- **LRU 缓存器**：2048 容量上限（< 1MB 内存），支持大小写自适应（0x20 防投毒兼容）、Transaction ID 动态重写、TTL 智能提取与异常码负缓存防御。

### 2.2 实施清单
- [x] 创建 `Redirector/DnsCache.h` & `DnsCache.cpp`。
- [x] 创建 `Redirector/PersistentDnsChannel.h` & `PersistentDnsChannel.cpp`。
- [x] 修改 `Redirector/DNSHandler.h` & `DNSHandler.cpp`：
  - 驱动回调 `CreateHandler` 加入 Fast-Path。
  - 慢速路径调用 `PersistentDnsChannel` 并回写缓存。
  - 增加 `DNSHandler::FREE()`。
- [x] 修改 `Redirector/EventHandler.cpp`：在 `eh_free()` 时释放 DNS 资源。
- [x] 更新 `Redirector/Redirector.vcxproj` & `Redirector.vcxproj.filters` 将新文件加入编译清单。

---

## 阶段三：订阅系统（Subscription）恢复与现代 Clash / Mihomo (YAML) 格式支持

### 3.1 现状与痛点
- 现代 90% 以上商业机场的默认订阅输出为 **Clash / Mihomo YAML** 格式（内含 `proxies:` 数组）。
- Netch 原生仅支持纯文本按行 Base64 解码，遇到 Clash 订阅时直接报错“没有任何节点被导入”，订阅体系名存实亡。

### 3.2 实施清单
- [x] 在 `Netch/Utils/ClashSubParser.cs` 与 `ShareLink.cs` 中实现轻量健壮的 Clash YAML 节点提取器（支持识别并提取 `ss`, `ssr`, `trojan`, `vmess`, `vless`, `socks5`, `wireguard` 字段并自动适配 Reality/Vision）。
- [x] 在 `ClashSubParser.cs` 中增加对 sing-box JSON 格式与 SSD JSON 格式订阅解析支持。
- [x] 优化 `WebUtil.cs`：预置默认客户端 UA 为 `clash.meta; Netch/1.9.10`，防止订阅被机场网关拦截。

---

## 阶段四：代理核心架构精简与统一（内核收窄为 sing-box + Xray-core）

### 4.1 目标与架构
- 彻底摒弃历史遗留、报毒率高、难以维护的独立过时核心（`Shadowsocks.exe`, `ShadowsocksR.exe`, `Trojan.exe`）。
- 全面收窄核心为 **`sing-box`** + **`Xray-core`** 双核心架构，支撑所有代理协议（SS 2022、SSR、Trojan、VMess、VLESS Reality/Vision、WireGuard、Socks5）。

### 4.2 实施清单
- [x] 移除历史废弃的控制器与配置源文件（`ShadowsocksController.cs`, `ShadowsocksRController.cs`, `TrojanController.cs`, `TrojanConfig.cs`）。
- [x] 更新所有协议的 `GetController()` 路由，统一导向 `SingboxController` 与 `V2rayController`。
- [x] 保留并强化直连远端 Socks5 裸节点零核心开销模式。

---

## 阶段五：自动化冒烟测试套件（类似 VS Code 插件的 Smoke Tests）与 CI 质量看门狗

### 5.1 目标与防护机制
- 建立全覆盖的冒烟测试套件（Smoke Tests），确保订阅解析、Socks5 编解码、内核配置生成以及核心架构边界在代码迭代时无静默破坏。
- CI 构建流水线接入端到端二进制健康检查看门狗，验证主程序及双核心二进制完备性及 CLI 正常执行。

### 5.2 实施清单
- [x] 配置 `Tests/Tests.csproj` 关联 `Netch.csproj` 并开放 `[InternalsVisibleTo]`。
- [x] 编写 `Tests/SmokeTests.cs`：
  - **订阅解析冒烟测试**：覆盖多行/行内（flow）Clash YAML 订阅（SS、VMess、VLESS Reality/Vision、Trojan、Socks5、WireGuard）及 sing-box outbounds JSON 解析。
  - **Socks5 协议与别名冒烟测试**：覆盖 RFC 规范链接、Base64 认证、URL Query 参数、IPv6 格式及双向往返序列化。
  - **内核配置生成冒烟测试**：覆盖 `sing-box` 与 `Xray-core` 各协议客户端配置文件合法性与 JSON 序列化。
  - **架构约束冒烟测试**：反射检查确保废弃控制器彻底移除，且现代控制器就绪。
- [x] 在 GitHub Actions CI 流水线（`.github/workflows/build.yml`）中挂载冒烟测试步骤与构建产物二进制存在性及版本探针检查。
