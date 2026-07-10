# AI Small Tool 使用说明


## 1. 功能简介

AI Small Tool 是 Hermes 的扩展功能。

它允许用户创建自定义 AI 快捷工具。

例如：

- 论文阅读助手
- 代码解释
- 文档总结
- 笔记整理


每个 AI 工具包含：

- 名称
- 快捷键
- Prompt 模板
- 输入文件


执行流程：

```

快捷键
↓
读取输入
↓
解析 Prompt
↓
调用 AI
↓
显示结果
↓
根据设置写回文件

```


---

# 2. 配置入口

打开：

```

Settings
|
AI Actions

```


可以管理：

- 新建 AI 工具
- 编辑
- 删除
- 修改快捷键
- 修改 Prompt


---

# 3. AI 配置


AI Small Tool 使用独立 AI 调用模块。

配置：

```

Settings
|
AI Configuration

```


支持：

- API Base URL
- API Key
- Model
- Temperature


默认模型：

```

deepseek-v4-flash

```


支持 OpenAI-compatible API。


配置保存位置：

```

%LOCALAPPDATA%\Hermes\AIAction\

```


文件：

```

actions.json
ai_config.json
ai_key.dat

```


其中：

- actions.json

保存 AI 工具配置。


- ai_config.json

保存 AI 参数配置。


- ai_key.dat

保存加密后的 API Key。


---

# 4. Prompt变量


AI 工具支持以下变量。


## $text$

当前选中的文本。


例如：

选中论文中的一段：

```

Number Theoretic Transform

```


AI收到：

```

$text$

```

会替换为：

```

Number Theoretic Transform

```


---

## $clipboard$

当前 Windows 剪贴板内容。


用于提供额外参考信息。


例如：

```

请分析：

$text$

补充：

$clipboard$

```


---

## $file_input$

读取绑定文件内容。


例如：

```

已有笔记：

$file_input$

分析：

$text$

```


执行时：

- 读取配置文件。
- 替换变量。
- 发送给 AI。


---

# 5. 文件写回


AI 工具支持将结果写回输入文件。


通过 Prompt 标签控制。


## $file_append$

追加写入。


例如：

```

根据已有笔记继续补充：

$file_input$

$text$

$file_append$

```


执行：

```

读取文件
↓
调用AI
↓
结果追加到原文件

```


---

## $file_overwrite$

覆盖写入。


例如：

```

整理以下笔记：

$file_input$

$text$

$file_overwrite$

```


执行：

```

读取文件
↓
调用AI
↓
结果覆盖原文件

```


注意：

```

$file_append$
$file_overwrite$

```

属于控制标签。

不会发送给 AI。


---

# 6. 文件输入


AI 工具可以绑定本地文本文件。


支持：

```

.md
.txt

```


选择文件后：

- 保存原文件路径。
- 不复制文件。
- 不修改文件名。


写回时：

直接修改原文件。


---

# 7. 示例


## 论文段落阅读


Prompt:

```

已有笔记：

$file_input$

当前论文内容：

$text$

请翻译并总结。

$file_overwrite$

```


效果：

- 翻译当前内容。
- 总结合并已有笔记。
- 更新论文笔记文件。


---

## 术语解释


Prompt:

```

请解释：

$text$

参考：

$file_input$

```


效果：

- 翻译术语。
- 给出背景说明。
- 不修改文件。

