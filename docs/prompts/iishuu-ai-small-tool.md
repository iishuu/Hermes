# Hermes AI Action Framework 二次开发需求说明

## 1. 项目目标

基于 Hermes 项目进行二次开发，新增 AI Action 功能。

目标：

将 Hermes 扩展为一个可配置的 AI 快捷操作工具。

用户可以配置多个 AI Action：

* 功能名称
* 快捷键
* Prompt 模板
* 输入文件

执行流程：

```
快捷键触发
    |
    v
读取 Action 配置
    |
    v
收集输入变量
    |
    v
解析 Prompt 模板
    |
    v
调用 AI
    |
    v
显示结果
    |
    v
执行文件写回操作
```

要求：

* 保持 Hermes 原翻译功能不变。
* 新增 AI Action 独立实现。
* 尽量复用已有：

  * API调用模块
  * 快捷键模块
  * 结果显示窗口
  * 设置页面框架。

---

# 2. 配置存储

## 2.1 Action配置目录

固定目录：

```
%APPDATA%\Hermes\AIAction\
```

结构：

```
AIAction
|
├── actions.json
|
└── Context
    |
    ├── kyber.md
    └── transformer.md
```

程序启动：

```
启动 Hermes

↓

读取 actions.json

↓

加载 Action

↓

注册快捷键
```

配置变化：

* 新建
* 编辑
* 删除

均实时写回 JSON。

---

# 3. Action 数据结构

新增：

```
Models
└── AIAction.cs
```

结构：

```csharp
class AIAction
{
    Guid Id;

    string Name;

    string Hotkey;

    int Order;

    string PromptTemplate;

    string InputFile;
}
```

说明：

| 字段             | 说明       |
| -------------- | -------- |
| Id             | 唯一标识     |
| Name           | 显示名称     |
| Hotkey         | 快捷键      |
| Order          | 排序       |
| PromptTemplate | Prompt模板 |
| InputFile      | 绑定输入文件   |

---

# 4. AI全局配置

新增设置页面：

```
Settings

└── AI Configuration
```

页面标题：

```
DeepSeek API 配置
```

说明：

第一阶段所有 AI Action 均使用此配置。

不允许：

* 每个 Action 单独配置 API。
* Action保存API Key。

---

## 配置内容

字段：

```
API Base URL

API Key

Model

Temperature
```

默认：

```
Base URL:

https://api.deepseek.com


Model:

deepseek-chat
```

保存：

```
%APPDATA%\Hermes\AIAction\ai_config.json
```

---

# 5. AI Action管理界面

新增：

```
Settings

└── AI Actions
```

要求：

使用卡片式布局，不使用普通表格。

示例：

```
+--------------------------------+

论文分析

快捷键:
Ctrl + Alt + P


输入文件:
kyber.md


Prompt:
已配置


[编辑] [删除]


+--------------------------------+
```

功能：

* 新建
* 编辑
* 删除
* 排序

---

# 6. Action编辑界面

字段：

## 名称

文本输入。

---

## 快捷键

支持：

```
Ctrl + Alt + P
```

要求：

* 检测冲突。
* 冲突时提示已有 Action。

---

## 输入文件

选择：

```
[选择文件]
```

第一阶段：

支持：

```
.txt
.md
```

编码：

```
UTF-8
```

保存：

文件名。

文件目录：

```
%APPDATA%\Hermes\AIAction\Context
```

---

# 7. Prompt编辑器

Prompt编辑框需要支持富文本显示效果。

要求：

变量高亮：

特殊变量：

```
$text$

$clipboard$

$file_input$

$file_append$

$file_overwrite$
```

显示：

* 不同颜色。
* 加粗。
* 与普通文本明显区分。

例如：

```
请分析：

$text$

参考资料：

$file_input$

$file_append$
```

其中变量显示为高亮标签。

---

# 8. Prompt变量

支持变量：

| 变量               | 作用     |
| ---------------- | ------ |
| $text$           | 当前划词文本 |
| $clipboard$      | 当前剪贴板  |
| $file_input$     | 读取绑定文件 |
| $file_append$    | 追加写回控制 |
| $file_overwrite$ | 覆盖写回控制 |

---

# 9. Prompt解析规则

执行顺序：

## 第一步：检测控制变量

检测：

```
$file_append$

$file_overwrite$
```

它们：

* 不是 Prompt 内容。
* 不发送给 AI。

例如：

原：

```
总结：

$text$

$file_append$
```

发送给 AI：

```
总结：

Transformer
```

同时记录：

```
SaveMode = Append
```

---

## 第二步：替换输入变量

替换：

```
$text$

$clipboard$

$file_input$
```

---

## 第三步：调用AI

---

# 10. 文件写回

根据控制变量决定。

## $file_append$

表示：

AI结果追加到输入文件。

流程：

```
读取文件

↓

发送AI

↓

获取结果

↓

追加结果
```

---

## $file_overwrite$

表示：

AI结果覆盖输入文件。

流程：

```
读取文件

↓

发送AI

↓

获取结果

↓

覆盖文件
```

覆盖前：

弹确认：

```
是否覆盖：

xxx.md
```

---

# 11. AI调用接口

新增：

```
Services
└── AIProvider
```

接口：

```csharp
Task<string> Chat(
    AIRequest request
);
```

请求：

```csharp
class AIRequest
{
    string Prompt;

    string Model;

    double Temperature;
}
```

---

# 12. DeepSeek API

使用 OpenAI Compatible API。

地址：

```
POST

https://api.deepseek.com/chat/completions
```

Header：

```
Authorization:
Bearer API_KEY

Content-Type:
application/json
```

Body：

```json
{
 "model":"deepseek-chat",

 "messages":[
  {
   "role":"user",
   "content":"Prompt"
  }
 ],

 "temperature":0.7
}
```

返回：

```
choices[0].message.content
```

---

# 13. 快捷键管理

## AI Action快捷键

启动：

```
读取 actions.json

↓

注册快捷键
```

修改：

* 新增 Action
* 删除 Action
* 修改快捷键

实时更新。

---

# 14. 原 Hermes快捷键处理

检查已有快捷键设置。

如果已有：

* 删除按钮

保持。

如果没有：

新增：

```
清空快捷键
```

功能：

例如：

原：

```
Ctrl + Alt + E
```

点击清空：

```
Hotkey=""
```

含义：

* 不再注册。
* 不检测冲突。
* 不触发功能。

---

# 15. 文件结构

新增：

```
Hermes.Windows

├── Models
│
│── AIAction.cs
│── AIConfig.cs
│
├── Services
│
│── AIAction
│   ├── AIActionExecutor.cs
│   └── AIActionConfigService.cs
│
│── Prompt
│   └── PromptTemplateResolver.cs
│
│── Context
│   ├── ContextFileService.cs
│   └── ContextWriteService.cs
│
│── AIProvider
│   ├── IAIProvider.cs
│   └── DeepSeekProvider.cs
│
│── Hotkey
│   └── AIActionHotkeyManager.cs
```

---

# 16. 第一阶段必须实现

* AI Action管理界面。
* 卡片式UI。
* JSON配置保存。
* 启动读取配置。
* AI全局配置窗口。
* DeepSeek调用。
* 动态快捷键。
* Prompt变量高亮。
* Prompt解析。
* `$text$`
* `$clipboard$`
* `$file_input$`
* `$file_append$`
* `$file_overwrite$`
* 文件追加。
* 文件覆盖。
* 结果窗口复用。

---

# 17. 第一阶段不实现

不实现：

* 图片输入。
* 多模态模型。
* RAG。
* 向量数据库。
* 对话历史。
* 长期记忆。
* Agent。
