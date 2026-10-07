# Netch 订阅格式与代理微内核（sing-box / Xray）配置架构规范文档
# Proxy Core Configuration & Subscription Specification

> 本文档完整对照了 **输入层订阅/分享链接格式**（Clash YAML、v2rayN URI、sing-box JSON 等）与 **输出层代理微内核**（`sing-box` 官方规范、`Xray` 官方规范），并深度参考了 `openwrt-passwall`（`clash_subconverter.lua`、`util_sing-box.lua`、`util_xray.lua`）的工业级工程实践。
> 旨在为 Netch 彻底根除订阅解析丢失、字段错乱、IPv6/虚拟主机 404、TLS 证书异常等问题，提供完整的配置对照、转换规则与后续维护标准。

---

## 1. 总体架构与数据流转模型

Netch 采用 **两段式 Socks5 桥接解耦架构**：
```text
[网络流量 / 游戏进程]
        │
   (驱动层拦截: nfdriver.sys / Wintun)
        ▼
[Socks5 本地总线: 127.0.0.1:2801]
        │
        ├──【裸 Socks5 节点】──(直接出站直通，0 额外微内核开销)──▶ 远端服务器
        │
        └──【其他代理节点】──▶ [微内核进程: sing-box (首选) / Xray (兼容备选)] ──▶ 远端服务器
```

### 1.1 数据转换三层管线
```text
┌────────────────────────────────────────────────────────┐
│ 1. 输入层 (Subscription / Share Link)                  │
│    - Clash / Clash.Meta (Mihomo) YAML                  │
│    - URI 链接 (vmess://, vless://, trojan://, ss:// ...)│
│    - sing-box JSON Outbounds                           │
└───────────────────────────┬────────────────────────────┘
                            │ (Parse & Normalize)
                            ▼
┌────────────────────────────────────────────────────────┐
│ 2. 内存抽象层 (Netch Unified Server Models)             │
│    - VMessServer / VLESSServer / VisionServer          │
│    - TrojanServer / ShadowsocksServer                  │
│    - Hysteria2Server / TUICServer (待扩充)             │
│    - WireGuardServer / SSHServer / UrlTestServer       │
└───────────────────────────┬────────────────────────────┘
                            │ (Generate Outbound Config)
                            ▼
┌────────────────────────────────────────────────────────┐
│ 3. 输出层 (Core JSON Configurations)                    │
│    - sing-box: inbounds, outbounds, route, dns (v1.8+) │
│    - Xray: inbounds, outbounds (streamSettings), etc.  │
└────────────────────────────────────────────────────────┘
```

---

## 2. 输入层：订阅与分享链接规范对照

### 2.1 格式全景对比矩阵

| 协议 / 格式 | Clash YAML 节点标识 | URI 链接前缀 | sing-box 出站类型 (`type`) | Xray 协议 (`protocol`) |
| :--- | :--- | :--- | :--- | :--- |
| **Shadowsocks** | `type: ss` | `ss://` (SIP002) | `shadowsocks` | `shadowsocks` |
| **VMess** | `type: vmess` | `vmess://` (Base64 JSON) | `vmess` | `vmess` |
| **VLESS** | `type: vless` | `vless://` | `vless` | `vless` |
| **VLESS-Vision** | `type: vless, flow: xtls-rprx-vision` | `vless://...?flow=xtls-rprx-vision` | `vless` (带 flow) | `vless` |
| **Trojan** | `type: trojan` | `trojan://` | `trojan` | `trojan` |
| **Hysteria 2** | `type: hysteria2` / `hy2` | `hysteria2://` / `hy2://` | `hysteria2` | `hysteria2` (v26+)/外置 |
| **TUIC** | `type: tuic` | `tuic://` | `tuic` | *(Xray 原生不支持)* |
| **WireGuard** | `type: wireguard` | `wireguard://` | `wireguard` | `wireguard` |
| **Socks5** | `type: socks5` / `socks` | `tg://socks?`, `socks5://` | `socks` | `socks` |
| **SSH** | *(私有扩展)* | `ssh://` | `ssh` | *(Xray 不支持)* |

---

### 2.2 Clash / Clash.Meta (Mihomo) YAML 字段提取规则（对齐 PassWall）

在实际机场订阅中，Clash YAML 存在 **块级缩进 (Block Style)** 与 **行内单行 (Flow Style)** 两种形式，且传输层参数存在多层嵌套。

#### 1) 基础连接字段
- `server`: 服务器地址（可为域名、IPv4 或纯 IPv6）。**注意：IPv6 地址在进入 URI 或 HTTP 请求时需包裹方括号 `[...]`，在纯 socket 连接时需剥离方括号。**
- `port`: 端口（整数 `1-65535`）。
- `name`: 节点名称（用于界面备注与分流展示）。

#### 2) TLS 与安全认证字段
- `tls`: 布尔值（`true` / `false`）。
- `servername` / `sni`: 优先取 `servername`，兜底取 `sni`。此字段代表 TLS 握手 SNI。
- `skip-cert-verify`: 布尔值。若为 `true`，对应 sing-box 的 `insecure: true`、Xray 的 `allowInsecure: true`。**特别注意：许多使用免流域名伪装（如 `sni: bing.com`）的节点必须开启此项，否则 TLS 校验必死！**
- `alpn`: 字符串或数组（`["h2", "http/1.1"]` 或 `["h3"]`）。
- `client-fingerprint` / `fingerprint`: 客户端 uTLS 伪装指纹（`chrome`, `firefox`, `safari`, `randomized` 等）。
- `reality-opts`:
  - `public-key`: Reality 服务端公钥。
  - `short-id`: 客户端 Short ID。

#### 3) 传输层网络（`network` 与 `*-opts`）
- `network`: `ws`、`grpc`、`http`、`h2`、`tcp`（默认）。
- **WebSocket (`ws-opts`)**：
  - `path`: 请求路径（默认为 `"/"`）。若包含 Early Data，需解析或保留 `?ed=2048`。
  - `headers`: 包含关键请求头！**特别关注 `headers.Host`**。在 CDN 反代或 Nginx 虚拟主机场景下，`Host` 请求头是路由的唯一凭据，**绝不可丢失**！
- **gRPC (`grpc-opts`)**：
  - `grpc-service-name`: 远端 RPC 服务名。

---

### 2.3 URI 分享链接解析标准

#### 1) VMess (`vmess://<base64-json>`)
标准 v2rayN JSON 结构：
```json
{
  "v": "2",
  "ps": "节点备注",
  "add": "服务器域名或IP (如 2001:df1:7880:2::e85)",
  "port": 24444,
  "id": "18adf668-63e2-4c2c-98a2-7bb9993932be",
  "aid": 0,
  "scy": "auto",
  "net": "ws",
  "type": "none",
  "host": "伪装域名 (若为空，出站必须能优雅回退)",
  "path": "/",
  "tls": "none",
  "sni": "",
  "alpn": ""
}
```

#### 2) VLESS / Vision / Trojan (`vless://`, `trojan://`)
```text
vless://uuid@server:port?encryption=none&flow=xtls-rprx-vision&security=reality&sni=example.com&pbk=xxx&sid=yyy&fp=chrome&type=tcp#节点备注
trojan://password@server:port?security=tls&sni=example.com&allowInsecure=1&type=ws&host=example.com&path=%2F#节点备注
```
- Query 参数必须做 URLDecode 处理；
- `allowInsecure`: `"1"` 或 `"true"` 对应跳过证书验证。

#### 3) Hysteria 2 (`hysteria2://` / `hy2://`)
```text
hysteria2://password@server:port?sni=example.com&insecure=1&mport=20000-40000&obfs=salamander&obfs-password=pwd#节点备注
```

#### 4) TUIC (`tuic://`)
```text
tuic://uuid:password@server:port?congestion_control=bbr&alpn=h3&sni=example.com&allowInsecure=1#节点备注
```

---

## 3. 输出层 1：sing-box 客户端配置规范

sing-box 是 Netch 的**默认首选微内核**。配置必须对齐 sing-box 1.8+ / 1.10+ / 1.14+ 的官方严格规范。

### 3.1 完整配置文件框架 (Schema)
```json
{
  "log": {
    "level": "info",
    "timestamp": true
  },
  "dns": {
    "servers": [
      {
        "tag": "dns-remote",
        "address": "tcp://1.1.1.1",
        "detour": "proxy"
      },
      {
        "tag": "dns-local",
        "address": "local",
        "detour": "direct"
      }
    ],
    "rules": [
      {
        "outbound": "any",
        "server": "dns-local"
      }
    ],
    "strategy": "prefer_ipv4"
  },
  "inbounds": [
    {
      "type": "mixed",
      "tag": "mixed-in",
      "listen": "127.0.0.1",
      "listen_port": 2801,
      "sniff": true,
      "sniff_override_destination": false
    }
  ],
  "outbounds": [
    /* 核心代理出站 (tag: "proxy") */
    /* 直连出站 (tag: "direct") */
    /* 阻断出站 (tag: "block") */
  ],
  "route": {
    "auto_detect_interface": true,
    "final": "proxy"
  }
}
```

---

### 3.2 各协议 Outbound 构造规范

#### 1) VMess 出站 (特别是 WebSocket 与 IPv6)
```json
{
  "tag": "proxy",
  "type": "vmess",
  "server": "2001:df1:7880:2::e85",
  "server_port": 24444,
  "uuid": "18adf668-63e2-4c2c-98a2-7bb9993932be",
  "alter_id": 0,
  "security": "auto",
  "packet_encoding": "xudp",
  "transport": {
    "type": "ws",
    "path": "/",
    "headers": {
      "Host": "[2001:df1:7880:2::e85]"
    }
  }
}
```
> ⚠️ **关键防坑规范 (Critical Pitfalls)**：
> 1. **`headers.Host` 的必须性**：
>    - 若节点自身定义了 `Host`，则必须填入 `transport.headers.Host`；
>    - 若节点自身的 `Host` 为空，**必须自动回退至节点的 `Hostname`**（若是 IPv6 地址，必须包裹为 `[IPv6]` 形式）；
>    - 绝不能输出空的 `headers` 或完全不传 `headers`，否则 Nginx / CDN 必报 `HTTP 404`！
> 2. **禁用生成阶段过早 DNS 预解析**：
>    - 除非节点本身就是 IP，否则 `server` 字段必须保留域名，交由 sing-box 运行时解析。

#### 2) VLESS / Vision / Reality 出站
```json
{
  "tag": "proxy",
  "type": "vless",
  "server": "example.com",
  "server_port": 443,
  "uuid": "18adf668-63e2-4c2c-98a2-7bb9993932be",
  "flow": "xtls-rprx-vision",
  "packet_encoding": "xudp",
  "tls": {
    "enabled": true,
    "server_name": "www.apple.com",
    "utls": {
      "enabled": true,
      "fingerprint": "chrome"
    },
    "reality": {
      "enabled": true,
      "public_key": "xxx",
      "short_id": "yyy"
    }
  }
}
```

#### 3) Trojan 出站 (支持 WebSocket 与 TLS SNI 纠偏)
```json
{
  "tag": "proxy",
  "type": "trojan",
  "server": "tvhinet.lajichang.xyz",
  "server_port": 2096,
  "password": "pwd",
  "tls": {
    "enabled": true,
    "server_name": "bing.com",
    "insecure": true
  },
  "transport": {
    "type": "ws",
    "path": "/",
    "headers": {
      "Host": "bing.com"
    }
  }
}
```
> ⚠️ **关键防坑规范**：
> - Trojan 不仅有 TCP 模式，亦有 WebSocket 模式。必须根据节点的 `TransferProtocol` 挂载 `transport`。
> - 若节点配置了与真实连接地址不一致的伪装 SNI（如 `bing.com`），`tls.insecure` 必须跟随节点配置开启（或在 `server_name != server` 时智能开启），否则 TLS 握手报证书不信任直接中断！

#### 4) Shadowsocks 出站
```json
{
  "tag": "proxy",
  "type": "shadowsocks",
  "server": "1.2.3.4",
  "server_port": 8388,
  "method": "chacha20-ietf-poly1305",
  "password": "pwd",
  "udp_over_tcp": {
    "enabled": true,
    "version": 2
  }
}
```
> ⚠️ **关键防坑规范**：
> - sing-box 的 shadowsocks 出站**不原生支持**外部 SIP003 可执行文件插件（如 `plugin: obfs-local`）。若遇到带外置插件的 SS 节点，需路由至外置程序或切换至 Xray 核心。

#### 5) Hysteria 2 出站
```json
{
  "tag": "proxy",
  "type": "hysteria2",
  "server": "hy2.example.com",
  "server_port": 443,
  "password": "pwd",
  "up_mbps": 50,
  "down_mbps": 200,
  "obfs": {
    "type": "salamander",
    "password": "obfspwd"
  },
  "tls": {
    "enabled": true,
    "server_name": "hy2.example.com",
    "insecure": false,
    "alpn": ["h3"]
  }
}
```

#### 6) TUIC 出站
```json
{
  "tag": "proxy",
  "type": "tuic",
  "server": "tuic.example.com",
  "server_port": 8443,
  "uuid": "xxx",
  "password": "yyy",
  "congestion_control": "bbr",
  "udp_relay_mode": "native",
  "zero_rtt_handshake": true,
  "heartbeat": "3s",
  "tls": {
    "enabled": true,
    "server_name": "tuic.example.com",
    "alpn": ["h3"]
  }
}
```

#### 7) WireGuard 出站
```json
{
  "tag": "proxy",
  "type": "wireguard",
  "server": "1.2.3.4",
  "server_port": 51820,
  "system_interface": false,
  "local_address": ["172.16.0.2/32"],
  "private_key": "xxx",
  "peer_public_key": "yyy",
  "pre_shared_key": "zzz",
  "reserved": [0, 0, 0],
  "mtu": 1420
}
```

---

## 4. 输出层 2：Xray 客户端配置规范

Xray 作为 **全功能兼容备选微内核**，在处理老旧协议、复杂的 V2Ray 传输插件或 SIP003 插件时具有更广泛的兼容性。

### 4.1 配置文件框架
```json
{
  "log": {
    "loglevel": "warning"
  },
  "inbounds": [
    {
      "tag": "mixed-in",
      "port": 2801,
      "listen": "127.0.0.1",
      "protocol": "socks",
      "sniffing": {
        "enabled": true,
        "destOverride": ["http", "tls"]
      },
      "settings": {
        "auth": "noauth",
        "udp": true
      }
    }
  ],
  "outbounds": [
    /* 核心 proxy 出站 */
    {
      "tag": "direct",
      "protocol": "freedom"
    },
    {
      "tag": "block",
      "protocol": "blackhole"
    }
  ],
  "routing": {
    "domainStrategy": "AsIs",
    "rules": []
  }
}
```

### 4.2 Xray streamSettings 规范矩阵

| 传输层 | `streamSettings.network` | 专有配置项 |
| :--- | :--- | :--- |
| **TCP / Raw** | `"tcp"` | `rawSettings` / `tcpSettings` (可选带 http header 伪装) |
| **WebSocket** | `"ws"` | `wsSettings: { "path": "...", "headers": { "Host": "..." } }` |
| **gRPC** | `"grpc"` | `grpcSettings: { "serviceName": "...", "multiMode": false }` |
| **HTTPUpgrade** | `"httpupgrade"` | `httpupgradeSettings: { "path": "...", "host": "..." }` |
| **TLS** | `security: "tls"` | `tlsSettings: { "serverName": "...", "allowInsecure": false, "fingerprint": "chrome" }` |
| **Reality** | `security: "reality"` | `realitySettings: { "serverName": "...", "publicKey": "...", "shortId": "...", "fingerprint": "chrome" }` |

---

## 5. PassWall 经验吸收与 Netch 适配架构建议

对照 PassWall 的 `clash_subconverter.lua` 和 `util_sing-box.lua`，Netch 现有实现（`ClashSubParser.cs` 与 `SingboxConfigUtils.cs`）的重构升级应当包含以下几点：

### 5.1 订阅解析层（`ClashSubParser.cs` 升级）
1. **替换为健壮的状态机或引入轻量级 YAML 递归解析器**：
   - 必须支持嵌套结构：解析 `ws-opts` 时，子项 `headers` 展开为字典，准确提取 `headers.Host`；
   - 支持 Flow Style 行内花括号解析；
   - 提取并保留节点级 `skip-cert-verify`、`sni` / `servername`。
2. **扩充现代协议支持**：
   - 在解析器中增加对 `hysteria2`（`hy2`）与 `tuic` 的识别；
   - 为 Netch 新增 `Hysteria2Server` 与 `TUICServer` 数据模型类，避免现代机场优质节点被静默丢弃。

### 5.2 出站生成层（`SingboxConfigUtils.cs` 升级）
1. **Host 请求头与 IPv6 自动兜底机制**：
   ```csharp
   // 当 WebSocket Host 为空时，优雅回退至 Hostname (IPv6 自动包裹方括号)
   var hostHeader = !string.IsNullOrWhiteSpace(server.Host) 
       ? server.Host 
       : (IPAddress.TryParse(server.Hostname, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6 
           ? $"[{ip}]" 
           : server.Hostname);
   ```
2. **Trojan 节点挂载 WebSocket 传输层**：
   对齐 VMess，根据节点的 `TransferProtocol` 挂载 `ws` / `grpc` 配置。
3. **域名直出，避免预先解析 IP**：
   停止在 `SingboxConfigUtils` 中强制调用 `AutoResolveHostnameAsync` 覆写 `server`，保持 `server = server.Hostname`，交由 sing-box 本地解析；若节点是纯 IP 则直出。
4. **SNI 与 Insecure 继承**：
   若节点自身配置了 `allowInsecure: true` 或携带了伪装 SNI，自动下发 `tls.insecure = true`，杜绝证书校验阻断。

---

## 6. 维护与测试基线清单

每次对核心配置生成与订阅解析进行迭代维护时，必须执行以下验证矩阵：

1. **IPv6 兼容测试**：纯 IPv6 VMess WebSocket 节点，生成的 `singbox.json` 必须包含合法的 `Host` 请求头，sing-box 进程启动且无 404 错误。
2. **伪装 SNI 测试**：Trojan + SNI (`bing.com`) 节点，生成的配置必须携带 `insecure: true`，TLS 握手无 bad certificate。
3. **URLTest 兼容测试**：候选池中混杂 Socks5、VMess、Trojan，生成的 `urltest` 组能正常并发测速与切换。
4. **配置文件语法校验命令**：
   ```powershell
   & "E:\Tools\Netch\bin\sing-box.exe" check -c "E:\Tools\Netch\data\singbox.json"
   ```
   输出为空即表示完全符合 sing-box 微内核语法。
