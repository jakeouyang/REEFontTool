# 新增游戏与确认字体语言

## 需要改什么

使用发布版时只需更新 EXE 同级的 `Projects/games.json`，并放入该游戏的 `.list`。无需重新编译。维护源码时修改根目录 `Projects`，执行 build.ps1 会复制到 Release/Projects。重启程序读取更新。

下面是用户已确认游戏内生效的鬼武者条目。新增游戏时复制结构，替换游戏标识、EXE 名、list 名及字体路径，不要照搬路径。

```json
{
  "id": "owots",
  "name": "ONIMUSHA: WAY OF THE SWORD",
  "zh": "鬼武者：剑之道",
  "list": "OWOTS_STM_Release.list",
  "executables": ["OnimushaWotS.exe"],
  "fonts": {
    "sc": ["natives/stm/gui/ui_font/fzbwks_gb18030.oft.1"]
  }
}
```

games.json 最外层是数组 `[...]`，游戏条目之间有逗号，不允许 JSON 注释或结尾多余逗号。

| 字段 | 用途 |
| --- | --- |
| id | 唯一且稳定的标识，也是备份记录目录名。已有补丁时不要改名。 |
| name / zh | 游戏列表英文名 / 中文名。 |
| list | Projects 下的资源列表文件名。 |
| executables | 游戏目录校验用的 EXE 文件名数组，至少一个；另需根目录 re_chunk_000.pak。 |
| fonts.sc | 简体中文字体路径数组。 |
| fonts.tc | 繁体中文字体路径数组。 |
| fonts.en | 英文字体路径数组。 |

只写已经确认的语言键；不支持的语言直接省略，界面会隐藏。不可用空数组代替。每个路径必须在 list 中真实存在，以 natives/ 开头和 .oft.1 结尾。同语言有多个粗细/用途字体时可写多条，选中后全部替换。一个字体可能被多种语言共享，应在发布配置前验证影响范围。

新增配置不会让原本不兼容的游戏获得 REFramework 支持，也不会自动识别游戏分支。必须确认所用框架支持该游戏，且离散加载可正常工作。如果游戏不是此种 PAK / OFT 结构，当前程序需要代码适配，不能只加 JSON。

## 能不能自动识别

可以做辅助分析，但不能从 list 或 OFT 扩展名得出可靠的实际语言映射。list 只有路径，没有字库内容。

1. 路径关键字：GB18030 / PRC / SC、HK / TC 可作为简体或繁体候选；命名不规范时会误判。cn 也可能表示 condensed（窄体），不能据此认定中文。
2. 提取、解密 OFT 后读取字体 name、cmap 和 OS/2 表：可分析字体名称及字符覆盖倾向。cmap 表是字符到字形的映射，不是“游戏语言”标记。简繁共享许多汉字，中文字体通常也包含拉丁字母，因此同时覆盖并不表示游戏会在对应语言下使用它。
3. 查看游戏 UI/字体资源的引用，或分语言观察 REFramework 的文件访问日志：能提供实际用途证据。具体引用格式和缓存行为随游戏而异。
4. 游戏内单文件替换验证：最后确认实际用途。字库类型、资源存在、替换可见，是三个不同的验证层次。

当前 v1.1 使用显式 JSON 映射，没有内置自动解包、字体分析或语言推断；不会默默把推测路径加入支持列表。

OpenType 依据：https://learn.microsoft.com/en-us/typography/opentype/spec/cmap

## 人工确认流程

1. 在新游戏 list 搜索 `.oft.1`，保存候选完整路径。列表可能不完整；没有候选不等于游戏没有字体。
2. 用匹配该游戏的 REE.Unpacker 从原始 PAK 提取候选字体，输出到独立工作目录，不覆盖游戏资源。
3. 用工作区原 REE.FontsCryptor 解密，例如：

```powershell
& 'path/to/REE.FontsCryptor.exe' 'path/to/candidate.oft.1' 'path/to/candidate.ttf'
```

输出扩展名并不转换内部轮廓类型；解密后可能是 TTF、OTF 或 TTC，应按真实文件头识别。可用字体查看器或 fontTools 查看名称、cmap。fontTools 文档：https://fonttools.readthedocs.io/en/latest/ttLib/index.html

4. 根据字体名称和代表性简繁字符覆盖筛选候选；不要只因为某字体支持 ASCII 就归入英文。
5. 准备风格明显、且包含所测字符的替换字体。每次只配置/替换一个候选。启动游戏，切换简体、繁体、英文，检查菜单、字幕和正文；记录变化，退出游戏并还原，再测试下一文件。可结合 Loose File Loader 日志确认具体命中路径。
6. 将通过验证的路径加入正式配置，记录游戏版本、字体用途、测试语言与共享影响。不要只凭文件存在发布“已支持”的结论。

修改已有映射或 id 之前先还原活动补丁；否则新配置可能无法匹配旧备份记录。

## v1.1 的范围修正

RE2 RT 与 RE3 RT 的旧配置只有空 sc 数组，所给列表未提供明确中文 OFT 候选。本版移除这两个无效条目，保留其原始 list 供后续调查。当前 8 个配置均有实际映射路径，但除用户确认的鬼武者简体外，其余仍需游戏内验证。
